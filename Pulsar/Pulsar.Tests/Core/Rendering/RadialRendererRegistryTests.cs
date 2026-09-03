using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Core.Rendering;
using Xunit;

namespace Pulsar.Tests.Rendering
{
    public class RadialRendererRegistryTests
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

        private static readonly string[] ReservedIds =
        {
            DefaultRadialRenderer.RendererId,
            "ClassicRing",
            "Glassmorphism"
        };

        private static RadialRendererRegistry CreateRegistry(Func<string?, bool>? canRegisterOwner = null)
        {
            return new RadialRendererRegistry(ReservedIds, canRegisterOwner);
        }

        [Fact]
        public void Register_ValidContribution_ShouldSucceedAndRaiseChanged()
        {
            var registry = CreateRegistry(_ => true);
            var raised = 0;
            registry.Changed += (_, _) => raised++;

            var ok = registry.Register(new StubRenderer("Neon"), "plugin.a");

            ok.Should().BeTrue();
            raised.Should().Be(1);
            registry.TryGet("Neon", out var renderer).Should().BeTrue();
            renderer.Should().BeOfType<StubRenderer>();
            registry.Registrations.Should().ContainSingle(r => r.OwnerId == "plugin.a");
        }

        [Fact]
        public void Register_DuplicateId_ShouldBeRejected()
        {
            var registry = CreateRegistry(_ => true);
            registry.Register(new StubRenderer("Neon"), "plugin.a").Should().BeTrue();

            var second = registry.Register(new StubRenderer("Neon"), "plugin.b");

            second.Should().BeFalse();
            registry.Registrations.Should().ContainSingle();
        }

        [Fact]
        public void Register_ReservedBuiltInId_ShouldBeRejectedRegardlessOfCase()
        {
            var registry = CreateRegistry(_ => true);

            registry.Register(new StubRenderer(DefaultRadialRenderer.RendererId), "plugin.a").Should().BeFalse();
            registry.Register(new StubRenderer("classicring"), "plugin.a").Should().BeFalse();
            registry.Register(new StubRenderer("GLASSMORPHISM"), "plugin.a").Should().BeFalse();

            registry.Registrations.Should().BeEmpty();
        }

        [Fact]
        public void Register_OwnerWithoutPermission_ShouldBeRejectedWithoutThrowing()
        {
            var registry = CreateRegistry(ownerId => ownerId == "plugin.granted");

            registry.Register(new StubRenderer("Neon"), "plugin.denied").Should().BeFalse();
            registry.Register(new StubRenderer("Neon"), "plugin.granted").Should().BeTrue();
            registry.Register(new StubRenderer("Aurora"), null!).Should().BeFalse();
        }

        [Fact]
        public void Register_NullRendererOrEmptyIds_ShouldBeRejected()
        {
            var registry = CreateRegistry(_ => true);

            registry.Register(null!, "plugin.a").Should().BeFalse();
            registry.Register(new StubRenderer(""), "plugin.a").Should().BeFalse();
            registry.Register(new StubRenderer("Neon"), "").Should().BeFalse();
            registry.Register(new StubRenderer("Neon"), "  ").Should().BeFalse();

            registry.Registrations.Should().BeEmpty();
        }

        [Fact]
        public void Unregister_ShouldOnlyRemoveMatchingOwner()
        {
            var registry = CreateRegistry(_ => true);
            registry.Register(new StubRenderer("Neon"), "plugin.a").Should().BeTrue();

            registry.Unregister("Neon", "plugin.b").Should().BeFalse();
            registry.TryGet("Neon", out _).Should().BeTrue();

            registry.Unregister("Neon", "plugin.a").Should().BeTrue();
            registry.TryGet("Neon", out _).Should().BeFalse();
        }

        [Fact]
        public void UnregisterOwner_ShouldRemoveAllOwnedAndBeIdempotent()
        {
            var registry = CreateRegistry(_ => true);
            registry.Register(new StubRenderer("Neon"), "plugin.a").Should().BeTrue();
            registry.Register(new StubRenderer("Aurora"), "plugin.a").Should().BeTrue();
            registry.Register(new StubRenderer("Solar"), "plugin.b").Should().BeTrue();
            var changes = 0;
            registry.Changed += (_, _) => changes++;

            registry.UnregisterOwner("plugin.a").Should().Be(2);
            registry.UnregisterOwner("plugin.a").Should().Be(0);
            changes.Should().Be(1);

            registry.TryGet("Neon", out _).Should().BeFalse();
            registry.TryGet("Aurora", out _).Should().BeFalse();
            registry.TryGet("Solar", out _).Should().BeTrue();
        }

        [Fact]
        public void TryGet_LookupIsCaseInsensitive()
        {
            var registry = CreateRegistry(_ => true);
            registry.Register(new StubRenderer("Neon"), "plugin.a").Should().BeTrue();

            registry.TryGet("neon", out var renderer).Should().BeTrue();
            renderer.Should().BeOfType<StubRenderer>();
        }
    }
}
