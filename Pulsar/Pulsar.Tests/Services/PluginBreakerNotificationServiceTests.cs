// [Path]: Pulsar/Pulsar.Tests/Services/PluginBreakerNotificationServiceTests.cs

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// Covers ADR-013: PluginBreakerNotificationService observes breaker state
    /// transitions (Tripped / Recovered) and relays them to health telemetry and
    /// tray notifications. The breaker policy itself must stay free of these
    /// consumer dependencies.
    /// </summary>
    public class PluginBreakerNotificationServiceTests
    {
        [Fact]
        public void Tripped_RelaysHealthTelemetry_AndShowsLocalizedTrayNotification()
        {
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var trayService = new Mock<ITrayService>();
            var loc = CreateLocalization();
            var breaker = new PluginCircuitBreakerPolicy();
            var service = new PluginBreakerNotificationService(
                breaker, healthMonitor.Object, trayService.Object, loc.Object, NullLogger<PluginBreakerNotificationService>.Instance);

            var descriptor = CreateExtensionDescriptor("trip.plugin");
            for (var i = 0; i < 3; i++)
            {
                breaker.RecordFailure(descriptor, "trip.plugin", new InvalidOperationException("boom"));
            }

            healthMonitor.Verify(x => x.RecordCircuitBreakerTrip("trip.plugin"), Times.Once);
            trayService.Verify(
                x => x.ShowNotification(
                    "Plugin Auto-Disabled",
                    It.Is<string>(m => m.Contains("trip.plugin") && m.Contains("60")),
                    PulsarNotificationIcon.Error),
                Times.Once);
            service.Should().NotBeNull(); // keep the activated observer referenced
        }

        [Fact]
        public void Recovered_RelaysHealthTelemetry_ButDoesNotNotify()
        {
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var trayService = new Mock<ITrayService>();
            var loc = CreateLocalization();
            var breaker = new PluginCircuitBreakerPolicy();
            var service = new PluginBreakerNotificationService(
                breaker, healthMonitor.Object, trayService.Object, loc.Object, NullLogger<PluginBreakerNotificationService>.Instance);

            var descriptor = CreateExtensionDescriptor("trip.plugin");
            for (var i = 0; i < 3; i++)
            {
                breaker.RecordFailure(descriptor, "trip.plugin", new InvalidOperationException("boom"));
            }

            // The trip above already showed one tray notification (covered by the
            // Tripped test). Clear it so the recovery assertions below only see
            // calls made during the recovery transition.
            trayService.Invocations.Clear();

            // Force cooldown expiry so the next availability check recovers.
            var brokenAtField = typeof(PluginCircuitBreakerPolicy).GetField("_brokenCircuits", BindingFlags.NonPublic | BindingFlags.Instance);
            brokenAtField.Should().NotBeNull();
            var brokenCircuits = brokenAtField!.GetValue(breaker).Should().BeAssignableTo<ConcurrentDictionary<string, DateTime>>().Subject;
            brokenCircuits["trip.plugin"] = DateTime.UtcNow - TimeSpan.FromMinutes(2);

            var availability = breaker.CheckAvailability(descriptor, "trip.plugin");

            availability.Allowed.Should().BeTrue();
            availability.Recovered.Should().BeTrue();
            healthMonitor.Verify(x => x.RecordCircuitBreakerRecovery("trip.plugin"), Times.Once);
            trayService.Verify(x => x.ShowNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PulsarNotificationIcon>()), Times.Never);
            service.Should().NotBeNull();
        }

        [Fact]
        public void ObserverException_IsIsolated_AndDoesNotPolluteBreakerStateMachine()
        {
            var healthMonitor = new Mock<IPluginHealthMonitor>();
            var trayService = new Mock<ITrayService>();
            trayService.Setup(x => x.ShowNotification(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<PulsarNotificationIcon>()))
                .Throws<InvalidOperationException>();
            var loc = CreateLocalization();
            var breaker = new PluginCircuitBreakerPolicy();
            var service = new PluginBreakerNotificationService(
                breaker, healthMonitor.Object, trayService.Object, loc.Object, NullLogger<PluginBreakerNotificationService>.Instance);

            var descriptor = CreateExtensionDescriptor("trip.plugin");
            for (var i = 0; i < 3; i++)
            {
                breaker.RecordFailure(descriptor, "trip.plugin", new InvalidOperationException("boom"));
            }

            // Telemetry was recorded before the tray notification threw; the
            // exception was swallowed by the observer, so the circuit is still open.
            healthMonitor.Verify(x => x.RecordCircuitBreakerTrip("trip.plugin"), Times.Once);
            breaker.CheckAvailability(descriptor, "trip.plugin").Allowed.Should().BeFalse();
        }

        private static Mock<ILocalizationService> CreateLocalization()
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l[It.IsAny<string>()]).Returns((string key) => key);
            loc.Setup(l => l["Plugin.CircuitBreakerTitle"]).Returns("Plugin Auto-Disabled");
            loc.Setup(l => l["Plugin.CircuitBreakerBody"]).Returns("Plugin '{0}' has been temporarily disabled for {1} seconds due to repeated crashes to protect the main program.");
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            return loc;
        }

        private static PluginDescriptor CreateExtensionDescriptor(string id)
        {
            return new PluginDescriptor
            {
                Id = id,
                DisplayName = id,
                Version = "1.0.0",
                Author = "Tests",
                Description = id,
                Icon = "T",
                CanDisable = true,
                Tier = PluginTier.Extension,
                ImplementationType = typeof(PluginBreakerNotificationServiceTests),
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
                        Tier = PluginTier.Extension,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }
    }
}
