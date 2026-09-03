using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Tests.TestHelpers;
using FluentAssertions;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    public class PluginRuntimeHardeningTests
    {
        [Fact]
        public void PluginExecutionContext_NestedScopes_ShouldRestorePreviousScope()
        {
            using var outer = PluginExecutionContext.BeginScope("outer.plugin", "outer-action");
            var outerId = outer.ExecutionId;

            using (var inner = PluginExecutionContext.BeginScope("inner.plugin", "inner-action"))
            {
                PluginExecutionContext.Current.Should().BeSameAs(inner);
                PluginExecutionContext.Current!.ExecutionId.Should().NotBe(outerId);
            }

            PluginExecutionContext.Current.Should().BeSameAs(outer,
                "disposing an inner scope must restore the outer AsyncLocal scope");
            PluginExecutionContext.Current!.PluginId.Should().Be("outer.plugin");
        }

        [Fact]
        public void CircuitBreaker_FailuresOutsideWindow_ShouldNotTrip()
        {
            var policy = new PluginCircuitBreakerPolicy();
            var descriptor = CreateExtensionDescriptor("test.windowed.plugin");
            var exception = new InvalidOperationException("boom");

            policy.RecordFailure(descriptor, descriptor.Id, exception);
            policy.RecordFailure(descriptor, descriptor.Id, exception);

            SetFailureTimestamps(policy, descriptor.Id, DateTime.UtcNow - TimeSpan.FromMinutes(2));

            // The first new failure evicts both old timestamps, leaving count=1.
            policy.RecordFailure(descriptor, descriptor.Id, exception);
            var availability = policy.CheckAvailability(descriptor, descriptor.Id);
            availability.Allowed.Should().BeTrue(
                "failures older than the one-minute window must not contribute to the breaker");
        }

        [Fact]
        public void CircuitBreaker_ThreeFailuresInsideWindow_ShouldTrip()
        {
            var policy = new PluginCircuitBreakerPolicy();
            var descriptor = CreateExtensionDescriptor("test.windowed.trip.plugin");
            var exception = new InvalidOperationException("boom");

            policy.RecordFailure(descriptor, descriptor.Id, exception);
            policy.RecordFailure(descriptor, descriptor.Id, exception);
            policy.RecordFailure(descriptor, descriptor.Id, exception);

            var availability = policy.CheckAvailability(descriptor, descriptor.Id);
            availability.Allowed.Should().BeFalse(
                "three failures inside the window should open the circuit");
        }

        [Fact]
        public void PermissionEvaluator_ExternalPlugin_RequiresExplicitGrants()
        {
            var descriptor = CreateExtensionDescriptor(
                "test.permissions.plugin",
                isExternal: true,
                permissions: new[] { PluginPermissions.ClipboardRead, PluginPermissions.InputInject });
            var service = new PluginPermissionService();

            var denied = service.Evaluate(descriptor, Array.Empty<string>());
            denied.Granted.Should().BeFalse();
            denied.MissingPermissions.Should().BeEquivalentTo(
                PluginPermissions.ClipboardRead,
                PluginPermissions.InputInject);

            var granted = service.Evaluate(
                descriptor,
                new[] { PluginPermissions.ClipboardRead, PluginPermissions.InputInject });
            granted.Granted.Should().BeTrue();
            granted.MissingPermissions.Should().BeEmpty();
        }

        [Fact]
        public void PermissionEvaluator_UnknownPermission_IsDenied()
        {
            var descriptor = CreateExtensionDescriptor(
                "test.permissions.unknown",
                isExternal: true,
                permissions: new[] { "filesystem.exec" });
            var service = new PluginPermissionService();

            var evaluation = service.Evaluate(descriptor, new[] { "filesystem.exec" });

            evaluation.Granted.Should().BeFalse();
            evaluation.UnknownPermissions.Should().Contain("filesystem.exec");
        }

        [Fact]
        public async Task Pipeline_BlocksExternalPlugin_WhenPermissionsAreNotGranted()
        {
            var descriptor = CreateExtensionDescriptor(
                "test.permissions.pipeline",
                isExternal: true,
                permissions: new[] { PluginPermissions.WindowFocus });
            var plugin = new PermissionTestPlugin(descriptor.Id);
            var state = new PluginRuntimeStateStore();
            state.SetPlugin(plugin, PluginLifecycleState.Enabled);
            var pipeline = new PluginExecutionPipeline(state, new PluginCircuitBreakerPolicy());

            var outcome = await pipeline.ExecuteAsync(new PluginExecutionRequest
            {
                Descriptor = descriptor,
                Action = "test",
                Args = new Dictionary<string, string>(),
                Context = PulsarContextFactory.CreateTestContext(),
                GrantedPermissions = Array.Empty<string>(),
                IsEnabled = () => true,
                ActivateAsync = () => Task.FromResult<IPulsarPlugin?>(plugin),
                CancellationToken = CancellationToken.None
            });

            outcome.Kind.Should().Be(PluginExecutionOutcomeKind.Blocked);
            outcome.Result.ErrorCode.Should().Be(PluginErrorCode.AccessDenied);
            plugin.ExecutionCount.Should().Be(0);
        }

        [Fact]
        public async Task Kernel_GrantPermissionsAsync_PersistsApprovedPermissions()
        {
            var descriptor = CreateExtensionDescriptor(
                "test.permissions.grant",
                isExternal: true,
                permissions: new[] { PluginPermissions.ClipboardRead });
            var catalog = new PluginCatalog();
            catalog.RegisterDescriptors(new[] { descriptor });

            var state = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(state, new PluginCircuitBreakerPolicy());
            var loader = new PluginLoader(Mock.Of<IServiceProvider>(), "unused");

            var config = new Pulsar.Models.ProfilesConfig();
            var configService = new Mock<Pulsar.Services.Interfaces.IConfigService>();
            configService.Setup(x => x.GetSnapshot()).Returns(config);
            configService.Setup(x => x.LoadSnapshotAsync(It.IsAny<bool>())).ReturnsAsync(config);
            configService.Setup(x => x.SaveAsync(It.IsAny<Pulsar.Models.ProfilesConfig>(), It.IsAny<long?>()))
                .Returns(Task.CompletedTask);

            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(),
                loader,
                catalog,
                state,
                pipeline,
                NullLogger<PluginRuntimeKernel>.Instance,
                configService.Object);

            await kernel.GrantPermissionsAsync(descriptor.Id, new[] { PluginPermissions.ClipboardRead });

            configService.Verify(x => x.SaveAsync(It.IsAny<Pulsar.Models.ProfilesConfig>(), It.IsAny<long?>()), Times.Once);
        }

        [Fact]
        public async Task Kernel_RefreshDiscoveryAsync_RegistersRuntimeInstalledPlugin_AndEnablesGrant()
        {
            // Simulates the ZIP-install flow: a package lands in the plugin
            // directory while the app is running, so the startup discovery pass
            // has already happened. RefreshDiscoveryAsync must surface the new
            // descriptor, otherwise the install-time permission grant is
            // rejected as "unknown plugin" (Profiles.json stays empty).
            var descriptor = CreateExtensionDescriptor(
                "test.runtimeinstall.plugin",
                isExternal: true,
                permissions: new[] { PluginPermissions.ClipboardRead });
            var loader = new StubPluginLoader(new[] { descriptor });

            var catalog = new PluginCatalog();
            var state = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(state, new PluginCircuitBreakerPolicy());

            var config = new Pulsar.Models.ProfilesConfig();
            var configService = new Mock<Pulsar.Services.Interfaces.IConfigService>();
            configService.Setup(x => x.GetSnapshot()).Returns(config);
            configService.Setup(x => x.LoadSnapshotAsync(It.IsAny<bool>())).ReturnsAsync(config);
            configService.Setup(x => x.SaveAsync(It.IsAny<Pulsar.Models.ProfilesConfig>(), It.IsAny<long?>()))
                .Returns(Task.CompletedTask);

            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(),
                loader,
                catalog,
                state,
                pipeline,
                NullLogger<PluginRuntimeKernel>.Instance,
                configService.Object);

            kernel.GetDescriptor(descriptor.Id).Should().BeNull(
                "a runtime-installed plugin is invisible until discovery refresh");

            await kernel.RefreshDiscoveryAsync();

            kernel.GetDescriptor(descriptor.Id).Should().NotBeNull();
            kernel.GetAllPluginDescriptors().Should().Contain(d => d.Id == descriptor.Id);

            // Regression: the grant that used to fail right after install.
            await kernel.GrantPermissionsAsync(descriptor.Id, new[] { PluginPermissions.ClipboardRead });
            configService.Verify(x => x.SaveAsync(It.IsAny<Pulsar.Models.ProfilesConfig>(), It.IsAny<long?>()), Times.Once);
        }

        [Fact]
        public async Task Kernel_DeactivatePluginAsync_ReleasesStateCatalogAndRendererRegistrations()
        {
            // Runtime uninstall requires a full teardown: lifecycle hook runs,
            // state + catalog entries dropped, renderer contributions revoked.
            // Only then can the plugin's assembly context be unloaded (freeing
            // the OS file locks) and its directory deleted while running.
            var descriptor = CreateExtensionDescriptor("test.deactivate.plugin", isExternal: true);
            var plugin = new LifecycleTrackingPlugin(descriptor.Id);

            var state = new PluginRuntimeStateStore();
            state.SetPlugin(plugin, PluginLifecycleState.Enabled);

            var catalog = new PluginCatalog();
            catalog.RegisterDescriptors(new[] { descriptor });

            var pipeline = new PluginExecutionPipeline(state, new PluginCircuitBreakerPolicy());
            var loader = new PluginLoader(Mock.Of<IServiceProvider>(), "unused");

            var rendererRegistry = new Pulsar.Core.Rendering.RadialRendererRegistry(
                new[] { Pulsar.Core.Rendering.DefaultRadialRenderer.RendererId, "ClassicRing", "Glassmorphism" },
                _ => true);
            rendererRegistry.Register(new StubDeactivateRenderer("Neon"), descriptor.Id).Should().BeTrue();

            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(),
                loader,
                catalog,
                state,
                pipeline,
                NullLogger<PluginRuntimeKernel>.Instance,
                rendererRegistry: rendererRegistry);

            await kernel.DeactivatePluginAsync(descriptor.Id);

            plugin.UnloadCount.Should().Be(1, "OnUnloadAsync must run exactly once during deactivation");
            kernel.GetDescriptor(descriptor.Id).Should().BeNull("the catalog entry must be dropped");
            state.TryGetPlugin(descriptor.Id, out _).Should().BeFalse("the runtime state entry must be dropped");
            rendererRegistry.TryGet("Neon", out _).Should().BeFalse("renderer contributions must be revoked");
        }

        [Fact]
        public void PluginRuntimeStateStore_ShouldRejectInvalidTransitions()
        {
            var store = new PluginRuntimeStateStore();

            var invalidTransition = () => store.Transition("missing.plugin", PluginLifecycleState.Running);

            invalidTransition.Should().Throw<InvalidOperationException>(
                "Unloaded -> Running is not a legal transition");
        }

        private static void SetFailureTimestamps(
            PluginCircuitBreakerPolicy policy,
            string pluginId,
            DateTime timestamp)
        {
            var field = typeof(PluginCircuitBreakerPolicy).GetField(
                "_recentFailures",
                BindingFlags.Instance | BindingFlags.NonPublic);
            field.Should().NotBeNull("the test must stay aligned with the policy implementation");

            var failures = field!.GetValue(policy) as ConcurrentDictionary<string, List<DateTime>>;
            failures.Should().NotBeNull();
            failures!.TryGetValue(pluginId, out var timestamps).Should().BeTrue();

            lock (timestamps!)
            {
                for (var i = 0; i < timestamps.Count; i++)
                {
                    timestamps[i] = timestamp;
                }
            }
        }

        private sealed class StubPluginLoader : PluginLoader
        {
            private readonly List<PluginDescriptor> _descriptors;

            public StubPluginLoader(IEnumerable<PluginDescriptor> descriptors)
                : base(Mock.Of<IServiceProvider>(), "unused")
            {
                _descriptors = descriptors.ToList();
            }

            public override List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
                => _descriptors.ToList();
        }

        private sealed class LifecycleTrackingPlugin : IPulsarPlugin, IPluginLifecycle
        {
            private readonly string _id;

            public LifecycleTrackingPlugin(string id)
            {
                _id = id;
            }

            public int UnloadCount { get; private set; }

            public string Id => _id;
            public string DisplayName => "Lifecycle Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Lifecycle test plugin";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services) { }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
                => Task.FromResult(PluginResult.Ok());

            public Task OnEnableAsync() => Task.CompletedTask;

            public Task OnDisableAsync() => Task.CompletedTask;

            public Task OnUnloadAsync()
            {
                UnloadCount++;
                return Task.CompletedTask;
            }
        }

        private sealed class StubDeactivateRenderer : Pulsar.Core.Rendering.IRadialRenderer
        {
            public StubDeactivateRenderer(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public void Initialize(Pulsar.Core.Rendering.IRadialThemeTokens tokens) { }

            public Pulsar.Core.Rendering.IRadialSlotHighlight ResolveHighlight(bool isActive)
                => Pulsar.Core.Rendering.RadialSlotHighlight.None;

            public void RenderDecorations(System.Windows.Controls.Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius) { }
        }

        private static PluginDescriptor CreateExtensionDescriptor(
            string pluginId,
            bool isExternal = false,
            IReadOnlyList<string>? permissions = null)
        {
            return new PluginDescriptor
            {
                Id = pluginId,
                DisplayName = pluginId,
                Version = "1.0.0",
                Author = "Test",
                Description = "Test descriptor",
                Icon = "T",
                CanDisable = true,
                Tier = PluginTier.Extension,
                IsExternal = isExternal,
                Permissions = permissions ?? Array.Empty<string>(),
                ImplementationType = typeof(PluginRuntimeHardeningTests),
                Dependencies = new List<string>(),
                Metadata = new PluginMetadata
                {
                    Id = pluginId,
                    Display = new DisplayInfo
                    {
                        Name = pluginId,
                        Description = "Test descriptor",
                        IconKey = "T",
                        Category = "Tests",
                        Version = "1.0.0",
                        Author = "Test",
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
                        SupportedActions = new List<string>(),
                        Dependencies = new List<string>(),
                        Tier = PluginTier.Extension,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }

        private sealed class PermissionTestPlugin : IPulsarPlugin
        {
            private readonly string _id;
            private int _executionCount;

            public PermissionTestPlugin(string id)
            {
                _id = id;
            }

            public int ExecutionCount => _executionCount;

            public string Id => _id;
            public string DisplayName => "Permission Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Permission test plugin";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services) { }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
            {
                Interlocked.Increment(ref _executionCount);
                return Task.FromResult(PluginResult.Ok());
            }
        }

    }
}
