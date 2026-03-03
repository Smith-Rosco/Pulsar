// [Path]: Pulsar.Tests/Plugin/PluginRegistryExecutionTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Tests.TestHelpers;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// 插件注册中心执行测试
    /// 测试目标：验证插件执行的基本行为、异常隔离、状态管理
    /// </summary>
    public class PluginRegistryExecutionTests
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly Mock<ILogger<PluginRegistry>> _mockLogger;
        private readonly Mock<IConfigService> _mockConfigService;

        public PluginRegistryExecutionTests()
        {
            var services = new ServiceCollection();
            
            _mockLogger = new Mock<ILogger<PluginRegistry>>();
            _mockConfigService = new Mock<IConfigService>();
            
            services.AddSingleton(_mockLogger.Object);
            services.AddSingleton(_mockConfigService.Object);
            
            _serviceProvider = services.BuildServiceProvider();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnSuccess_WhenPluginSucceeds()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new TestPlugin(shouldSucceed: true);
            
            RegisterPlugin(registry, plugin);
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act
            var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result.Success.Should().BeTrue();
            result.Message.Should().Be("Test success");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnError_WhenPluginFails()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new TestPlugin(shouldSucceed: false);
            
            RegisterPlugin(registry, plugin);
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act
            var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Be("Test failure");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldIsolateException_WhenPluginThrows()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new TestPlugin(shouldThrow: true);
            
            RegisterPlugin(registry, plugin);
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act
            var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result.Success.Should().BeFalse("exception should be caught and converted to error result");
            result.Message.Should().Contain("Plugin execution failed");
            result.Message.Should().Contain("Test exception");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnError_WhenPluginNotFound()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act
            var result = await registry.ExecuteAsync("nonexistent.plugin", "test", args, context);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Plugin not found");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldRespectDisabledState_ForExtensionPlugins()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new TestPlugin(shouldSucceed: true, canDisable: true);
            
            RegisterPlugin(registry, plugin);
            
            // Mock config service to return disabled state
            var config = new ProfilesConfig();
            config.Plugins[plugin.Id] = new PluginProfile { Enabled = false };
            _mockConfigService.Setup(x => x.Current).Returns(config);
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act
            var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result.Success.Should().BeFalse();
            result.Message.Should().Contain("Plugin is disabled");
        }

        [Fact]
        public async Task ExecuteAsync_ShouldIgnoreDisabledState_ForCorePlugins()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new TestPlugin(shouldSucceed: true, canDisable: false); // Core plugin
            
            RegisterPlugin(registry, plugin);
            
            // Mock config service to return disabled state (should be ignored for core plugins)
            var config = new ProfilesConfig();
            config.Plugins[plugin.Id] = new PluginProfile { Enabled = false };
            _mockConfigService.Setup(x => x.Current).Returns(config);
            
            var context = PulsarContextFactory.CreateTestContext();
            var args = new Dictionary<string, string>().AsReadOnly();

            // Act
            var result = await registry.ExecuteAsync(plugin.Id, "test", args, context);

            // Assert
            result.Success.Should().BeTrue("core plugins cannot be disabled");
        }

        [Fact]
        public void GetPlugin_ShouldReturnPlugin_WhenExists()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin = new TestPlugin(shouldSucceed: true);
            
            RegisterPlugin(registry, plugin);

            // Act
            var retrieved = registry.GetPlugin(plugin.Id);

            // Assert
            retrieved.Should().NotBeNull();
            retrieved!.Id.Should().Be(plugin.Id);
        }

        [Fact]
        public void GetPlugin_ShouldReturnNull_WhenNotExists()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);

            // Act
            var retrieved = registry.GetPlugin("nonexistent.plugin");

            // Assert
            retrieved.Should().BeNull();
        }

        [Fact]
        public void GetAllPlugins_ShouldReturnAllRegisteredPlugins()
        {
            // Arrange
            var registry = new PluginRegistry(_serviceProvider, _mockLogger.Object);
            var plugin1 = new TestPlugin(shouldSucceed: true) { TestId = "plugin1" };
            var plugin2 = new TestPlugin(shouldSucceed: true) { TestId = "plugin2" };
            
            RegisterPlugin(registry, plugin1);
            RegisterPlugin(registry, plugin2);

            // Act
            var allPlugins = registry.GetAllPlugins();

            // Assert
            allPlugins.Should().HaveCount(2);
        }

        /// <summary>
        /// Helper method to register plugin using reflection
        /// </summary>
        private void RegisterPlugin(PluginRegistry registry, IPulsarPlugin plugin)
        {
            var pluginsField = typeof(PluginRegistry)
                .GetField("_plugins", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
            
            var plugins = pluginsField?.GetValue(registry) as Dictionary<string, IPulsarPlugin>;
            if (plugins != null)
            {
                plugins[plugin.Id] = plugin;
            }
        }

        /// <summary>
        /// Test plugin with configurable behavior
        /// </summary>
        private class TestPlugin : IPulsarPlugin
        {
            public string TestId { get; set; } = "test.plugin";
            public string Id => TestId;
            public string DisplayName => "Test Plugin";
            public string Version => "1.0.0";
            public string Author => "Test";
            public string Description => "Test plugin";
            public string Icon => "🧪";
            public bool CanDisable { get; }

            private readonly bool _shouldSucceed;
            private readonly bool _shouldThrow;

            public TestPlugin(bool shouldSucceed = true, bool shouldThrow = false, bool canDisable = true)
            {
                _shouldSucceed = shouldSucceed;
                _shouldThrow = shouldThrow;
                CanDisable = canDisable;
            }

            public void Initialize(IServiceProvider services) { }

            public Task<PluginResult> ExecuteAsync(string action, IReadOnlyDictionary<string, string> args, PulsarContext context)
            {
                if (_shouldThrow)
                {
                    throw new InvalidOperationException("Test exception");
                }

                if (_shouldSucceed)
                {
                    return Task.FromResult(PluginResult.Ok("Test success"));
                }

                return Task.FromResult(PluginResult.Error("Test failure"));
            }
        }
    }
}
