using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Rendering;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class StyleRendererFactoryTests
    {
        private sealed class StubRenderer : IRadialRenderer
        {
            public StubRenderer(string id)
            {
                Id = id;
            }

            public string Id { get; }

            public void Initialize(IRadialThemeTokens tokens)
            {
            }

            public IRadialSlotHighlight ResolveHighlight(bool isActive)
            {
                return RadialSlotHighlight.None;
            }

            public void RenderDecorations(System.Windows.Controls.Canvas canvas, double cx, double cy, double wheelRadius, double coreRadius)
            {
            }
        }

        private static StyleRendererFactory CreateFactory()
        {
            return new StyleRendererFactory(new IRadialRenderer[]
            {
                new DefaultRadialRenderer(),
                new StubRenderer("ClassicRing"),
                new StubRenderer("Glassmorphism")
            });
        }

        [Fact]
        public void Create_RegisteredId_ShouldReturnThatRenderer()
        {
            var factory = CreateFactory();

            factory.Create("ClassicRing").Should().BeOfType<StubRenderer>();
            factory.Create("Glassmorphism").Should().BeOfType<StubRenderer>();
        }

        [Fact]
        public void Create_RegisteredId_IsCaseInsensitive()
        {
            var factory = CreateFactory();

            factory.Create("classicring").Should().BeOfType<StubRenderer>();
            factory.Create("CLASSICRING").Should().BeOfType<StubRenderer>();
        }

        [Fact]
        public void Create_UnknownId_ShouldFallBackToDefault()
        {
            var factory = CreateFactory();

            factory.Create("DoesNotExist").Should().BeOfType<DefaultRadialRenderer>();
        }

        [Fact]
        public void Create_NullOrEmptyId_ShouldFallBackToDefault()
        {
            var factory = CreateFactory();

            factory.Create(null).Should().BeOfType<DefaultRadialRenderer>();
            factory.Create(string.Empty).Should().BeOfType<DefaultRadialRenderer>();
            factory.Create("   ").Should().BeOfType<DefaultRadialRenderer>();
        }

        [Fact]
        public void Create_DefaultConfigValue_ShouldResolveDefaultRenderer()
        {
            // "Default" is the persisted default value of ProfileSettings.RadialRenderer;
            // the factory must resolve it to the Default renderer exactly as before the
            // factory existed (visual output unchanged).
            var factory = CreateFactory();

            factory.Create(DefaultRadialRenderer.RendererId).Should().BeOfType<DefaultRadialRenderer>();
        }

        [Fact]
        public void Ctor_WithoutDefaultRenderer_ShouldThrow()
        {
            var act = () => new StyleRendererFactory(new IRadialRenderer[]
            {
                new StubRenderer("ClassicRing")
            });

            act.Should().Throw<InvalidOperationException>();
        }

        [Fact]
        public void DiRegistration_ShouldResolveFactoryWithDefaultPresent()
        {
            // Mirrors App.xaml.cs ConfigureServices: every renderer is a singleton,
            // Default is registered LAST so GetService<IRadialRenderer>() falls back to
            // Default, and the factory resolves with all three registered.
            var services = new ServiceCollection();
            services.AddSingleton<IRadialRenderer>(_ => new StubRenderer("ClassicRing"));
            services.AddSingleton<IRadialRenderer>(_ => new StubRenderer("Glassmorphism"));
            services.AddSingleton<IRadialRenderer, DefaultRadialRenderer>();
            services.AddSingleton<StyleRendererFactory>();

            using var provider = services.BuildServiceProvider();

            var factory = provider.GetService<StyleRendererFactory>();
            factory.Should().NotBeNull();
            factory!.Create("ClassicRing").Should().BeOfType<StubRenderer>();
            factory.Create("Glassmorphism").Should().BeOfType<StubRenderer>();
            factory.Create(DefaultRadialRenderer.RendererId).Should().BeOfType<DefaultRadialRenderer>();

            // The legacy single-instance lookup must resolve to the Default fallback.
            provider.GetService<IRadialRenderer>().Should().BeOfType<DefaultRadialRenderer>();
        }
    }
}
