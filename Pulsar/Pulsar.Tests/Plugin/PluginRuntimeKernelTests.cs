using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;

namespace Pulsar.Tests.Plugin
{
    public class PluginRuntimeKernelTests
    {
        [Fact]
        public async Task ExecuteAsync_ShouldRecordSuccess_ThroughUnifiedPipeline()
        {
            var runtimeState = new PluginRuntimeStateStore();
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var usageTracker = new Mock<IPluginUsageTracker>();
            var breakerPolicy = new PluginCircuitBreakerPolicy(healthMonitor: healthMonitor.Object);
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy, usageTracker: usageTracker.Object, healthMonitor: healthMonitor.Object);
            var plugin = new RuntimeTestPlugin();
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            var descriptor = CreateDescriptor(plugin.Id, PluginTier.Extension, canDisable: true, plugin.GetType());
            var context = PulsarContextFactory.CreateTestContext();

            var outcome = await pipeline.ExecuteAsync(new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "run",
                Args = new Dictionary<string, string>().AsReadOnly(),
                Context = context,
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin)
            });

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Success);
            outcome.Result.Success.Should().BeTrue();
            runtimeState.GetState(plugin.Id).Should().Be(PluginLifecycleState.Enabled);
            usageTracker.Verify(x => x.RecordExecution(plugin.Id, true, It.IsAny<long>(), context.TargetProcessName), Times.Once);
            healthMonitor.Verify(x => x.RecordSuccess(plugin.Id), Times.Once);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRecordHandledFailureTelemetry_ThroughUnifiedPipeline()
        {
            var runtimeState = new PluginRuntimeStateStore();
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var usageTracker = new Mock<IPluginUsageTracker>();
            var breakerPolicy = new PluginCircuitBreakerPolicy(healthMonitor: healthMonitor.Object);
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy, usageTracker: usageTracker.Object, healthMonitor: healthMonitor.Object);
            var plugin = new RuntimeTestPlugin
            {
                ResultFactory = () => PluginResult.Error("handled failure")
            };
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            var descriptor = CreateDescriptor(plugin.Id, PluginTier.Extension, canDisable: true, plugin.GetType());
            var context = PulsarContextFactory.CreateTestContext();

            var outcome = await pipeline.ExecuteAsync(new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "run",
                Args = new Dictionary<string, string>().AsReadOnly(),
                Context = context,
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin)
            });

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.HandledFailure);
            outcome.Result.Success.Should().BeFalse();
            usageTracker.Verify(x => x.RecordExecution(plugin.Id, false, It.IsAny<long>(), context.TargetProcessName), Times.Once);
            healthMonitor.Verify(x => x.RecordError(plugin.Id, It.Is<Exception>(ex => ex.Message == "handled failure"), "run"), Times.Once);
            healthMonitor.Verify(x => x.RecordSuccess(It.IsAny<string>()), Times.Never);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRecordFault_AndTripBreaker_AfterRepeatedExceptions()
        {
            var runtimeState = new PluginRuntimeStateStore();
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var breakerPolicy = new PluginCircuitBreakerPolicy(healthMonitor: healthMonitor.Object);
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy, healthMonitor: healthMonitor.Object);
            var plugin = new RuntimeTestPlugin { ThrowOnExecute = true };
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            var descriptor = CreateDescriptor(plugin.Id, PluginTier.Extension, canDisable: true, plugin.GetType());
            var context = PulsarContextFactory.CreateTestContext();

            for (int i = 0; i < 3; i++)
            {
                var outcome = await pipeline.ExecuteAsync(new PluginExecutionRequest
                {
                    Descriptor = descriptor,
                    Action = "run",
                    Args = new Dictionary<string, string>().AsReadOnly(),
                    Context = context,
                    IsEnabled = () => true,
                    ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin)
                });

                outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Exception);
            }

            var blocked = await pipeline.ExecuteAsync(new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "run",
                Args = new Dictionary<string, string>().AsReadOnly(),
                Context = context,
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin)
            });

            blocked.Kind.Should().Be(PluginExecutionOutcomeKind.Blocked);
            blocked.Result.Message.Should().Contain("disabled for safety");
            runtimeState.GetState(plugin.Id).Should().Be(PluginLifecycleState.Faulted);
            healthMonitor.Verify(x => x.RecordCircuitBreakerTrip(plugin.Id), Times.Once);
            healthMonitor.Verify(x => x.RecordError(plugin.Id, It.IsAny<Exception>(), "run"), Times.Exactly(3));
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRecordRecoveryTelemetry_AfterBreakerCooldown()
        {
            var runtimeState = new PluginRuntimeStateStore();
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var usageTracker = new Mock<IPluginUsageTracker>();
            var breakerPolicy = new PluginCircuitBreakerPolicy(healthMonitor: healthMonitor.Object);
            var failingPlugin = new RuntimeTestPlugin { ThrowOnExecute = true };
            runtimeState.SetPlugin(failingPlugin, PluginLifecycleState.Enabled);

            var descriptor = CreateDescriptor(failingPlugin.Id, PluginTier.Extension, canDisable: true, failingPlugin.GetType());
            var context = PulsarContextFactory.CreateTestContext();

            for (int i = 0; i < 3; i++)
            {
                var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy, usageTracker: usageTracker.Object, healthMonitor: healthMonitor.Object);
                await pipeline.ExecuteAsync(new PluginExecutionRequest
                {
                    Descriptor = descriptor,
                    Action = "run",
                    Args = new Dictionary<string, string>().AsReadOnly(),
                    Context = context,
                    IsEnabled = () => true,
                    ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(failingPlugin)
                });
            }

            var brokenAtField = typeof(PluginCircuitBreakerPolicy).GetField("_brokenCircuits", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            brokenAtField.Should().NotBeNull();
            var brokenCircuits = brokenAtField!.GetValue(breakerPolicy).Should().BeAssignableTo<System.Collections.Generic.Dictionary<string, DateTime>>().Subject;
            brokenCircuits[failingPlugin.Id] = DateTime.UtcNow - TimeSpan.FromMinutes(2);

            var recoveredPlugin = new RuntimeTestPlugin();
            runtimeState.SetPlugin(recoveredPlugin, PluginLifecycleState.Faulted);
            var recoveryPipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy, usageTracker: usageTracker.Object, healthMonitor: healthMonitor.Object);

            var outcome = await recoveryPipeline.ExecuteAsync(new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "run",
                Args = new Dictionary<string, string>().AsReadOnly(),
                Context = context,
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(recoveredPlugin)
            });

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Success);
            runtimeState.GetState(recoveredPlugin.Id).Should().Be(PluginLifecycleState.Enabled);
            healthMonitor.Verify(x => x.RecordCircuitBreakerRecovery(recoveredPlugin.Id), Times.Once);
            healthMonitor.Verify(x => x.RecordSuccess(recoveredPlugin.Id), Times.Once);
            usageTracker.Verify(x => x.RecordExecution(recoveredPlugin.Id, true, It.IsAny<long>(), context.TargetProcessName), Times.Once);
        }

        [Fact]
        public async Task SetPluginStateAsync_ShouldApplyLifecycleTransitionsThroughKernelFacade()
        {
            var services = new Mock<IServiceProvider>();
            var config = new ProfilesConfig();
            config.Plugins["runtime.plugin"] = new PluginProfile { Enabled = true };

            var configService = new Mock<IConfigService>();
            configService.Setup(x => x.Current).Returns(config);

            var catalog = new PluginCatalog();
            var runtimeState = new PluginRuntimeStateStore();
            var descriptor = CreateDescriptor("runtime.plugin", PluginTier.Extension, canDisable: true, typeof(RuntimeLifecyclePlugin));
            catalog.RegisterDescriptors(new[] { descriptor });

            var plugin = new RuntimeLifecyclePlugin();
            runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);

            var kernel = new PluginRuntimeKernel(
                services.Object,
                new FakeLoader(plugin, descriptor),
                catalog,
                runtimeState,
                new PluginExecutionPipeline(runtimeState, new PluginCircuitBreakerPolicy()),
                NullLogger<PluginRuntimeKernel>.Instance,
                configService.Object);

            await kernel.SetPluginStateAsync(plugin.Id, enabled: false);
            await kernel.SetPluginStateAsync(plugin.Id, enabled: true);

            plugin.DisableCount.Should().Be(1);
            plugin.EnableCount.Should().Be(1);
            runtimeState.GetState(plugin.Id).Should().Be(PluginLifecycleState.Enabled);
            configService.Verify(x => x.SaveAsync(config), Times.Exactly(2));
        }

        private static PluginDescriptor CreateDescriptor(string id, PluginTier tier, bool canDisable, Type implementationType)
        {
            return new PluginDescriptor
            {
                Id = id,
                DisplayName = id,
                Version = "1.0.0",
                Author = "Tests",
                Description = id,
                Icon = "T",
                CanDisable = canDisable,
                Tier = tier,
                ImplementationType = implementationType,
                Dependencies = Array.Empty<string>(),
                Metadata = new PluginMetadata
                {
                    Id = id,
                    Display = new DisplayInfo
                    {
                        Name = id,
                        Description = id,
                        IconKey = "T",
                        Category = "Tests",
                        Version = "1.0.0",
                        Author = "Tests",
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
                        SupportedActions = new List<string> { "run" },
                        Dependencies = new List<string>(),
                        Tier = tier,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }

        private sealed class FakeLoader : PluginLoader
        {
            private readonly IPulsarPlugin _plugin;
            private readonly PluginDescriptor _descriptor;

            public FakeLoader(IPulsarPlugin plugin, PluginDescriptor descriptor)
                : base(new Mock<IServiceProvider>().Object, string.Empty)
            {
                _plugin = plugin;
                _descriptor = descriptor;
            }

            public override List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
            {
                return new List<PluginDescriptor> { _descriptor };
            }

            public override IPulsarPlugin ActivatePlugin(PluginDescriptor descriptor)
            {
                return _plugin;
            }
        }

        private class RuntimeTestPlugin : IPulsarPlugin
        {
            public string Id => "runtime.plugin";
            public string DisplayName => "Runtime Plugin";
            public string Version => "1.0.0";
            public string Author => "Tests";
            public string Description => "Runtime test plugin";
            public string Icon => "T";
            public bool CanDisable => true;
            public bool ThrowOnExecute { get; set; }
            public Func<PluginResult>? ResultFactory { get; set; }

            public void Initialize(IServiceProvider services)
            {
            }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context)
            {
                if (ThrowOnExecute)
                {
                    throw new InvalidOperationException("boom");
                }

                return Task.FromResult(ResultFactory?.Invoke() ?? PluginResult.Ok("ok"));
            }
        }

        private sealed class RuntimeLifecyclePlugin : RuntimeTestPlugin, IPluginLifecycle
        {
            public int EnableCount { get; private set; }

            public int DisableCount { get; private set; }

            public Task OnEnableAsync()
            {
                EnableCount++;
                return Task.CompletedTask;
            }

            public Task OnDisableAsync()
            {
                DisableCount++;
                return Task.CompletedTask;
            }

            public Task OnUnloadAsync()
            {
                return Task.CompletedTask;
            }
        }
    }
}
