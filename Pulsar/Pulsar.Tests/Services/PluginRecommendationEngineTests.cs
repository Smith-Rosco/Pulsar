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

        private PluginRecommendationEngine CreateEngine(Func<DateTime>? clock = null)
        {
            return new PluginRecommendationEngine(
                _registryMock.Object,
                _usageTrackerMock.Object,
                _healthMonitorMock.Object,
                _loggerMock.Object,
                _loc,
                clock);
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

        private static PluginUsageStats CreateStatsWithTrend(string id, int recentPerDay, int previousPerDay, DateTime now)
        {
            var daily = new Dictionary<string, int>();
            for (int i = 0; i < 7; i++)
            {
                daily[now.AddDays(-i).ToString("yyyy-MM-dd")] = recentPerDay;
                daily[now.AddDays(-i - 7).ToString("yyyy-MM-dd")] = previousPerDay;
            }

            return new PluginUsageStats
            {
                PluginId = id,
                TotalExecutions = recentPerDay * 7 + previousPerDay * 7,
                DailyStats = daily,
                LastUsed = now
            };
        }

        [Fact]
        public void RecentUsageDoublesPrevious_TriggersUsageTrendUp()
        {
            var now = new DateTime(2026, 9, 2, 12, 0, 0);
            var stats = CreateStatsWithTrend("plugin.used.often", recentPerDay: 20, previousPerDay: 5, now);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.used.often"] = stats
            });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.used.often"] = new PluginHealthReport { PluginId = "plugin.used.often" }
            });

            var engine = CreateEngine(() => now);
            var recommendations = engine.GetRecommendations();

            recommendations.Should().Contain(r =>
                r.PluginId == "plugin.used.often" &&
                r.Type == RecommendationType.UsageTrendUp);
        }

        [Fact]
        public void RecentUsageHalvesPrevious_TriggersUsageTrendDown()
        {
            var now = new DateTime(2026, 9, 2, 12, 0, 0);
            var stats = CreateStatsWithTrend("plugin.used.often", recentPerDay: 5, previousPerDay: 20, now);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.used.often"] = stats
            });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.used.often"] = new PluginHealthReport { PluginId = "plugin.used.often" }
            });

            var engine = CreateEngine(() => now);
            var recommendations = engine.GetRecommendations();

            recommendations.Should().Contain(r =>
                r.PluginId == "plugin.used.often" &&
                r.Type == RecommendationType.UsageTrendDown);
        }

        [Fact]
        public void UsageChangeBelowThreshold_DoesNotTriggerTrend()
        {
            var now = new DateTime(2026, 9, 2, 12, 0, 0);
            var stats = CreateStatsWithTrend("plugin.used.often", recentPerDay: 12, previousPerDay: 10, now);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.used.often"] = stats
            });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.used.often"] = new PluginHealthReport { PluginId = "plugin.used.often" }
            });

            var engine = CreateEngine(() => now);
            var recommendations = engine.GetRecommendations();

            recommendations.Should().NotContain(r =>
                r.PluginId == "plugin.used.often" &&
                (r.Type == RecommendationType.UsageTrendUp || r.Type == RecommendationType.UsageTrendDown));
        }

        [Fact]
        public void PreviousWindowEmpty_DoesNotTriggerTrend()
        {
            var now = new DateTime(2026, 9, 2, 12, 0, 0);
            var stats = CreateStatsWithTrend("plugin.used.often", recentPerDay: 15, previousPerDay: 0, now);
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.used.often"] = stats
            });
            _healthMonitorMock.Setup(h => h.GetAllHealthReports()).Returns(new Dictionary<string, PluginHealthReport>
            {
                ["plugin.used.often"] = new PluginHealthReport { PluginId = "plugin.used.often" }
            });

            var engine = CreateEngine(() => now);
            var recommendations = engine.GetRecommendations();

            recommendations.Should().NotContain(r =>
                r.PluginId == "plugin.used.often" &&
                (r.Type == RecommendationType.UsageTrendUp || r.Type == RecommendationType.UsageTrendDown));
        }
    }
}
