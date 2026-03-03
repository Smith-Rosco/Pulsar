// [Path]: Pulsar.Tests/Plugin/PluginRegistryCircuitBreakerTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// 插件注册中心断路器测试
    /// 测试目标：验证 Circuit Breaker 的触发、冷却、恢复机制
    /// </summary>
    public class PluginRegistryCircuitBreakerTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<ILogger<PluginRegistry>> _mockLogger;
        private readonly Mock<ITrayService> _mockTrayService;
        private readonly Mock<IConfigService> _mockConfigService;

        public PluginRegistryCircuitBreakerTests()
        {
            var services = new ServiceCollection();
            
            _mockLogger = new Mock<ILogger<PluginRegistry>>();
            _mockTrayService = new Mock<ITrayService>();
            _mockConfigService = new Mock<IConfigService>();
            
            services.AddSingleton(_mockLogger.Object);
            services.AddSingleton(_mockTrayService.Object);
            services.AddSingleton(_mockConfigService.Object);
            
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task CircuitBreaker_ShouldTrip_AfterThreeConsecutiveFailures()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new FaultyTestPlugin(shouldThrow: true);
            
            // Manually register plugin (bypass LoadAllAsync)
            typeof(PluginRegistry)
                .GetField("_plugins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(registry, new Dictionary<string, IPulsarPlugin> { [plugin.Id] = plugin });
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act - Execute 3 times to trigger circuit breaker
            var result1 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result2 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result3 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result4 = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result1.Success.Should().BeFalse("first failure should return error");
            result2.Success.Should().BeFalse("second failure should return error");
            result3.Success.Should().BeFalse("third failure should return error");
            result4.Success.Should().BeFalse("circuit breaker should be open");
            result4.Message.Should().Contain("disabled for safety", "circuit breaker message should indicate plugin is disabled");
        }

        [Fact]
        public async Task CircuitBreaker_ShouldNotTrip_ForCorePlugins()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new FaultyTestPlugin(shouldThrow: true, canDisable: false); // Core plugin
            
            typeof(PluginRegistry)
                .GetField("_plugins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(registry, new Dictionary<string, IPulsarPlugin> { [plugin.Id] = plugin });
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act - Execute 4 times (should not trigger circuit breaker for core plugins)
            var result1 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result2 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result3 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result4 = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result4.Message.Should().NotContain("disabled for safety", "core plugins should not be protected by circuit breaker");
        }

        [Fact]
        public async Task CircuitBreaker_ShouldReset_AfterSuccessfulExecution()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new FaultyTestPlugin(shouldThrow: false); // Can control failure
            
            typeof(PluginRegistry)
                .GetField("_plugins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(registry, new Dictionary<string, IPulsarPlugin> { [plugin.Id] = plugin });
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act - Fail twice, then succeed, then fail twice more
            plugin.ShouldThrow = true;
            await registry.ExecuteAsync(plugin.Id, "test", args, context);
            await registry.ExecuteAsync(plugin.Id, "test", args, context);
            
            plugin.ShouldThrow = false;
            var successResult = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            
            plugin.ShouldThrow = true;
            var result1 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result2 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result3 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result4 = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            successResult.Success.Should().BeTrue("success should reset failure count");
            result3.Success.Should().BeFalse("third failure should still execute but fail");
            result4.Message.Should().Contain("disabled for safety", "circuit breaker should be open after 3 failures");
        }

        [Fact]
        public async Task CircuitBreaker_ShouldTripOnCriticalErrors()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new FaultyTestPlugin(shouldThrow: false, returnCriticalError: true);
            
            typeof(PluginRegistry)
                .GetField("_plugins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
                ?.SetValue(registry, new Dictionary<string, IPulsarPlugin> { [plugin.Id] = plugin });
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act - Return Critical errors 3 times
            var result1 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result2 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result3 = await registry.ExecuteAsync(plugin.Id, "test", args, context);
            var result4 = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result1.Success.Should().BeFalse();
            result1.Severity.Should().Be(PluginErrorSeverity.Critical);
            result4.Message.Should().Contain("disabled for safety", "critical errors should trigger circuit breaker");
        }

        /// <summary>
        /// Test plugin that can simulate failures
        /// </summary>
        private class FaultyTestPlugin : IPulsarPlugin
        {
            public string Id => "test.faulty.plugin";
            public string DisplayName => "Faulty Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Test plugin for circuit breaker";
            public string Icon => "⚠️";
            public bool CanDisable { get; }
            public bool ShouldThrow { get; set; }
            public bool ReturnCriticalError { get; }

            public FaultyTestPlugin(bool shouldThrow, bool canDisable = true, bool returnCriticalError = false)
            {
                ShouldThrow = shouldThrow;
                CanDisable = canDisable;
                ReturnCriticalError = returnCriticalError;
            }

            public void Initialize(IServiceProvider services) { }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context)
            {
                if (ShouldThrow)
                {
                    throw new InvalidOperationException("Simulated plugin crash");
                }

                if (ReturnCriticalError)
                {
                    return Task.FromResult(PluginResult.Error("Critical error", PluginErrorSeverity.Critical));
                }

                return Task.FromResult(PluginResult.Ok("Success"));
            }
        }
    }
}
