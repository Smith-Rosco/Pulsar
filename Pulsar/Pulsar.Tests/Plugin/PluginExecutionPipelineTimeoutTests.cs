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
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    public class PluginExecutionPipelineTimeoutTests
    {
        [Fact]
        public async Task PipelineTimeout_ShouldTransitionToFaulted_AndRecordBreakerFailure()
        {
            var breakerPolicy = new PluginCircuitBreakerPolicy(
                NullLogger<PluginCircuitBreakerPolicy>.Instance,
                Mock.Of<IPluginHealthMonitor>(),
                Mock.Of<ITrayService>());

            var runtimeState = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy);

            var plugin = new HungTestPlugin();
            var descriptor = CreateDescriptor(plugin, canDisable: true, tier: PluginTier.Extension);
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var request = new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "hang",
                Args = new Dictionary<string, string>(),
                Context = PulsarContextFactory.CreateTestContext(),
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin),
                CancellationToken = CancellationToken.None
            };

            var outcome = await pipeline.ExecuteAsync(request, cts.Token);

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Blocked, "timeout should produce a Blocked outcome");
            outcome.Result.Severity.Should().Be(PluginErrorSeverity.Critical, "timeout severity should be Critical");
            outcome.Result.Message.Should().Contain("timed out", "message should indicate timeout");

            var snapshot = runtimeState.GetSnapshot(plugin.Id);
            snapshot.State.Should().Be(PluginLifecycleState.Faulted, "plugin should transition to Faulted on timeout");
            snapshot.LastError.Should().NotBeNull("faulted state should include error");
            snapshot.LastError.Should().BeOfType<TimeoutException>("error should be TimeoutException");
        }

        [Fact]
        public async Task PipelineTimeout_ShouldNotOpenBreaker_ForCorePlugins()
        {
            var breakerPolicy = new PluginCircuitBreakerPolicy(
                NullLogger<PluginCircuitBreakerPolicy>.Instance,
                Mock.Of<IPluginHealthMonitor>(),
                Mock.Of<ITrayService>());

            var runtimeState = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy);

            var plugin = new HungTestPlugin();
            var descriptor = CreateDescriptor(plugin, canDisable: false, tier: PluginTier.Core);
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            using var cts = new CancellationTokenSource(TimeSpan.FromMilliseconds(500));

            var request = new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "hang",
                Args = new Dictionary<string, string>(),
                Context = PulsarContextFactory.CreateTestContext(),
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin),
                CancellationToken = CancellationToken.None
            };

            var outcome = await pipeline.ExecuteAsync(request, cts.Token);

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Blocked);
            var snapshot = runtimeState.GetSnapshot(plugin.Id);
            snapshot.State.Should().Be(PluginLifecycleState.Faulted, "core plugin should still transition to Faulted on timeout");

            var availability = breakerPolicy.CheckAvailability(descriptor, plugin.Id);
            availability.Allowed.Should().BeTrue("breaker should NOT open for core plugins");
        }

        [Fact]
        public async Task PipelineNormalExecution_ShouldSucceed_WithCancellationToken()
        {
            var breakerPolicy = new PluginCircuitBreakerPolicy(
                NullLogger<PluginCircuitBreakerPolicy>.Instance,
                Mock.Of<IPluginHealthMonitor>(),
                Mock.Of<ITrayService>());

            var runtimeState = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy);

            var plugin = new FastTestPlugin();
            var descriptor = CreateDescriptor(plugin, canDisable: true, tier: PluginTier.Extension);
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            var request = new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "fast",
                Args = new Dictionary<string, string>(),
                Context = PulsarContextFactory.CreateTestContext(),
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin),
                CancellationToken = CancellationToken.None
            };

            var outcome = await pipeline.ExecuteAsync(request);

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Success);
            outcome.Result.Success.Should().BeTrue();
            var snapshot = runtimeState.GetSnapshot(plugin.Id);
            snapshot.State.Should().Be(PluginLifecycleState.Enabled, "successful plugins should return to ready state");
        }

        private sealed class HungTestPlugin : IPulsarPlugin
        {
            public string Id => "test.hung.plugin";
            public string DisplayName => "Hung Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Test plugin that simulates a hang";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services) { }

            public async Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
            {
                await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
                return PluginResult.Ok("Should not reach here");
            }
        }

        private sealed class FastTestPlugin : IPulsarPlugin
        {
            public string Id => "test.fast.plugin";
            public string DisplayName => "Fast Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Test plugin that completes quickly";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services) { }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PluginResult.Ok("Fast success"));
            }
        }

        private static PluginDescriptor CreateDescriptor(IPulsarPlugin plugin, bool canDisable, PluginTier tier)
        {
            return new PluginDescriptor
            {
                Id = plugin.Id,
                DisplayName = plugin.DisplayName,
                Version = plugin.Version,
                Author = plugin.Author,
                Description = plugin.Description,
                Icon = plugin.Icon,
                CanDisable = canDisable,
                Tier = tier,
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
                        SupportedActions = new List<string> { "hang", "fast" },
                        Dependencies = new List<string>(),
                        Tier = tier,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }
    }
}
