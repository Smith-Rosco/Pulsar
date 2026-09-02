using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.WinSwitcher;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Regression tests for the settings save pipeline backed by a REAL
    /// ConfigService (temp file), so optimistic-concurrency revision semantics
    /// behave exactly as in production.
    /// </summary>
    public class SettingsSaveSessionTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;

        public SettingsSaveSessionTests()
        {
            _testDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            Directory.CreateDirectory(_testDirectory);
            _configPath = Path.Combine(_testDirectory, "Profiles.json");
        }

        public void Dispose()
        {
            try
            {
                if (Directory.Exists(_testDirectory))
                {
                    Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }

        [Fact]
        public async Task Save_SecondConsecutiveSave_ShouldAlsoPersist()
        {
            EnsureApplication();
            var harness = CreateHarness();
            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            viewModel.GeneralSettings.SlotsPerPage = 10;
            await viewModel.Save();
            viewModel.HasUnsavedChanges.Should().BeFalse("the first save should succeed");

            viewModel.GeneralSettings.SlotsPerPage = 6;
            await viewModel.Save();

            viewModel.HasUnsavedChanges.Should().BeFalse(
                "the second save must also succeed; a stale edit-session revision must not block subsequent saves");

            var reloaded = await LoadFromDiskAsync();
            reloaded.Settings.SlotsPerPage.Should().Be(6);
        }

        [Fact]
        public async Task Save_AfterExternalWriterCommitted_ShouldPersistUserEdits_AndKeepExternalChanges()
        {
            EnsureApplication();
            var harness = CreateHarness();
            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var externalSession = await ConfigEditSession.BeginAsync(harness.ConfigService);
            externalSession.Draft.Plugins["com.pulsar.winswitcher"] = new PluginProfile
            {
                Config = new Dictionary<string, object>
                {
                    ["ExcludeProcesses"] = "chrome"
                }
            };
            await externalSession.CommitAsync();

            var slot = viewModel.CurrentSlots.Single();
            slot.Label = "Edited Label";

            await viewModel.Save();

            viewModel.HasUnsavedChanges.Should().BeFalse(
                "a background writer committing while the settings window is open must not make the user's save fail");

            var reloaded = await LoadFromDiskAsync();
            var reloadedSlot = reloaded.Profiles["Global"].SwitchMode.Single();
            reloadedSlot.Label.Should().Be("Edited Label");
            reloaded.Plugins.Should().ContainKey("com.pulsar.winswitcher");
            reloaded.Plugins["com.pulsar.winswitcher"].Config.Should().ContainKey("ExcludeProcesses",
                "the external writer's change must survive the settings editor's commit");
        }

        [Fact]
        public async Task SelectingActionOption_ShouldUpdateSlotAction_AndMarkDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();
            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var slot = viewModel.CurrentSlots.Single();
            slot.Action.Should().Be("switch");
            viewModel.HasUnsavedChanges.Should().BeFalse();

            var activateOption = slot.AvailableActions.Single(option => option.Value == "activate");
            activateOption.IsSelected = true;

            slot.Action.Should().Be("activate",
                "selecting an action option (as the segmented RadioButton does) must apply the action to the slot");
            viewModel.HasUnsavedChanges.Should().BeTrue(
                "changing the slot action must mark the settings editor as dirty");
        }

        [Fact]
        public async Task SelectingActionOption_ThenSave_ShouldPersistNewAction()
        {
            EnsureApplication();
            var harness = CreateHarness();
            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var slot = viewModel.CurrentSlots.Single();
            var activateOption = slot.AvailableActions.Single(option => option.Value == "activate");
            activateOption.IsSelected = true;

            await viewModel.Save();

            viewModel.HasUnsavedChanges.Should().BeFalse();
            var reloaded = await LoadFromDiskAsync();
            reloaded.Profiles["Global"].SwitchMode.Single().Action.Should().Be("activate",
                "the App Launcher slot behavior chosen in the editor must be the behavior persisted to disk");
        }

        [Fact]
        public async Task Save_RendererStyleAndThemePreset_ShouldPersistWithoutRevertingProfiles()
        {
            EnsureApplication();
            var harness = CreateHarness();
            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            viewModel.RendererStyle = "ClassicRing";
            viewModel.ThemePreset = "MatchaForest";
            viewModel.HasUnsavedChanges.Should().BeTrue();

            await viewModel.Save();

            viewModel.HasUnsavedChanges.Should().BeFalse();
            var reloaded = await LoadFromDiskAsync();
            reloaded.Settings.RadialRenderer.Should().Be("ClassicRing",
                "the renderer style selector must persist to Profiles.json");
            reloaded.Settings.RadialThemePreset.Should().Be("MatchaForest",
                "the theme preset selector must persist to Profiles.json");
        }

        [Fact]
        public async Task Save_RendererSelectorsSecondSave_ShouldNotRevertFirstChanges()
        {
            // Regression guard: a second consecutive save must not revert the first
            // selector change (stale-revision / stale-hotkey-cache overwrite).
            EnsureApplication();
            var harness = CreateHarness();
            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            viewModel.RendererStyle = "Glassmorphism";
            viewModel.ThemePreset = "GlacialIce";
            await viewModel.Save();
            viewModel.HasUnsavedChanges.Should().BeFalse();

            await viewModel.Save();
            viewModel.HasUnsavedChanges.Should().BeFalse(
                "the second save must succeed; a stale edit-session revision must not block or revert");

            var reloaded = await LoadFromDiskAsync();
            reloaded.Settings.RadialRenderer.Should().Be("Glassmorphism");
            reloaded.Settings.RadialThemePreset.Should().Be("GlacialIce");
        }

        private async Task<ProfilesConfig> LoadFromDiskAsync()
        {
            var freshService = new ConfigService(
                new Mock<ILogger<ConfigService>>().Object,
                configPath: _configPath);
            return await freshService.LoadAsync(forceReload: true);
        }

        private static async Task WaitForInitializationAsync(SettingsViewModel viewModel)
        {
            if (viewModel.AvailableContexts.Count == 0)
            {
                await viewModel.LoadSettings();
            }

            for (int attempt = 0; attempt < 50; attempt++)
            {
                if (viewModel.AvailableContexts.Count > 0 && viewModel.CurrentContext != null)
                {
                    return;
                }

                await Task.Delay(20);
            }

            throw new TimeoutException("SettingsViewModel did not finish initialization in time.");
        }

        private static void EnsureApplication()
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }
        }

        private SettingsViewModelHarness CreateHarness()
        {
            SeedConfigFile();

            var configService = new ConfigService(
                new Mock<ILogger<ConfigService>>().Object,
                configPath: _configPath);

            var dialogService = new Mock<IDialogService>();
            dialogService.Setup(service => service.ShowMessageAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<DialogType>(), It.IsAny<DialogButtons>()))
                .ReturnsAsync(DialogResult.Confirmed);
            dialogService.Setup(service => service.ShowCustomAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<DialogButtons>()))
                .ReturnsAsync(DialogResult.Cancelled);
            dialogService.Setup(service => service.ShowCustomAsync(It.IsAny<string>(), It.IsAny<object>(), It.IsAny<DialogButtons>(), It.IsAny<DialogSizeConstraints>()))
                .ReturnsAsync(DialogResult.Cancelled);
            dialogService.Setup(service => service.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Cancelled);
            dialogService.Setup(service => service.ShowInputAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);
            dialogService.Setup(service => service.ShowColorPickerAsync(It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync((string?)null);

            var secretStore = new Mock<IPkiSecretStore>();
            secretStore.Setup(store => store.LoadAsync()).ReturnsAsync(new Dictionary<Guid, SecretPayload>());
            secretStore.Setup(store => store.SaveAsync(It.IsAny<Dictionary<Guid, SecretPayload>>())).Returns(Task.CompletedTask);

            var secretMetadataResolver = new Mock<IPkiSecretMetadataResolver>();
            secretMetadataResolver
                .Setup(resolver => resolver.Resolve(It.IsAny<string?>(), It.IsAny<IReadOnlyDictionary<Guid, SecretPayload>?>(), It.IsAny<IReadOnlyDictionary<Guid, SecretPayload>?>(), It.IsAny<IReadOnlyDictionary<Guid, string>?>()))
                .Returns((SecretDisplayMetadata?)null);
            secretMetadataResolver
                .Setup(resolver => resolver.Resolve(It.IsAny<Guid>(), It.IsAny<IReadOnlyDictionary<Guid, SecretPayload>?>(), It.IsAny<IReadOnlyDictionary<Guid, SecretPayload>?>(), It.IsAny<IReadOnlyDictionary<Guid, string>?>()))
                .Returns((SecretDisplayMetadata?)null);
            secretMetadataResolver
                .Setup(resolver => resolver.Merge(It.IsAny<IReadOnlyDictionary<Guid, SecretPayload>?>(), It.IsAny<IReadOnlyDictionary<Guid, SecretPayload>?>()))
                .Returns(new Dictionary<Guid, SecretPayload>());

            var pluginMetadataRegistry = new PluginMetadataRegistry(NullLogger<PluginMetadataRegistry>.Instance);
            pluginMetadataRegistry.Register(new WinSwitcherPlugin().GetMetadata());

            var settingsShell = new SettingsShellViewModel(
                new SettingsPageCatalog(CreateLoc()),
                new Mock<ILocalUiPreferencesService>().Object,
                new Mock<ISettingsNavigationGuard>().Object,
                NullLogger<SettingsShellViewModel>.Instance);

            var themeService = new Mock<IThemeService>();
            themeService.SetupGet(service => service.CurrentTheme).Returns(AppTheme.Light);

            var viewModel = new SettingsViewModel(
                configService,
                new Mock<IWindowDiscoveryService>().Object,
                themeService.Object,
                new Mock<IHotkeyService>().Object,
                dialogService.Object,
                new Mock<IFuzzySearchService<IconItem>>().Object,
                secretStore.Object,
                new Mock<ISecretProtector>().Object,
                secretMetadataResolver.Object,
                pluginMetadataRegistry,
                settingsShell,
                NullLogger<SettingsViewModel>.Instance,
                CreateLoc(),
                new Mock<ITutorialService>().Object,
                new Mock<ILoggingConfigService>().Object,
                null);

            return new SettingsViewModelHarness(viewModel, configService, dialogService);
        }

        private void SeedConfigFile()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    SlotsPerPage = 8,
                    Theme = "Light"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        CommandMode = new List<PluginSlot>(),
                        SwitchMode = new List<PluginSlot>
                        {
                            new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "switch",
                                Label = "Chrome",
                                IconKey = "E8AB",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "chrome"
                                }
                            }
                        }
                    }
                }
            };

            var options = new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true
            };
            File.WriteAllText(_configPath, System.Text.Json.JsonSerializer.Serialize(config, options));
        }

        private static ILocalizationService CreateLoc()
        {
            return new LocalizationService(new Mock<ILogger<LocalizationService>>().Object);
        }

        private sealed record SettingsViewModelHarness(
            SettingsViewModel ViewModel,
            ConfigService ConfigService,
            Mock<IDialogService> DialogService);
    }
}
