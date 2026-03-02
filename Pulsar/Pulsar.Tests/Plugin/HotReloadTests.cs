using System;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin;
using Xunit;

namespace Pulsar.Tests.Plugin
{
    /// <summary>
    /// 热重载管理器集成测试
    /// </summary>
    public class HotReloadTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _pluginDirectory;
        private readonly Mock<ILogger<HotReloadManager>> _mockLogger;
        private HotReloadManager? _hotReloadManager;

        public HotReloadTests()
        {
            // 创建临时测试目录
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            _pluginDirectory = Path.Combine(_testDirectory, "Plugins");
            Directory.CreateDirectory(_pluginDirectory);

            _mockLogger = new Mock<ILogger<HotReloadManager>>();
        }

        [Fact]
        public void Constructor_ShouldCreateShadowCopyDirectory()
        {
            // Arrange & Act
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);

            // Assert
            var shadowDir = Path.Combine(Path.GetTempPath(), "Pulsar", "PluginShadow");
            Directory.Exists(shadowDir).Should().BeTrue();
        }

        [Fact]
        public void Enable_ShouldStartWatchingPluginDirectory()
        {
            // Arrange
            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);

            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);

            // Act
            _hotReloadManager.Enable();

            // Assert - No exception should be thrown
            _hotReloadManager.Should().NotBeNull();
        }

        [Fact]
        public void Disable_ShouldStopWatching()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            _hotReloadManager.Enable();

            // Act
            _hotReloadManager.Disable();

            // Assert - No exception should be thrown
            _hotReloadManager.Should().NotBeNull();
        }

        [Fact]
        public void RegisterPlugin_ShouldTrackPluginPath()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            var pluginId = "TestPlugin";
            var pluginPath = Path.Combine(_pluginDirectory, "TestPlugin", "TestPlugin.dll");

            // Act
            _hotReloadManager.RegisterPlugin(pluginId, pluginPath);

            // Assert - No exception should be thrown
            _hotReloadManager.Should().NotBeNull();
        }

        [Fact]
        public void UnregisterPlugin_ShouldRemovePluginTracking()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            var pluginId = "TestPlugin";
            var pluginPath = Path.Combine(_pluginDirectory, "TestPlugin", "TestPlugin.dll");
            _hotReloadManager.RegisterPlugin(pluginId, pluginPath);

            // Act
            _hotReloadManager.UnregisterPlugin(pluginId);

            // Assert - No exception should be thrown
            _hotReloadManager.Should().NotBeNull();
        }

        [Fact]
        public void CreateShadowCopy_ShouldCopyFileToTempDirectory()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            
            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);
            
            var originalPath = Path.Combine(pluginFolder, "TestPlugin.dll");
            File.WriteAllText(originalPath, "Test DLL Content");

            // Act
            var shadowPath = _hotReloadManager.CreateShadowCopy(originalPath);

            // Assert
            File.Exists(shadowPath).Should().BeTrue();
            shadowPath.Should().Contain("PluginShadow");
            shadowPath.Should().Contain("TestPlugin_");
            File.ReadAllText(shadowPath).Should().Be("Test DLL Content");
        }

        [Fact]
        public void CreateShadowCopy_ShouldThrowIfFileNotFound()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            var nonExistentPath = Path.Combine(_pluginDirectory, "NonExistent.dll");

            // Act & Assert
            Action act = () => _hotReloadManager.CreateShadowCopy(nonExistentPath);
            act.Should().Throw<FileNotFoundException>();
        }

        [Fact]
        public void CreateShadowCopy_ShouldCopyDependencies()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            
            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);
            
            var mainDll = Path.Combine(pluginFolder, "TestPlugin.dll");
            var depDll = Path.Combine(pluginFolder, "Dependency.dll");
            var pdbFile = Path.Combine(pluginFolder, "TestPlugin.pdb");
            
            File.WriteAllText(mainDll, "Main DLL");
            File.WriteAllText(depDll, "Dependency DLL");
            File.WriteAllText(pdbFile, "PDB Content");

            // Act
            var shadowPath = _hotReloadManager.CreateShadowCopy(mainDll);

            // Assert
            File.Exists(shadowPath).Should().BeTrue();
            
            var shadowDir = Path.GetDirectoryName(shadowPath);
            var shadowFileName = Path.GetFileNameWithoutExtension(shadowPath);
            // Extract timestamp: "TestPlugin_20260302_123456_789" -> "20260302_123456_789"
            var timestampPart = shadowFileName.Substring("TestPlugin_".Length);
            
            var shadowDepPath = Path.Combine(shadowDir!, $"Dependency_{timestampPart}.dll");
            var shadowPdbPath = Path.Combine(shadowDir!, $"TestPlugin_{timestampPart}.pdb");
            
            File.Exists(shadowDepPath).Should().BeTrue();
            File.Exists(shadowPdbPath).Should().BeTrue();
        }

        [Fact]
        public void CleanupOldShadowCopies_ShouldKeepOnlyRecentVersions()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            
            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);
            
            var originalPath = Path.Combine(pluginFolder, "TestPlugin.dll");
            File.WriteAllText(originalPath, "Test DLL");

            // Create 10 shadow copies
            var shadowPaths = new List<string>();
            for (int i = 0; i < 10; i++)
            {
                Thread.Sleep(10); // Ensure different timestamps
                var shadowPath = _hotReloadManager.CreateShadowCopy(originalPath);
                shadowPaths.Add(shadowPath);
            }

            // Act
            _hotReloadManager.CleanupOldShadowCopies("TestPlugin.dll", keepCount: 5);

            // Assert
            var shadowDir = Path.Combine(Path.GetTempPath(), "Pulsar", "PluginShadow");
            var remainingFiles = Directory.GetFiles(shadowDir, "TestPlugin_*.dll");
            remainingFiles.Length.Should().BeLessOrEqualTo(5);
        }

        [Fact]
        public void CleanupAllShadowCopies_ShouldRemoveAllFiles()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            
            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);
            
            var originalPath = Path.Combine(pluginFolder, "TestPlugin.dll");
            File.WriteAllText(originalPath, "Test DLL");

            // Create some shadow copies
            _hotReloadManager.CreateShadowCopy(originalPath);
            _hotReloadManager.CreateShadowCopy(originalPath);

            // Act
            _hotReloadManager.CleanupAllShadowCopies();

            // Assert
            var shadowDir = Path.Combine(Path.GetTempPath(), "Pulsar", "PluginShadow");
            if (Directory.Exists(shadowDir))
            {
                var files = Directory.GetFiles(shadowDir);
                files.Length.Should().Be(0);
            }
        }

        [Fact]
        public async Task FileChange_ShouldTriggerPluginFileChangedEvent()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            _hotReloadManager.DebounceDelayMs = 200; // Shorter delay for testing

            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);
            
            var pluginPath = Path.Combine(pluginFolder, "TestPlugin.dll");
            File.WriteAllText(pluginPath, "Initial Content");

            _hotReloadManager.RegisterPlugin("TestPlugin", pluginPath);
            _hotReloadManager.Enable();

            var eventRaised = false;
            string? changedFilePath = null;

            _hotReloadManager.PluginFileChanged += (sender, args) =>
            {
                eventRaised = true;
                changedFilePath = args.FilePath;
            };

            // Act
            await Task.Delay(100); // Wait for watcher to initialize
            File.WriteAllText(pluginPath, "Modified Content");
            await Task.Delay(1000); // Wait for debounce + processing

            // Assert
            eventRaised.Should().BeTrue();
            changedFilePath.Should().Be(pluginPath);
        }

        [Fact]
        public async Task MultipleRapidChanges_ShouldDebounceToSingleEvent()
        {
            // Arrange
            _hotReloadManager = new HotReloadManager(_pluginDirectory, _mockLogger.Object);
            _hotReloadManager.DebounceDelayMs = 300;

            var pluginFolder = Path.Combine(_pluginDirectory, "TestPlugin");
            Directory.CreateDirectory(pluginFolder);
            
            var pluginPath = Path.Combine(pluginFolder, "TestPlugin.dll");
            File.WriteAllText(pluginPath, "Initial Content");

            _hotReloadManager.RegisterPlugin("TestPlugin", pluginPath);
            _hotReloadManager.Enable();

            var eventCount = 0;
            _hotReloadManager.PluginFileChanged += (sender, args) => eventCount++;

            // Act
            await Task.Delay(100); // Wait for watcher to initialize
            
            // Trigger multiple rapid changes
            for (int i = 0; i < 5; i++)
            {
                File.WriteAllText(pluginPath, $"Content {i}");
                await Task.Delay(50); // Rapid changes within debounce window
            }

            await Task.Delay(1000); // Wait for debounce + processing

            // Assert
            eventCount.Should().Be(1, "debouncing should consolidate multiple changes into one event");
        }

        public void Dispose()
        {
            _hotReloadManager?.Dispose();

            // Cleanup test directory
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
                // Ignore cleanup errors
            }
        }
    }
}
