using System;
using FluentAssertions;
using Moq;
using Pulsar.Core.Rendering;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// StyleRendererResolver is the single seam for radial-renderer resolution: it
    /// reads the configured id from the config snapshot, resolves it through the
    /// StyleRendererFactory (plugin registry first, then built-ins, Default fallback)
    /// and caches the result against the config revision, invalidating on
    /// IRadialRendererRegistry.Changed so plugin contributions pick up without a save.
    /// </summary>
    public class StyleRendererResolverTests
    {
        [Fact]
        public void Resolve_ConfiguredId_ShouldReturnThatRenderer()
        {
            var classic = new Mock<IRadialRenderer>();
            classic.SetupGet(r => r.Id).Returns("ClassicRing");
            var resolver = CreateResolver(
                factoryRenderers: new IRadialRenderer[] { new DefaultRadialRenderer(), classic.Object },
                config: new ProfilesConfig { Settings = new ProfileSettings { RadialRenderer = "ClassicRing" } });

            resolver.Resolve().Should().BeSameAs(classic.Object);
        }

        [Fact]
        public void Resolve_UnknownId_ShouldFallBackToDefault()
        {
            var defaultRenderer = new Mock<IRadialRenderer>();
            defaultRenderer.SetupGet(r => r.Id).Returns(DefaultRadialRenderer.RendererId);
            var resolver = CreateResolver(
                factoryRenderers: new IRadialRenderer[] { defaultRenderer.Object },
                config: new ProfilesConfig { Settings = new ProfileSettings { RadialRenderer = "DoesNotExist" } });

            resolver.Resolve().Should().BeSameAs(defaultRenderer.Object);
        }

        [Fact]
        public void Resolve_ConfigRevisionChanged_ShouldResolveNewRenderer()
        {
            // A saved renderer-selection bump = revision bump + new snapshot; the
            // resolver must not keep serving the stale renderer.
            var classic = new Mock<IRadialRenderer>();
            classic.SetupGet(r => r.Id).Returns("ClassicRing");
            var glass = new Mock<IRadialRenderer>();
            glass.SetupGet(r => r.Id).Returns("Glassmorphism");

            var snapshot = new ProfilesConfig
            {
                Settings = new ProfileSettings { RadialRenderer = "ClassicRing" }
            };
            long revision = 1;
            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(snapshot);
            config.SetupGet(c => c.CurrentRevision).Returns(() => revision);

            var resolver = CreateResolver(
                factoryRenderers: new IRadialRenderer[] { new DefaultRadialRenderer(), classic.Object, glass.Object },
                config: null,
                configMock: config);

            resolver.Resolve().Should().BeSameAs(classic.Object);

            snapshot.Settings.RadialRenderer = "Glassmorphism";
            revision++;
            resolver.Resolve().Should().BeSameAs(glass.Object);
        }

        [Fact]
        public void Resolve_RegistryChanged_ShouldInvalidateCache_WithoutRevisionBump()
        {
            // A renderer id can be saved to config before its owning plugin registers
            // (e.g. plugin re-installed). While unregistered the id falls back to
            // Default; registering the plugin must make the configured id resolve to
            // it even though the config revision never changed.
            var defaultRenderer = new Mock<IRadialRenderer>();
            defaultRenderer.SetupGet(r => r.Id).Returns(DefaultRadialRenderer.RendererId);
            var plugin = new Mock<IRadialRenderer>();
            plugin.SetupGet(r => r.Id).Returns("PluginRing");

            var registry = new RadialRendererRegistry(
                reservedIds: new[] { "ClassicRing", "Glassmorphism", DefaultRadialRenderer.RendererId });

            var config = new Mock<IConfigService>();
            config.Setup(c => c.GetSnapshot()).Returns(new ProfilesConfig
            {
                Settings = new ProfileSettings { RadialRenderer = "PluginRing" }
            });
            config.SetupGet(c => c.CurrentRevision).Returns(1);

            var resolver = new StyleRendererResolver(
                new StyleRendererFactory(new IRadialRenderer[] { defaultRenderer.Object }, registry),
                config.Object,
                registry);

            resolver.Resolve().Should().BeSameAs(defaultRenderer.Object);

            registry.Register(plugin.Object, "owner.test").Should().BeTrue();
            resolver.Resolve().Should().BeSameAs(plugin.Object);
        }

        private static StyleRendererResolver CreateResolver(
            IRadialRenderer[] factoryRenderers,
            ProfilesConfig? config,
            Mock<IConfigService>? configMock = null)
        {
            var factory = new StyleRendererFactory(factoryRenderers);
            if (configMock != null)
            {
                return new StyleRendererResolver(factory, configMock.Object);
            }

            var mock = new Mock<IConfigService>();
            mock.Setup(c => c.GetSnapshot()).Returns(config!);
            mock.SetupGet(c => c.CurrentRevision).Returns(0);
            return new StyleRendererResolver(factory, mock.Object);
        }
    }
}
