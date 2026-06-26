using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class PluginRecommendationEngineTests
    {
        private readonly Mock<IPluginRegistry> _registryMock = new();
        private readonly Mock<IPluginUsageTracker> _usageTrackerMock = new();
        private readonly Mock<IPluginHealthMonitor> _healthMonitorMock = new();
        private readonly Mock<ILogger<PluginRecommendationEngine>> _loggerMock = new();
        private readonly ILocalizationService _loc;
        private readonly List<IPulsarPlugin> _testPlugins;

        public PluginRecommendationEngineTests()
        {
            _loc = new LocalizationService(new Mock<ILogger<LocalizationService>>().Object);
            _testPlugins = new List<IPulsarPlugin>
            {
                CreatePlugin("plugin.never.used", "Never Used", true),
                CreatePlugin("plugin.used.often", "Used Often", true),
                CreatePlugin("plugin.core", "Core Plugin", false),
                CreatePlugin("plugin.high.error", "High Error", true),
            };
            _registryMock.Setup(r => r.GetAllPlugins()).Returns(_testPlugins);
        }

        private static IPulsarPlugin CreatePlugin(string id, string displayName, bool canDisable)
        {
            var mock = new Mock<IPulsarPlugin>();
            mock.Setup(p => p.Id).Returns(id);
            mock.Setup(p => p.DisplayName).Returns(displayName);
            mock.Setup(p => p.CanDisable).Returns(canDisable);
            return mock.Object;
        }

        private PluginRecommendationEngine CreateEngine()
        {
            return new PluginRecommendationEngine(
                _registryMock.Object,
                _usageTrackerMock.Object,
                _healthMonitorMock.Object,
                _loggerMock.Object,
                _loc);
        }

        [Fact]
        public void NeverUsedPlugin_TriggersDisableUnusedPlugin_Recommendation()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.never.used"] = new PluginUsageStats
                {
                    PluginId = "plugin.never.used",
                    TotalExecutions = 0,
                    LastUsed = null
                }
            });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.never.used"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "plugin.never.used",
                    TotalExecutions = 0,
                    LastUsed = null
                });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.never.used"] = new PluginHealthReport { PluginId = "plugin.never.used" }
            });
            _healthMonitorMock.Setup(h => h.GetHealthReport("plugin.never.used"))
                .Returns(new PluginHealthReport { PluginId = "plugin.never.used" });

            var engine = CreateEngine();
            var recommendations = engine.GetRecommendations();

            recommendations.Should().Contain(r =>
                r.PluginId == "plugin.never.used" &&
                r.Type == RecommendationType.DisableUnusedPlugin);
        }

        [Fact]
        public void PluginUnusedOver30Days_TriggersDisableUnusedPlugin_Recommendation()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.used.often"] = new PluginUsageStats
                {
                    PluginId = "plugin.used.often",
                    TotalExecutions = 50,
                    LastUsed = DateTime.UtcNow.AddDays(-45)
                }
            });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.used.often"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "plugin.used.often",
                    TotalExecutions = 50,
                    LastUsed = DateTime.UtcNow.AddDays(-45)
                });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.used.often"] = new PluginHealthReport { PluginId = "plugin.used.often" }
            });
            _healthMonitorMock.Setup(h => h.GetHealthReport("plugin.used.often"))
                .Returns(new PluginHealthReport { PluginId = "plugin.used.often" });

            var engine = CreateEngine();
            var recommendations = engine.GetRecommendations();

            recommendations.Should().Contain(r =>
                r.PluginId == "plugin.used.often" &&
                r.Type == RecommendationType.DisableUnusedPlugin);
        }

        [Fact]
        public void HighErrorRate_TriggersCheckPluginErrors_Recommendation()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.high.error"] = new PluginUsageStats
                {
                    PluginId = "plugin.high.error",
                    TotalExecutions = 50
                }
            });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.high.error"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "plugin.high.error",
                    TotalExecutions = 50
                });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.high.error"] = new PluginHealthReport
                {
                    PluginId = "plugin.high.error",
                    ErrorRate = 0.3,
                    CircuitBreakerTrips = 0
                }
            });
            _healthMonitorMock.Setup(h => h.GetHealthReport("plugin.high.error"))
                .Returns(new PluginHealthReport
                {
                    PluginId = "plugin.high.error",
                    ErrorRate = 0.3,
                    CircuitBreakerTrips = 0
                });

            var engine = CreateEngine();
            var recommendations = engine.GetRecommendations();

            recommendations.Should().Contain(r =>
                r.PluginId == "plugin.high.error" &&
                r.Type == RecommendationType.CheckPluginErrors);
        }

        [Fact]
        public void CircuitBreakerTrips_TriggerRecommendation()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.high.error"] = new PluginUsageStats
                {
                    PluginId = "plugin.high.error",
                    TotalExecutions = 50
                }
            });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.high.error"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "plugin.high.error",
                    TotalExecutions = 50
                });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.high.error"] = new PluginHealthReport
                {
                    PluginId = "plugin.high.error",
                    ErrorRate = 0.05,
                    CircuitBreakerTrips = 3
                }
            });
            _healthMonitorMock.Setup(h => h.GetHealthReport("plugin.high.error"))
                .Returns(new PluginHealthReport
                {
                    PluginId = "plugin.high.error",
                    ErrorRate = 0.05,
                    CircuitBreakerTrips = 3
                });

            var engine = CreateEngine();
            var recommendations = engine.GetRecommendations();

            recommendations.Should().Contain(r =>
                r.PluginId == "plugin.high.error" &&
                r.Type == RecommendationType.CheckPluginErrors);
        }

        [Fact]
        public void CorePlugins_WithCanDisableFalse_AreExcludedFromRecommendations()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.core"] = new PluginUsageStats
                {
                    PluginId = "plugin.core",
                    TotalExecutions = 0,
                    LastUsed = null
                }
            });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.core"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "plugin.core",
                    TotalExecutions = 0,
                    LastUsed = null
                });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.core"] = new PluginHealthReport { PluginId = "plugin.core" }
            });
            _healthMonitorMock.Setup(h => h.GetHealthReport("plugin.core"))
                .Returns(new PluginHealthReport { PluginId = "plugin.core" });

            var engine = CreateEngine();
            var recommendations = engine.GetRecommendations();

            recommendations.Should().NotContain(r => r.PluginId == "plugin.core");
        }

        [Fact]
        public void GetRecommendationsForPlugin_ReturnsOnlyThatPluginsRecommendations()
        {
            _usageTrackerMock.Setup(u => u.GetStats("plugin.never.used"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "plugin.never.used",
                    TotalExecutions = 0,
                    LastUsed = null
                });
            _healthMonitorMock.Setup(h => h.GetHealthReport("plugin.never.used"))
                .Returns(new PluginHealthReport { PluginId = "plugin.never.used" });

            var engine = CreateEngine();
            var recommendations = engine.GetRecommendationsForPlugin("plugin.never.used");

            recommendations.Should().AllSatisfy(r => r.PluginId.Should().Be("plugin.never.used"));
        }
    }
}
