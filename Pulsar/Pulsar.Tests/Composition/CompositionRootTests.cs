// [Path]: Pulsar/Pulsar.Tests/Composition/CompositionRootTests.cs
//
// Guards the composition root (App.RegisterApplicationServices).
//
// Before this file the service graph existed only as an untestable inline block inside
// OnStartup, so nothing could catch a service registered twice. Microsoft DI keeps the
// LAST registration for a given service type and silently discards the earlier one, so a
// duplicate is a dead registration that no test could see. Two such duplicates shipped
// unnoticed (IDialogService, Tutorial StartupCoordinator).
//
// The suite deliberately does NOT assert "one registration per service type": some
// services are registered once per implementation on purpose (IRadialRenderer x3,
// ISubMenuStrategy x2) and are resolved as IEnumerable<T>. Those rows are pinned below so
// a future "cleanup" cannot silently drop renderers or strategies.

using System;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Debug;
using Pulsar.Core.Localization;
using Pulsar.Core.Rendering;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Strategies;
using Serilog.Core;
using Xunit;

namespace Pulsar.Tests.Composition
{
    public class CompositionRootTests
    {
        /// <summary>
        /// Builds the real graph exactly as startup does (minus the container). Debug
        /// options default to production — no <c>--ui-debug</c> — so the debug-only
        /// registrations stay out, matching a normal run.
        /// </summary>
        private static IServiceCollection BuildGraph()
        {
            var services = new ServiceCollection();
            App.RegisterApplicationServices(
                services,
                new LoggingLevelSwitch(),
                DebugModeOptions.FromArgs(Array.Empty<string>()));
            return services;
        }

        /// <summary>
        /// The framework's own registrations are out of scope: <c>AddLogging</c> pulls in
        /// <c>AddOptions</c>, which legitimately registers several <c>IConfigureOptions&lt;&gt;</c>
        /// rows — options configuration is a collection by design. This guard is about the
        /// app's own wiring.
        /// </summary>
        private static bool IsFrameworkService(Type serviceType)
            => serviceType.Namespace?.StartsWith("Microsoft.", StringComparison.Ordinal) == true
               || serviceType.Namespace?.StartsWith("System.", StringComparison.Ordinal) == true;

        [Fact]
        public void RegisteredImplementations_ShouldBeUniquePerServiceAndLifetime()
        {
            var duplicates = BuildGraph()
                .Where(d => d.ImplementationType != null && !IsFrameworkService(d.ServiceType))
                .GroupBy(d => (d.ServiceType, d.ImplementationType, d.Lifetime))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.ServiceType.Name} -> {g.Key.ImplementationType!.Name} ({g.Key.Lifetime}) x{g.Count()}")
                .ToList();

            duplicates.Should().BeEmpty(
                "registering the same (service, implementation, lifetime) twice silently keeps only the "
                + "last one; the earlier registration can never be resolved. Intentional multiple "
                + "implementations differ by implementation type and are allowed.");
        }

        [Fact]
        public void FactoryRegistrations_ShouldNotServeTheSameServiceTwice()
        {
            var duplicates = BuildGraph()
                .Where(d => d.ImplementationType == null && !IsFrameworkService(d.ServiceType))
                .GroupBy(d => (d.ServiceType, d.Lifetime))
                .Where(g => g.Count() > 1)
                .Select(g => $"{g.Key.ServiceType.Name} ({g.Key.Lifetime}) x{g.Count()}")
                .ToList();

            duplicates.Should().BeEmpty(
                "two factories for one service behave the same way as two implementations: the last wins.");
        }

        [Theory]
        [InlineData(typeof(IDialogService))]
        [InlineData(typeof(ILocalizationService))]
        [InlineData(typeof(IAppStartupCoordinator))]
        [InlineData(typeof(Pulsar.Features.Tutorial.Services.StartupCoordinator))]
        public void KeyServices_ShouldBeRegisteredExactlyOnce(Type serviceType)
        {
            BuildGraph().Count(d => d.ServiceType == serviceType)
                .Should().Be(1, $"{serviceType.Name} must stay resolvable — and only once");
        }

        [Theory]
        [InlineData(typeof(IRadialRenderer), 3)]
        [InlineData(typeof(ISubMenuStrategy), 2)]
        public void IntentionalMultipleImplementations_ShouldKeepTheirCount(Type serviceType, int expected)
        {
            BuildGraph().Count(d => d.ServiceType == serviceType)
                .Should().Be(expected,
                    "these are deliberate IEnumerable<T> registrations — de-duplicating them would silently "
                    + "drop renderers or sub-menu strategies");
        }

        [Fact]
        public void RadialRenderer_DefaultImplementation_ShouldBeRegisteredLast()
        {
            var renderers = BuildGraph()
                .Where(d => d.ServiceType == typeof(IRadialRenderer))
                .Select(d => d.ImplementationType)
                .ToList();

            renderers.Should().EndWith(
                new[] { typeof(DefaultRadialRenderer) },
                "the legacy GetService<IRadialRenderer>() fallback resolves the LAST registration, which "
                + "must stay the Default renderer (the ordering used to live only in a comment)");
        }

        [Fact]
        public void DebugFactories_ShouldBeFailClosed_OutsideUiDebug()
        {
            var graph = BuildGraph();
            var factory = graph.Single(d => d.ServiceType == typeof(Func<IDebugStatePublisher>))
                .ImplementationFactory!;
            using var provider = new ServiceCollection().BuildServiceProvider();

            // The registered factory returns the guarded Func; the throw lives inside it.
            var guarded = (Func<IDebugStatePublisher>)factory(provider);

            var act = () => guarded();

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*ui-debug*",
                    "the debug factories must throw rather than hand out a half-wired publisher in a normal run");
        }

        [Fact]
        public void Graph_ShouldBuild()
        {
            using var provider = BuildGraph().BuildServiceProvider();

            provider.Should().NotBeNull();
        }
    }
}
