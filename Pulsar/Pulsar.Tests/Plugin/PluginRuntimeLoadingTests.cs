using System;
using System.Collections.Generic;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    public class PluginRuntimeLoadingTests
    {
        [Fact]
        public async Task DiscoverDeferredAsync_ShouldRegisterDescriptorsWithoutActivatingPlugins()
        {
            var catalog = new PluginCatalog();
            var runtimeState = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(runtimeState, new PluginCircuitBreakerPolicy());
            var services = new ServiceCollection().BuildServiceProvider();
            var loader = new FakePluginLoader(services, BuildDescriptors())
            {
                ActivationFactory = _ => new DeferredActivationPlugin()
            };
            var kernel = new PluginRuntimeKernel(services, loader, catalog, runtimeState, pipeline);

            await kernel.DiscoverDeferredAsync();

            kernel.GetDescriptor("test.deferred").Should().NotBeNull();
            kernel.GetPlugin("test.deferred").Should().BeNull();
            loader.ActivationCount.Should().Be(0);
        }

        [Fact]
        public async Task ExecuteAsync_ShouldActivateDeferredPluginOnFirstUse_AndReuseInstance()
        {
            var catalog = new PluginCatalog();
            var runtimeState = new PluginRuntimeStateStore();
            var pipeline = new PluginExecutionPipeline(runtimeState, new PluginCircuitBreakerPolicy());
            var services = new ServiceCollection();
            var config = new ProfilesConfig();
            config.Plugins["test.deferred"] = new PluginProfile { Enabled = true };

            var configService = new Mock<IConfigService>();
            configService.Setup(x => x.GetSnapshot()).Returns(config);
            services.AddSingleton(configService.Object);

            var provider = services.BuildServiceProvider();
            var loader = new FakePluginLoader(provider, BuildDescriptors())
            {
                ActivationFactory = _ => new DeferredActivationPlugin()
            };
            var kernel = new PluginRuntimeKernel(provider, loader, catalog, runtimeState, pipeline, NullLogger<PluginRuntimeKernel>.Instance, configService.Object);
            await kernel.DiscoverDeferredAsync();

            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            var result1 = await kernel.ExecuteAsync("test.deferred", "run", args, context);
            var result2 = await kernel.ExecuteAsync("test.deferred", "run", args, context);

            result1.Success.Should().BeTrue();
            result2.Success.Should().BeTrue();
            loader.ActivationCount.Should().Be(1);
            kernel.GetPlugin("test.deferred").Should().NotBeNull();
        }

        [Fact]
        public void DiscoverDescriptors_ShouldReuseCachedDescriptorResults()
        {
            var services = new ServiceCollection().BuildServiceProvider();
            var loader = new CountingPluginLoader(services, BuildDescriptors());

            var first = loader.DiscoverDescriptors(includeCore: false, includeExtensions: true, analyzeDependencies: true);
            var second = loader.DiscoverDescriptors(includeCore: false, includeExtensions: true, analyzeDependencies: true);

            first.Should().HaveCount(1);
            second.Should().HaveCount(1);
            loader.DiscoverCount.Should().Be(1);
        }

        [Fact]
        public void ManifestVersionCompatibility_ShouldRejectTooNewMinimumVersion()
        {
            var manifest = new PluginManifest
            {
                Id = "test.version",
                MinPulsarVersion = "999.0.0"
            };

            var compatible = PluginLoader.IsManifestVersionCompatible(manifest, out var reason);

            compatible.Should().BeFalse();
            reason.Should().Contain("requires Pulsar");
        }

        [Fact]
        public void ManifestVersionCompatibility_ShouldAcceptCurrentHostVersion()
        {
            var manifest = new PluginManifest
            {
                Id = "test.version",
                MinPulsarVersion = "1.0.0",
                MaxPulsarVersion = "2.0.0"
            };

            var compatible = PluginLoader.IsManifestVersionCompatible(manifest, out var reason);

            compatible.Should().BeTrue(reason);
        }

        [Fact]
        public void ManifestVersionCompatibility_ShouldAcceptSemanticVersionSuffix()
        {
            var manifest = new PluginManifest
            {
                Id = "test.version",
                MinPulsarVersion = "1.0.0-beta.1",
                MaxPulsarVersion = "2.0.0+build.7"
            };

            var compatible = PluginLoader.IsManifestVersionCompatible(manifest, out var reason);

            compatible.Should().BeTrue(reason);
        }

        [Fact]
        public void TryReadExternalManifest_ShouldPreferPluginManifestFile()
        {
            var tempDir = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(tempDir);
            try
            {
                File.WriteAllText(Path.Combine(tempDir, "manifest.json"), "{\"id\":\"test.legacy\"}");
                File.WriteAllText(Path.Combine(tempDir, "plugin.manifest.json"), "{\"id\":\"test.preferred\"}");

                var manifest = PluginLoader.TryReadExternalManifest(tempDir);

                manifest.Should().NotBeNull();
                manifest!.Id.Should().Be("test.preferred");
            }
            finally
            {
                try { Directory.Delete(tempDir, recursive: true); } catch { /* ignore */ }
            }
        }

        [Fact]
        public void CreateExternalDescriptor_ShouldNotInstantiatePluginType()
        {
            var manifest = new PluginManifest
            {
                Id = "test.external.descriptor",
                DisplayName = "External Descriptor",
                Version = "1.0.0",
                EntryPoint = typeof(ThrowingConstructorPlugin).FullName!
            };

            var descriptor = PluginLoader.CreateExternalDescriptor(typeof(ThrowingConstructorPlugin), manifest);

            descriptor.Should().NotBeNull();
            descriptor.IsExternal.Should().BeTrue();
            descriptor.Id.Should().Be(manifest.Id);
            descriptor.Permissions.Should().BeSameAs(manifest.Permissions);
        }

        [Fact]
        public void CreateExternalDescriptor_CoreTier_ShouldBeRejected()
        {
            var manifest = new PluginManifest
            {
                Id = "test.external.core",
                Tier = PluginTier.Core
            };

            var action = () => PluginLoader.CreateExternalDescriptor(typeof(ThrowingConstructorPlugin), manifest);

            action.Should().Throw<PluginInstantiationException>();
        }

        private static List<PluginDescriptor> BuildDescriptors()
        {
            var metadata = new Core.Plugin.Metadata.PluginMetadata
            {
                Id = "test.deferred",
                Display = new Core.Plugin.Metadata.DisplayInfo
                {
                    Name = "Deferred Test Plugin",
                    Description = "Deferred activation test plugin",
                    IconKey = "T",
                    Category = "Tests",
                    Version = "1.0.0",
                    Author = "Tests",
                    License = "MIT"
                },
                Schema = null,
                UI = new Core.Plugin.Metadata.UIHints
                {
                    Badge = "Plugin",
                    AccentColor = "#4A90E2",
                    ShowInQuickAccess = true,
                    SortOrder = 1
                },
                Capabilities = new Core.Plugin.Metadata.PluginCapabilities
                {
                    SupportedActions = new List<string> { "run" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = true,
                    Tier = PluginTier.Extension,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, Core.Plugin.Metadata.SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
            };

            return new List<PluginDescriptor>
            {
                new()
                {
                    Id = "test.deferred",
                    DisplayName = "Deferred Test Plugin",
                    Version = "1.0.0",
                    Author = "Tests",
                    Description = "Deferred activation test plugin",
                    Icon = "T",
                    CanDisable = true,
                    Tier = PluginTier.Extension,
                    ImplementationType = typeof(DeferredActivationPlugin),
                    Dependencies = new List<string>(),
                    Metadata = metadata,
                    IsConfigurable = false
                }
            };
        }

        private sealed class FakePluginLoader : PluginLoader
        {
            private readonly List<PluginDescriptor> _descriptors;
            private readonly IServiceProvider _services;

            public FakePluginLoader(IServiceProvider services, List<PluginDescriptor> descriptors)
                : base(services, string.Empty)
            {
                _services = services;
                _descriptors = descriptors;
            }

            public int ActivationCount { get; private set; }

            public Func<PluginDescriptor, IPulsarPlugin>? ActivationFactory { get; init; }

            public override List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
            {
                return _descriptors;
            }

            public override IPulsarPlugin ActivatePlugin(PluginDescriptor descriptor)
            {
                ActivationCount++;
                var plugin = ActivationFactory?.Invoke(descriptor) ?? new DeferredActivationPlugin();
                plugin.Initialize(_services);
                return plugin;
            }

        }

        private sealed class CountingPluginLoader : PluginLoader
        {
            private readonly List<PluginDescriptor> _descriptors;

            public CountingPluginLoader(IServiceProvider services, List<PluginDescriptor> descriptors)
                : base(services, string.Empty)
            {
                _descriptors = descriptors;
            }

            public override List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
            {
                return base.DiscoverDescriptors(includeCore, includeExtensions, analyzeDependencies);
            }

            protected override void DiscoverBuiltinDescriptors(List<PluginDescriptor> descriptors, bool includeCore, bool includeExtensions)
            {
                DiscoverCount++;
                descriptors.AddRange(_descriptors);
            }

            public int DiscoverCount { get; private set; }
        }

        private sealed class ThrowingConstructorPlugin : IPulsarPlugin
        {
            public ThrowingConstructorPlugin()
            {
                throw new InvalidOperationException("External plugin constructors must not run during discovery.");
            }

            public string Id => "test.external.descriptor";
            public string DisplayName => "External Descriptor";
            public string Version => "1.0.0";
            public string Author => "Tests";
            public string Description => "Throwing constructor test plugin";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services) { }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PluginResult.Ok());
            }
        }

        private sealed class DeferredActivationPlugin : IPulsarPlugin
        {
            public string Id => "test.deferred";
            public string DisplayName => "Deferred Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Tests";
            public string Description => "Deferred activation plugin";
            public string Icon => "T";
            public bool CanDisable => true;

            public void Initialize(IServiceProvider services)
            {
            }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context, CancellationToken cancellationToken = default)
            {
                return Task.FromResult(PluginResult.Ok("Activated"));
            }
        }
    }
}
