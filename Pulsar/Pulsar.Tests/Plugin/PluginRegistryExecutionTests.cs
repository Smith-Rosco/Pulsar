// [Path]: Pulsar.Tests/Plugin/PluginRegistryExecutionTests.cs

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Core.Plugin.Runtime;
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
        private readonly Mock<IConfigService> _mockConfigService;

        public PluginRegistryExecutionTests()
        {
            _mockConfigService = new Mock<IConfigService>();
        }

        [Fact]
        public async Task ExecuteAsync_ShouldReturnSuccess_WhenPluginSucceeds()
        {
            // Arrange
            var plugin = new TestPlugin(shouldSucceed: true);
            var registry = CreateRegistry(plugin);
            
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
            var plugin = new TestPlugin(shouldSucceed: false);
            var registry = CreateRegistry(plugin);
            
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
            var plugin = new TestPlugin(shouldThrow: true);
            var registry = CreateRegistry(plugin);
            
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
            var registry = CreateEmptyRegistry();
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
            var plugin = new TestPlugin(shouldSucceed: true, canDisable: true);
            
            // Mock config service to return disabled state
            var config = new ProfilesConfig();
            config.Plugins[plugin.Id] = new PluginProfile { Enabled = false };
            _mockConfigService.Setup(x => x.Current).Returns(config);
            
            var registry = CreateRegistry(plugin, _mockConfigService.Object);
            
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
            var plugin = new TestPlugin(shouldSucceed: true, canDisable: false); // Core plugin
            
            // Mock config service to return disabled state (should be ignored for core plugins)
            var config = new ProfilesConfig();
            config.Plugins[plugin.Id] = new PluginProfile { Enabled = false };
            _mockConfigService.Setup(x => x.Current).Returns(config);
            
            var registry = CreateRegistry(plugin, _mockConfigService.Object);
            
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
            var plugin = new TestPlugin(shouldSucceed: true);
            var registry = CreateRegistry(plugin);

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
            var registry = CreateEmptyRegistry();

            // Act
            var retrieved = registry.GetPlugin("nonexistent.plugin");

            // Assert
            retrieved.Should().BeNull();
        }

        [Fact]
        public void GetAllPlugins_ShouldReturnAllRegisteredPlugins()
        {
            // Arrange
            var plugin1 = new TestPlugin(shouldSucceed: true) { TestId = "plugin1" };
            var plugin2 = new TestPlugin(shouldSucceed: true) { TestId = "plugin2" };
            var registry = CreateRegistryWithPlugins(null, plugin1, plugin2);

            // Act
            var allPlugins = registry.GetAllPlugins();

            // Assert
            allPlugins.Should().HaveCount(2);
        }

        private PluginRegistry CreateRegistry(IPulsarPlugin plugin, IConfigService? configService = null)
        {
            return CreateRegistryWithPlugins(configService, plugin);
        }

        private PluginRegistry CreateEmptyRegistry()
        {
            var catalog = new PluginCatalog();
            var runtimeState = new PluginRuntimeStateStore();
            var breakerPolicy = new PluginCircuitBreakerPolicy();
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy);

            var loader = new FakeLoader(
                new TestPlugin(shouldSucceed: true),
                CreateDescriptor(new TestPlugin(shouldSucceed: true) { TestId = "stub" }));
            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(), loader, catalog, runtimeState, pipeline);
            return new PluginRegistry(kernel, catalog, runtimeState);
        }

        private PluginRegistry CreateRegistryWithPlugins(IConfigService? configService, params IPulsarPlugin[] plugins)
        {
            var catalog = new PluginCatalog();
            var runtimeState = new PluginRuntimeStateStore();
            var breakerPolicy = new PluginCircuitBreakerPolicy();
            var pipeline = new PluginExecutionPipeline(runtimeState, breakerPolicy);

            var descriptors = new List<PluginDescriptor>();
            foreach (var plugin in plugins)
            {
                var descriptor = CreateDescriptor(plugin);
                descriptors.Add(descriptor);
                runtimeState.SetPlugin(plugin, PluginLifecycleState.Enabled);
            }
            catalog.RegisterDescriptors(descriptors);

            var loader = new FakeLoader(plugins[0], descriptors[0]);
            var kernel = new PluginRuntimeKernel(
                Mock.Of<IServiceProvider>(), loader, catalog, runtimeState, pipeline,
                NullLogger<PluginRuntimeKernel>.Instance, configService);
            return new PluginRegistry(kernel, catalog, runtimeState);
        }

        private static PluginDescriptor CreateDescriptor(IPulsarPlugin plugin)
        {
            return new PluginDescriptor
            {
                Id = plugin.Id,
                DisplayName = plugin.DisplayName,
                Version = plugin.Version,
                Author = plugin.Author,
                Description = plugin.Description,
                Icon = plugin.Icon,
                CanDisable = plugin.CanDisable,
                Tier = plugin.CanDisable ? PluginTier.Extension : PluginTier.Core,
                ImplementationType = plugin.GetType(),
                Dependencies = new List<string>(),
                Metadata = new PluginMetadata
                {
                    Id = plugin.Id,
                    Display = new DisplayInfo
                    {
                        Name = plugin.DisplayName,
                        Description = plugin.Description,
                        IconKey = plugin.Icon,
                        Category = "Tests",
                        Version = plugin.Version,
                        Author = plugin.Author,
                        License = "MIT"
                    },
                    Schema = null,
                    UI = new UIHints
                    {
                        Badge = "Test",
                        AccentColor = "#4A90E2",
                        ShowInQuickAccess = false,
                        SortOrder = 0
                    },
                    Capabilities = new PluginCapabilities
                    {
                        SupportedActions = new List<string> { "test" },
                        Dependencies = new List<string>(),
                        Tier = plugin.CanDisable ? PluginTier.Extension : PluginTier.Core,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                },
                IsConfigurable = false
            };
        }

        private sealed class FakeLoader : PluginLoader
        {
            private readonly IPulsarPlugin _plugin;
            private readonly PluginDescriptor _descriptor;

            public FakeLoader(IPulsarPlugin plugin, PluginDescriptor descriptor)
                : base(Mock.Of<IServiceProvider>(), string.Empty)
            {
                _plugin = plugin;
                _descriptor = descriptor;
            }

            public override List<PluginDescriptor> DiscoverDescriptors(bool includeCore, bool includeExtensions, bool analyzeDependencies)
            {
                return new List<PluginDescriptor> { _descriptor };
            }

            public override IPulsarPlugin ActivatePlugin(PluginDescriptor descriptor)
            {
                return _plugin;
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
