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
