using System;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class PluginHealthMonitorTests
    {
        private readonly Mock<ILogger<PluginHealthMonitor>> _loggerMock = new();
        private readonly Mock<IPluginUsageTracker> _usageTrackerMock = new();

        private PluginHealthMonitor CreateMonitor()
        {
            return new PluginHealthMonitor(_loggerMock.Object, _usageTrackerMock.Object);
        }

        [Fact]
        public void RecordSuccess_RecordError_PopulateRecentExecutionBuffer()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin", LastUsed = DateTime.UtcNow });

            var monitor = CreateMonitor();

            monitor.RecordSuccess("test.plugin");
            monitor.RecordError("test.plugin", new Exception("test error"));

            var report = monitor.GetHealthReport("test.plugin");
            report.RecentExecutionCount.Should().Be(2);
            report.RecentErrorCount.Should().Be(1);
            report.ErrorRate.Should().Be(0.5);
        }

        [Fact]
        public void CalculateHealthScore_Returns100_For100PercentSuccessRate()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin", LastUsed = DateTime.UtcNow });

            var monitor = CreateMonitor();
            for (int i = 0; i < 100; i++)
            {
                monitor.RecordSuccess("test.plugin");
            }

            var score = monitor.CalculateHealthScore("test.plugin");
            score.Should().BeInRange(99, 100);
        }

        [Fact]
        public void CalculateHealthScore_PenalizesErrors_50PercentErrorRate()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin", LastUsed = DateTime.UtcNow });

            var monitor = CreateMonitor();
            for (int i = 0; i < 50; i++)
            {
                monitor.RecordSuccess("test.plugin");
                monitor.RecordError("test.plugin", new Exception("error"));
            }

            var score = monitor.CalculateHealthScore("test.plugin");
            score.Should().BeInRange(74, 75);
        }

        [Fact]
        public void CalculateHealthScore_PenalizesCircuitBreakerTrips()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin", LastUsed = DateTime.UtcNow });

            var monitor = CreateMonitor();
            for (int i = 0; i < 3; i++)
            {
                monitor.RecordCircuitBreakerTrip("test.plugin");
            }

            var score = monitor.CalculateHealthScore("test.plugin");
            score.Should().BeInRange(69, 70);
        }

        [Fact]
        public void CalculateHealthScore_PenalizesUnusedPlugins()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats
                {
                    PluginId = "test.plugin",
                    LastUsed = DateTime.UtcNow.AddDays(-60)
                });

            var monitor = CreateMonitor();

            var score = monitor.CalculateHealthScore("test.plugin");
            score.Should().BeLessThan(100);
            score.Should().BeGreaterOrEqualTo(0);
        }

        [Fact]
        public void RecordCircuitBreakerTrip_SetsBreakerStateToOpen()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin" });

            var monitor = CreateMonitor();

            monitor.RecordCircuitBreakerTrip("test.plugin");

            monitor.IsCircuitBreakerOpen("test.plugin").Should().BeTrue();
        }

        [Fact]
        public void RecordCircuitBreakerRecovery_SetsBreakerStateToClosed()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin" });

            var monitor = CreateMonitor();

            monitor.RecordCircuitBreakerTrip("test.plugin");
            monitor.RecordCircuitBreakerRecovery("test.plugin");

            monitor.IsCircuitBreakerOpen("test.plugin").Should().BeFalse();
        }

        [Fact]
        public void DetermineHealthStatus_ReturnsCorrectStatus()
        {
            _usageTrackerMock.Setup(u => u.GetStats(It.IsAny<string>()))
                .Returns(new PluginUsageStats { PluginId = "test.plugin", LastUsed = DateTime.UtcNow });

            var monitor = CreateMonitor();

            for (int i = 0; i < 10; i++)
                monitor.RecordSuccess("test.plugin");

            var report = monitor.GetHealthReport("test.plugin");
            report.Status.Should().Be(PluginHealthStatus.Healthy);

            for (int i = 0; i < 5; i++)
                monitor.RecordError("test.plugin", new Exception("err"));

            report = monitor.GetHealthReport("test.plugin");
            report.Status.Should().Be(PluginHealthStatus.Warning);

            monitor.RecordCircuitBreakerTrip("test.plugin");
            report = monitor.GetHealthReport("test.plugin");
            report.Status.Should().Be(PluginHealthStatus.Critical);

            _usageTrackerMock.Setup(u => u.GetStats("unused.plugin"))
                .Returns(new PluginUsageStats
                {
                    PluginId = "unused.plugin",
                    LastUsed = DateTime.UtcNow.AddDays(-60)
                });

            report = monitor.GetHealthReport("unused.plugin");
            report.Status.Should().Be(PluginHealthStatus.Unused);
        }

        [Fact]
        public void GetAllHealthReports_ReturnsReportsForAllTrackedPlugins()
        {
            _usageTrackerMock.Setup(u => u.GetAllStats()).Returns(new Dictionary<string, PluginUsageStats>
            {
                ["plugin.a"] = new PluginUsageStats { PluginId = "plugin.a", LastUsed = DateTime.UtcNow },
                ["plugin.b"] = new PluginUsageStats { PluginId = "plugin.b", LastUsed = DateTime.UtcNow }
            });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.a"))
                .Returns(new PluginUsageStats { PluginId = "plugin.a", LastUsed = DateTime.UtcNow });
            _usageTrackerMock.Setup(u => u.GetStats("plugin.b"))
                .Returns(new PluginUsageStats { PluginId = "plugin.b", LastUsed = DateTime.UtcNow });

            var monitor = CreateMonitor();
            monitor.RecordSuccess("plugin.a");
            monitor.RecordSuccess("plugin.b");

            var reports = monitor.GetAllHealthReports();

            reports.Should().ContainKeys("plugin.a", "plugin.b");
            reports.Count.Should().Be(2);
        }
    }
}
