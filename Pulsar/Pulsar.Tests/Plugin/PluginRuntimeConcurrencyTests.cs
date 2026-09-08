using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Tests.TestHelpers;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    public class PluginRuntimeConcurrencyTests
    {
        [Fact]
        public async Task GetOrActivatePluginAsync_ConcurrentRequests_ShouldCreateSingleInstance()
        {
            var plugin = new ConcurrencyTestPlugin();
            var loader = new CountingPluginLoader(plugin);
            var catalog = new PluginCatalog();
            catalog.RegisterDescriptors(new[] { CreateDescriptor(plugin) });
            var state = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(state, new PluginCircuitBreakerPolicy());

            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(),
                loader,
                catalog,
                state,
                pipeline,
                NullLogger<PluginRuntimeKernel>.Instance);

            var first = kernel.GetOrActivatePluginAsync(plugin.Id);
            var second = kernel.GetOrActivatePluginAsync(plugin.Id);

            var instances = await Task.WhenAll(first, second);

            loader.ActivationCount.Should().Be(1,
                "concurrent activation requests must share one per-plugin gate");
            instances[0].Should().BeSameAs(plugin);
            instances[1].Should().BeSameAs(plugin);
        }

        [Fact]
        public async Task ExecuteAsync_ConcurrentRequests_ShouldBlockSecondRequestUntilFirstCompletes()
        {
            var plugin = new ConcurrencyTestPlugin(executionDelay: TimeSpan.FromMilliseconds(150));
            var state = new PluginRuntimeStateStore();
            state.SetPlugin(plugin, PluginLifecycleState.Enabled);
            var breaker = new PluginCircuitBreakerPolicy();
            var pipeline = new PluginExecutionPipeline(
                state,
                breaker,
                executionTimeout: TimeSpan.FromSeconds(2));

            var request = CreateRequest(plugin);

            var first = pipeline.ExecuteAsync(request);
            await Task.Delay(50);

            var second = await pipeline.ExecuteAsync(request);
            second.Kind.Should().Be(PluginExecutionOutcomeKind.Blocked,
                "the default execution policy is one action per plugin at a time");
            second.Result.ErrorCode.Should().Be(PluginErrorCode.TemporaryUnavailable);

            var firstOutcome = await first;
            firstOutcome.Kind.Should().Be(PluginExecutionOutcomeKind.Success);
            state.GetState(plugin.Id).Should().Be(PluginLifecycleState.Enabled);
        }

        // ============ C4: execution ↔ teardown coordination seam ============

        [Fact]
        public async Task AcquireExecutionGate_ShouldBlockNewExecutions_UntilReleased()
        {
            var plugin = new ConcurrencyTestPlugin();
            var pipeline = CreatePipeline(plugin, executionTimeout: TimeSpan.FromSeconds(2));

            using var gate = await pipeline.AcquireExecutionGateAsync(plugin.Id, TimeSpan.FromSeconds(2));
            gate.Should().NotBeNull("an idle plugin's gate is immediately acquirable");

            var blocked = await pipeline.ExecuteAsync(CreateRequest(plugin));
            blocked.Kind.Should().Be(PluginExecutionOutcomeKind.Blocked,
                "no new action may start while a teardown holds the plugin's execution gate");
        }

        [Fact]
        public async Task AcquireExecutionGate_ShouldWaitForInFlightExecution()
        {
            var plugin = new GatedTestPlugin();
            var pipeline = CreatePipeline(plugin, executionTimeout: TimeSpan.FromSeconds(5));

            var execution = pipeline.ExecuteAsync(CreateRequest(plugin));
            await plugin.WaitUntilExecutingAsync();

            var acquireTask = pipeline.AcquireExecutionGateAsync(plugin.Id, TimeSpan.FromSeconds(5));

            acquireTask.IsCompleted.Should().BeFalse(
                "the gate must not be handed over while the action is still running");
            plugin.Release();
            using var gate = await acquireTask;
            gate.Should().NotBeNull("the gate is acquired once the in-flight action completes");

            (await execution).Kind.Should().Be(PluginExecutionOutcomeKind.Success);
        }

        [Fact]
        public async Task AcquireExecutionGate_ShouldReturnNull_WhenInFlightExceedsTimeout()
        {
            var plugin = new GatedTestPlugin();
            var pipeline = CreatePipeline(plugin, executionTimeout: TimeSpan.FromSeconds(5));

            var execution = pipeline.ExecuteAsync(CreateRequest(plugin));
            await plugin.WaitUntilExecutingAsync();

            var gate = await pipeline.AcquireExecutionGateAsync(plugin.Id, TimeSpan.FromMilliseconds(100));
            gate.Should().BeNull("a teardown must not proceed while the action is still running");

            plugin.Release();
            (await execution).Kind.Should().Be(PluginExecutionOutcomeKind.Success);
        }

        [Fact]
        public async Task DeactivatePluginAsync_ShouldWaitForInFlightExecution_BeforeTeardown()
        {
            var plugin = new GatedLifecycleTestPlugin();
            var loader = new CountingPluginLoader(plugin);
            var catalog = new PluginCatalog();
            catalog.RegisterDescriptors(new[] { CreateDescriptor(plugin) });
            var state = new PluginRuntimeStateStore();
            state.SetPlugin(plugin, PluginLifecycleState.Enabled);
            var pipeline = new PluginExecutionPipeline(
                state,
                new PluginCircuitBreakerPolicy(),
                executionTimeout: TimeSpan.FromSeconds(5));
            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(),
                loader,
                catalog,
                state,
                pipeline,
                NullLogger<PluginRuntimeKernel>.Instance);

            var execution = pipeline.ExecuteAsync(CreateRequest(plugin));
            await plugin.WaitUntilExecutingAsync();

            var deactivate = kernel.DeactivatePluginAsync(plugin.Id);
            await Task.Delay(100);
            plugin.UnloadCount.Should().Be(0,
                "teardown (OnUnloadAsync onward) must not start while the action is still executing");

            plugin.Release();
            await deactivate;
            plugin.UnloadCount.Should().Be(1, "teardown runs once the in-flight action has completed");

            (await execution).Kind.Should().Be(PluginExecutionOutcomeKind.Success);
        }

        [Fact]
        public async Task DeactivatePluginAsync_ShouldThrow_WhenExecutionExceedsDrainBudget()
        {
            var plugin = new GatedLifecycleTestPlugin();
            var loader = new CountingPluginLoader(plugin);
            var catalog = new PluginCatalog();
            catalog.RegisterDescriptors(new[] { CreateDescriptor(plugin) });
            var state = new PluginRuntimeStateStore();
            state.SetPlugin(plugin, PluginLifecycleState.Enabled);
            // The gated action ignores cancellation and hangs, so the pipeline's
            // own force-cancel never fires; the injected 200ms drain budget is
            // what fails the deactivate. (Default budget: ExecutionTimeout + 5s.)
            var pipeline = new PluginExecutionPipeline(
                state,
                new PluginCircuitBreakerPolicy(),
                executionTimeout: TimeSpan.FromSeconds(5));
            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(),
                loader,
                catalog,
                state,
                pipeline,
                NullLogger<PluginRuntimeKernel>.Instance,
                executionDrainTimeout: TimeSpan.FromMilliseconds(200));

            var execution = pipeline.ExecuteAsync(CreateRequest(plugin));
            await plugin.WaitUntilExecutingAsync();

            Func<Task> act = () => kernel.DeactivatePluginAsync(plugin.Id);

            await act.Should().ThrowAsync<InvalidOperationException>(
                "a deactivate that cannot drain must fail closed instead of zombie-ing the action on a torn-down plugin");
            plugin.UnloadCount.Should().Be(0, "no teardown step may run when the drain fails");

            plugin.Release();
            (await execution).Kind.Should().Be(PluginExecutionOutcomeKind.Success);
        }

        private static PluginDescriptor CreateDescriptor(IPulsarPlugin plugin)
        {
            return new PluginDescriptor
            {
                Id = plugin.Id,
                DisplayName = plugin.DisplayName,
                Version = plugin.Version,
                Author = plugin.Author,
                Description = plugin.Description,
                Icon = plugin.Icon,
                CanDisable = plugin.CanDisable,
                Tier = plugin.CanDisable ? PluginTier.Extension : PluginTier.Core,
                ImplementationType = plugin.GetType(),
                Dependencies = new List<string>(),
                Metadata = new PluginMetadata
                {
                    Id = plugin.Id,
                    Display = new DisplayInfo
                    {
                        Name = plugin.DisplayName,
                        Description = plugin.Description,
                        IconKey = plugin.Icon,
                        Category = "Tests",
                        Version = plugin.Version,
                        Author = plugin.Author,
                        License = "MIT"
                    },
                    Schema = null,
                    UI = new UIHints
                    {
                        Badge = "Test",
                        AccentColor = "#4A90E2",
                        ShowInQuickAccess = false,
                        SortOrder = 0
                    },
                    Capabilities = new PluginCapabilities
                    {
                        SupportedActions = new List<string> { "test" },
                        Dependencies = new List<string>(),
                        Tier = plugin.CanDisable ? PluginTier.Extension : PluginTier.Core,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }

        private static PluginExecutionRequest CreateRequest(IPulsarPlugin plugin)
        {
            return new PluginExecutionRequest
            {
                Descriptor = CreateDescriptor(plugin),
                Action = "test",
                Args = new Dictionary<string, string>(),
                Context = PulsarContextFactory.CreateTestContext(),
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin),
                CancellationToken = CancellationToken.None
            };
        }

        private static PluginExecutionPipeline CreatePipeline(IPulsarPlugin plugin, TimeSpan executionTimeout)
        {
            var state = new PluginRuntimeStateStore();
            state.SetPlugin(plugin, PluginLifecycleState.Enabled);
            return new PluginExecutionPipeline(
                state,
                new PluginCircuitBreakerPolicy(),
                executionTimeout: executionTimeout);
        }

        /// <summary>
        /// Plugin whose action blocks until <see cref="Release"/> — lets a test
        /// hold an action in-flight deterministically (no sleeps).
        /// </summary>
        private class GatedTestPluginBase : IPulsarPlugin
        {
            private readonly TaskCompletionSource _executing = new(TaskCreationOptions.RunContinuationsAsynchronously);
            private readonly TaskCompletionSource _release = new(TaskCreationOptions.RunContinuationsAsynchronously);

            public string Id => "test.gated.plugin";
            public string DisplayName => "Gated Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Gated concurrency test plugin";
            public string Icon => "G";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services)
            {
            }

            public async Task<PluginResult> ExecuteAsync(
                string action,
                IReadOnlyDictionary<string, string> args,
                PulsarContext context,
                CancellationToken cancellationToken = default)
            {
                _executing.TrySetResult();
                // Deliberately ignores cancellationToken: models a hung plugin that
                // holds its execution gate past the pipeline's force-cancel budget.
                await _release.Task.ConfigureAwait(false);
                return PluginResult.Ok("Success");
            }

            public Task WaitUntilExecutingAsync() => _executing.Task;

            public void Release() => _release.TrySetResult();
        }

        private sealed class GatedTestPlugin : GatedTestPluginBase
        {
        }

        private sealed class GatedLifecycleTestPlugin : GatedTestPluginBase, IPluginLifecycle
        {
            private int _unloadCount;

            public int UnloadCount => Volatile.Read(ref _unloadCount);

            public Task OnEnableAsync() => Task.CompletedTask;

            public Task OnDisableAsync() => Task.CompletedTask;

            public Task OnUnloadAsync()
            {
                Interlocked.Increment(ref _unloadCount);
                return Task.CompletedTask;
            }
        }

        private sealed class ConcurrencyTestPlugin : IPulsarPlugin
        {
            private readonly TimeSpan _executionDelay;

            public ConcurrencyTestPlugin(TimeSpan executionDelay = default)
            {
                _executionDelay = executionDelay;
            }

            public string Id => "test.concurrency.plugin";
            public string DisplayName => "Concurrency Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Concurrency test plugin";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services)
            {
            }

            public async Task<PluginResult> ExecuteAsync(
                string action,
                IReadOnlyDictionary<string, string> args,
                PulsarContext context,
                CancellationToken cancellationToken = default)
            {
                if (_executionDelay > TimeSpan.Zero)
                {
                    await Task.Delay(_executionDelay, cancellationToken);
                }

                return PluginResult.Ok("Success");
            }
        }

        private sealed class CountingPluginLoader : PluginLoader
        {
            private readonly IPulsarPlugin _plugin;
            private int _activationCount;

            public CountingPluginLoader(IPulsarPlugin plugin)
                : base(Mock.Of<IServiceProvider>(), "unused")
            {
                _plugin = plugin;
            }

            public int ActivationCount => Volatile.Read(ref _activationCount);

            public override IPulsarPlugin ActivatePlugin(PluginDescriptor descriptor)
            {
                Interlocked.Increment(ref _activationCount);
                Thread.Sleep(150);
                return _plugin;
            }
        }
    }
}
