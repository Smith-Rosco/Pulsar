using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Tests.Plugins.Core
{
    public class WinSwitcherPluginBehaviorTests
    {
        [Fact]
        public void SettingsDefinition_ShouldDescribeExcludeProcessesAsDiscoveryOnly()
        {
            var plugin = new WinSwitcherPlugin();

            var setting = plugin.GetSettingsDefinition().Single(definition => definition.Key == "ExcludeProcesses");

            setting.Label.Should().Be("Discovery Blacklist");
            setting.Description.Should().Contain("automatic window discovery");
            setting.Description.Should().Contain("still target those processes when selected directly");
        }

        [Fact]
        public void SettingsDefinition_ShouldExposeExcludeRules()
        {
            var plugin = new WinSwitcherPlugin();

            var setting = plugin.GetSettingsDefinition().Single(definition => definition.Key == "ExcludeRules");

            setting.Type.Should().Be(PluginSettingType.String);
            setting.Description.Should().Contain("JSON");
        }

        [Fact]
        public void SettingsDefinition_ShouldExposeSwitchDiagnostics()
        {
            var plugin = new WinSwitcherPlugin();

            var setting = plugin.GetSettingsDefinition().Single(definition => definition.Key == "EnableSwitchDiagnostics");

            setting.Type.Should().Be(PluginSettingType.Boolean);
            setting.DefaultValue.Should().Be(false);
        }

        [Fact]
        public void MetadataSchema_ShouldAllowSwitchDiagnostics()
        {
            var plugin = new WinSwitcherPlugin();

            var property = plugin.GetMetadata().Schema!.Properties["EnableSwitchDiagnostics"];

            property.Type.Should().Be("bool");
            property.DefaultValue.Should().Be(false);
        }

        [Fact]
        public async Task ConfigValidation_ShouldAllowPersistedSwitchDiagnostics()
        {
            var plugin = new WinSwitcherPlugin();
            var metadataRegistry = new PluginMetadataRegistry(Mock.Of<ILogger<PluginMetadataRegistry>>());
            metadataRegistry.Register(plugin.GetMetadata());
            var pipeline = new ConfigValidationPipeline(
                Mock.Of<IPluginRegistry>(),
                metadataRegistry,
                Mock.Of<ILogger<ConfigValidationPipeline>>());
            var config = new ProfilesConfig();
            config.Plugins[plugin.Id] = new PluginProfile
            {
                Config = new Dictionary<string, object>
                {
                    ["EnableSwitchDiagnostics"] = true
                }
            };

            var result = await pipeline.ValidateAsync(config);

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void UpdateSettings_ShouldToggleSwitchDiagnostics()
        {
            var (plugin, windowService, _) = CreateInitializedPlugin();

            plugin.UpdateSettings(new Dictionary<string, object> { ["EnableSwitchDiagnostics"] = true });

            windowService.Verify(s => s.SetSwitchDiagnosticsEnabled(true), Times.Once);
        }

        [Fact]
        public void Metadata_ShouldDescribeExcludeProcessesAsDiscoveryOnly()
        {
            var plugin = new WinSwitcherPlugin();

            var metadata = plugin.GetMetadata();
            metadata.Schema.Should().NotBeNull();
            var property = metadata.Schema!.Properties["ExcludeProcesses"];

            property.Description.Should().Contain("excluded from discovery lists only");
            property.Description.Should().Contain("direct activate and switch actions still target them");
        }

        [Fact]
        public void UpdateSettings_WithExcludeRules_ShouldPushParsedRulesToWindowService()
        {
            var (plugin, windowService, _) = CreateInitializedPlugin();

            plugin.UpdateSettings(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "[{\"Allow\":false,\"WindowClass\":\"GhostClass\"}]"
            });

            windowService.Verify(s => s.UpdateEligibilityRules(It.Is<IReadOnlyList<WindowEligibilityRule>>(rules =>
                rules.Count == 1 && rules[0].WindowClass == "GhostClass")), Times.Once);
        }

        [Fact]
        public void ValidateSettings_InvalidExcludeRulesJson_ShouldFail()
        {
            var plugin = new WinSwitcherPlugin();

            var result = plugin.ValidateSettings(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "{ not json"
            });

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void ValidateSettings_ValidExcludeRulesJson_ShouldPass()
        {
            var plugin = new WinSwitcherPlugin();

            var result = plugin.ValidateSettings(new Dictionary<string, object>
            {
                ["ExcludeRules"] = "[{\"Allow\":false,\"WindowClass\":\"GhostClass\"}]"
            });

            result.IsValid.Should().BeTrue();
        }

        [Fact]
        public void Initialize_WithoutProcessLauncher_ShouldThrow()
        {
            var windowService = new Mock<IWindowService>();
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IWindowService))).Returns(windowService.Object);

            var act = () => new WinSwitcherPlugin().Initialize(services.Object);

            act.Should().Throw<InvalidOperationException>()
                .WithMessage("*IProcessLauncher*");
        }

        [Fact]
        public async Task Launch_MissingPath_ShouldReturnRecoverableMissingParameter()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();

            var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string>(), PulsarContextStub);

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.ErrorCode.Should().Be(PluginErrorCode.MissingRequiredParameter);
            launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task Launch_RelativePath_ShouldReturnInvalidConfiguration()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();

            var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = "notepad.exe" }, PulsarContextStub);

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.ErrorCode.Should().Be(PluginErrorCode.InvalidConfiguration);
            launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task Launch_FileNotFound_ShouldReturnNotFound()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var missingPath = Path.Combine(Path.GetTempPath(), $"pulsar-winswitcher-missing-{Guid.NewGuid():N}.exe");

            var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = missingPath }, PulsarContextStub);

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.ErrorCode.Should().Be(PluginErrorCode.NotFound);
            launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task Launch_UnsupportedExtension_ShouldReturnInvalidConfiguration()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var txtPath = CreateTempLaunchFile(".txt");
            try
            {
                var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = txtPath }, PulsarContextStub);

                result.Success.Should().BeFalse();
                result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
                result.ErrorCode.Should().Be(PluginErrorCode.InvalidConfiguration);
                launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
            }
            finally
            {
                File.Delete(txtPath);
            }
        }

        [Fact]
        public async Task Launch_ValidFile_ShouldStartProcessThroughLauncher()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var exePath = CreateTempLaunchFile(".exe");
            try
            {
                var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string>
                {
                    ["path"] = exePath,
                    ["arguments"] = "--flag value"
                }, PulsarContextStub);

                result.Success.Should().BeTrue();
                result.Message.Should().Contain(Path.GetFileName(exePath));
                launcher.Verify(l => l.Launch(It.Is<ProcessStartInfo>(startInfo =>
                    startInfo.FileName == exePath &&
                    startInfo.Arguments == "--flag value" &&
                    startInfo.UseShellExecute &&
                    startInfo.WindowStyle == ProcessWindowStyle.Normal)), Times.Once);
            }
            finally
            {
                File.Delete(exePath);
            }
        }

        [Fact]
        public async Task Launch_WhenLauncherThrowsFileNotFound_ShouldMapToRecoverableNotFound()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var exePath = CreateTempLaunchFile(".exe");
            launcher.Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Throws(new FileNotFoundException("gone", exePath));
            try
            {
                var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = exePath }, PulsarContextStub);

                result.Success.Should().BeFalse();
                result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
                result.ErrorCode.Should().Be(PluginErrorCode.NotFound);
            }
            finally
            {
                File.Delete(exePath);
            }
        }

        [Fact]
        public async Task Launch_WhenLauncherThrowsUnauthorizedAccess_ShouldMapToCriticalAccessDenied()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var exePath = CreateTempLaunchFile(".exe");
            launcher.Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Throws(new UnauthorizedAccessException("denied"));
            try
            {
                var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = exePath }, PulsarContextStub);

                result.Success.Should().BeFalse();
                result.Severity.Should().Be(PluginErrorSeverity.Critical);
                result.ErrorCode.Should().Be(PluginErrorCode.AccessDenied);
            }
            finally
            {
                File.Delete(exePath);
            }
        }

        [Fact]
        public async Task Launch_WhenLauncherThrowsWin32Exception_ShouldMapToRecoverableExecutionFailed()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var exePath = CreateTempLaunchFile(".exe");
            launcher.Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Throws(new Win32Exception(2, "cannot find the file"));
            try
            {
                var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = exePath }, PulsarContextStub);

                result.Success.Should().BeFalse();
                result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
                result.ErrorCode.Should().Be(PluginErrorCode.ExecutionFailed);
            }
            finally
            {
                File.Delete(exePath);
            }
        }

        [Fact]
        public async Task Launch_WhenLauncherThrowsUnexpected_ShouldMapToCriticalExecutionFailed()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();
            var exePath = CreateTempLaunchFile(".exe");
            launcher.Setup(l => l.Launch(It.IsAny<ProcessStartInfo>()))
                .Throws(new InvalidOperationException("boom"));
            try
            {
                var result = await plugin.ExecuteAsync("launch", new Dictionary<string, string> { ["path"] = exePath }, PulsarContextStub);

                result.Success.Should().BeFalse();
                result.Severity.Should().Be(PluginErrorSeverity.Critical);
                result.ErrorCode.Should().Be(PluginErrorCode.ExecutionFailed);
            }
            finally
            {
                File.Delete(exePath);
            }
        }

        [Fact]
        public async Task Switch_Success_ShouldNotLaunch()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin(switchSucceeded: true);

            var result = await plugin.ExecuteAsync("switch", new Dictionary<string, string>
            {
                ["app"] = "chrome",
                ["path"] = @"C:\some\chrome.exe"
            }, PulsarContextStub);

            result.Success.Should().BeTrue();
            launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task Switch_FailWithPath_ShouldLaunchThroughLauncher()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin(switchSucceeded: false);
            var exePath = CreateTempLaunchFile(".exe");
            try
            {
                var result = await plugin.ExecuteAsync("switch", new Dictionary<string, string>
                {
                    ["app"] = "chrome",
                    ["path"] = exePath
                }, PulsarContextStub);

                result.Success.Should().BeTrue();
                launcher.Verify(l => l.Launch(It.Is<ProcessStartInfo>(startInfo => startInfo.FileName == exePath)), Times.Once);
            }
            finally
            {
                File.Delete(exePath);
            }
        }

        [Fact]
        public async Task Switch_FailWithoutPath_ShouldReturnNotFound()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin(switchSucceeded: false);

            var result = await plugin.ExecuteAsync("switch", new Dictionary<string, string> { ["app"] = "chrome" }, PulsarContextStub);

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.ErrorCode.Should().Be(PluginErrorCode.NotFound);
            launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task Switch_MissingApp_ShouldReturnMissingParameter()
        {
            var (plugin, _, launcher) = CreateInitializedPlugin();

            var result = await plugin.ExecuteAsync("switch", new Dictionary<string, string>(), PulsarContextStub);

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.ErrorCode.Should().Be(PluginErrorCode.MissingRequiredParameter);
            launcher.Verify(l => l.Launch(It.IsAny<ProcessStartInfo>()), Times.Never);
        }

        [Fact]
        public async Task Activate_Fail_ShouldReturnNotFound()
        {
            var (plugin, _, _) = CreateInitializedPlugin(switchSucceeded: false);

            var result = await plugin.ExecuteAsync("activate", new Dictionary<string, string> { ["app"] = "chrome" }, PulsarContextStub);

            result.Success.Should().BeFalse();
            result.Severity.Should().Be(PluginErrorSeverity.Recoverable);
            result.ErrorCode.Should().Be(PluginErrorCode.NotFound);
        }

        private static PulsarContext PulsarContextStub => TestHelpers.PulsarContextFactory.CreateTestContext();

        private static (WinSwitcherPlugin Plugin, Mock<IWindowService> WindowService, Mock<IProcessLauncher> Launcher) CreateInitializedPlugin(
            bool switchSucceeded = false)
        {
            var windowService = new Mock<IWindowService>();
            windowService.Setup(s => s.SwitchToProcessAsync(It.IsAny<string>())).ReturnsAsync(switchSucceeded);
            var launcher = new Mock<IProcessLauncher>();
            var services = new Mock<IServiceProvider>();
            services.Setup(s => s.GetService(typeof(IWindowService))).Returns(windowService.Object);
            services.Setup(s => s.GetService(typeof(IProcessLauncher))).Returns(launcher.Object);

            var plugin = new WinSwitcherPlugin();
            plugin.Initialize(services.Object);
            return (plugin, windowService, launcher);
        }

        private static string CreateTempLaunchFile(string extension)
        {
            var path = Path.Combine(Path.GetTempPath(), $"pulsar-winswitcher-test-{Guid.NewGuid():N}{extension}");
            File.WriteAllText(path, "pulsar test fixture");
            return path;
        }
    }
}
