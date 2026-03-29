using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Pulsar.ViewModels;

namespace Pulsar.Tests.ViewModels
{
    public class SettingsViewModelDirtyStateTests
    {
        [Fact]
        public async Task LoadSettings_DoesNotSetDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            viewModel.HasUnsavedChanges.Should().BeFalse();
        }

        [Fact]
        public async Task SwitchContext_DoesNotSetDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var launcher = viewModel.AvailableContexts.Single(context => context.Key == "Launcher");
            var global = viewModel.AvailableContexts.Single(context => context.Key == "Global");

            viewModel.CurrentContext = launcher;
            viewModel.HasUnsavedChanges.Should().BeFalse();

            viewModel.CurrentContext = global;
            viewModel.HasUnsavedChanges.Should().BeFalse();
        }

        [Fact]
        public async Task CommitCreatedSlot_SetsDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var slot = new PluginSlot
            {
                PluginId = "com.pulsar.command",
                Action = string.Empty,
                Label = "Open Notes",
                IconKey = "E756",
                Color = "#32CD32",
                Args = new Dictionary<string, string>()
            };

            InvokePrivate(viewModel, "CommitCreatedSlot", slot);

            viewModel.HasUnsavedChanges.Should().BeTrue();
            viewModel.CurrentSlots.Should().Contain(slot);
        }

        [Fact]
        public async Task EditSlotLabel_SetsDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var slot = viewModel.CurrentSlots.Single();
            viewModel.HasUnsavedChanges.Should().BeFalse();

            slot.Label = "Renamed Slot";

            viewModel.HasUnsavedChanges.Should().BeTrue();
        }

        [Fact]
        public async Task CreateSlotDraft_DoesNotSetDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            var draftResult = InvokePrivate(viewModel, "CreateSlotDraft", "com.pulsar.command");
            draftResult.Should().BeOfType<PluginSlot>();
            var draft = (PluginSlot)draftResult!;

            InvokePrivate(viewModel, "SetSlotDraftAction", draft, "run");

            draft.IconKey.Should().Be("E756");
            draft.Color.Should().BeEmpty();
            viewModel.HasUnsavedChanges.Should().BeFalse();
            viewModel.CurrentSlots.Should().NotContain(draft);
        }

        [Fact]
        public async Task ChangingSlotsPerPage_SetsDirty()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            viewModel.GeneralSettings.SlotsPerPage = 10;

            viewModel.HasUnsavedChanges.Should().BeTrue();
        }

        [Fact]
        public async Task ResetConfig_UsesUnifiedResetPathAndReloadsRegeneratedDefaults()
        {
            EnsureApplication();
            var harness = CreateHarness();

            var fallbackConfig = CreateResetFallbackConfig();
            harness.ConfigService
                .Setup(service => service.ResetToFirstLaunchAsync())
                .ReturnsAsync(CloneConfig(fallbackConfig));
            harness.ConfigService
                .Setup(service => service.LoadAsync())
                .ReturnsAsync(() => CloneConfig(fallbackConfig));

            harness.DialogService
                .Setup(service => service.ShowConfirmationAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(DialogResult.Confirmed);

            var viewModel = harness.ViewModel;
            await WaitForInitializationAsync(viewModel);

            // Act
            await viewModel.ResetConfig();

            // Assert
            harness.ConfigService.Verify(service => service.ResetToFirstLaunchAsync(), Times.Once);
            harness.ConfigService.Verify(service => service.SaveAsync(It.IsAny<ProfilesConfig>()), Times.Never);

            var reloadedConfig = await viewModel.GetConfigAsync();
            reloadedConfig.Profiles.Should().ContainKey("Global");
            reloadedConfig.Profiles["Global"].SwitchMode.Should().NotBeEmpty();
            reloadedConfig.Settings.HasCompletedTutorial.Should().BeFalse();
            reloadedConfig.Settings.LastTutorialStep.Should().BeNull();
            reloadedConfig.Settings.HasCompletedInitialDetection.Should().BeFalse();
        }

        private static async Task WaitForInitializationAsync(SettingsViewModel viewModel)
        {
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

        private static object? InvokePrivate(object instance, string methodName, params object[] arguments)
        {
            var method = instance.GetType().GetMethod(methodName, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
            method.Should().NotBeNull($"{methodName} should exist on {instance.GetType().Name}");
            return method!.Invoke(instance, arguments);
        }

        private static void EnsureApplication()
        {
            if (Application.Current == null)
            {
                _ = new Application();
            }
        }

        private static SettingsViewModelHarness CreateHarness()
        {
            var config = CreateConfig();

            var configService = new Mock<IConfigService>();
            configService.SetupGet(service => service.Current).Returns(config);
            configService.SetupGet(service => service.LastValidationResult).Returns((ValidationResult?)null);
            configService.Setup(service => service.LoadAsync()).ReturnsAsync(CloneConfig(config));
            configService.Setup(service => service.SaveAsync(It.IsAny<ProfilesConfig>())).Returns(Task.CompletedTask);
            configService.Setup(service => service.GetValidatedSlotsPerPage()).Returns(() => config.Settings.SlotsPerPage);

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

            var processRegistryService = new Mock<IProcessRegistryService>();
            processRegistryService.Setup(service => service.GetCacheStatisticsAsync())
                .ReturnsAsync(new CacheStatistics { TotalProcesses = 1 });

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
            pluginMetadataRegistry.Register(CreateCommandMetadata());
            pluginMetadataRegistry.Register(CreateWinSwitcherMetadata());

            var viewModel = new SettingsViewModel(
                configService.Object,
                new Mock<IWindowService>().Object,
                CreateThemeServiceMock().Object,
                new Mock<IHotkeyService>().Object,
                dialogService.Object,
                new Mock<IFuzzySearchService<IconItem>>().Object,
                secretStore.Object,
                new Mock<ISecretProtector>().Object,
                secretMetadataResolver.Object,
                pluginMetadataRegistry,
                NullLogger<SettingsViewModel>.Instance,
                processRegistryService.Object);

            return new SettingsViewModelHarness(viewModel, configService, dialogService);
        }

        private static Mock<IThemeService> CreateThemeServiceMock()
        {
            var themeService = new Mock<IThemeService>();
            themeService.SetupGet(service => service.CurrentTheme).Returns(AppTheme.Light);
            return themeService;
        }

        private static ProfilesConfig CreateConfig()
        {
            return new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    SlotsPerPage = 8,
                    LauncherTheme = "Light",
                    SettingsTheme = "Light"
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        CommandMode = new List<PluginSlot>
                        {
                            CreateSlot(1, "Global Command")
                        },
                        SwitchMode = new List<PluginSlot>
                        {
                            CreateSlot(1, "Launcher Command")
                        }
                    },
                    ["notepad"] = new ProcessProfile
                    {
                        Alias = "Notepad",
                        CommandMode = new List<PluginSlot>
                        {
                            CreateSlot(1, "Profile Command")
                        }
                    }
                }
            };
        }

        private static PluginSlot CreateSlot(int slotNumber, string label)
        {
            return new PluginSlot
            {
                Slot = slotNumber,
                PluginId = "com.pulsar.command",
                Action = "run",
                Label = label,
                IconKey = "E756",
                Color = "#32CD32",
                Args = new Dictionary<string, string>
                {
                    ["path"] = "notepad.exe"
                }
            };
        }

        private static ProfilesConfig CloneConfig(ProfilesConfig config)
        {
            var options = new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                WriteIndented = true
            };

            var json = System.Text.Json.JsonSerializer.Serialize(config, options);
            return System.Text.Json.JsonSerializer.Deserialize<ProfilesConfig>(json, options) ?? new ProfilesConfig();
        }

        private static ProfilesConfig CreateResetFallbackConfig()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    HasCompletedTutorial = false,
                    LastTutorialStep = null,
                    HasCompletedInitialDetection = false,
                    LauncherTheme = "Light",
                    SettingsTheme = "Light",
                    SlotsPerPage = 8
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new List<PluginSlot>
                        {
                            CreateSlot(1, "Notepad")
                        },
                        CommandMode = new List<PluginSlot>
                        {
                            CreateSlot(1, "Command Prompt")
                        }
                    }
                }
            };

            return config;
        }

        private static PluginMetadata CreateCommandMetadata()
        {
            return new PluginMetadata
            {
                Id = "com.pulsar.command",
                Display = new DisplayInfo
                {
                    Name = "Command Runner",
                    Description = "Open apps, files, folders, or URLs.",
                    IconKey = "E756",
                    Category = "Automation",
                    Version = "1.0.0",
                    Author = "Tests",
                    License = "MIT"
                },
                UI = new UIHints
                {
                    Badge = "Cmd",
                    AccentColor = "#32CD32",
                    ShowInQuickAccess = true,
                    SortOrder = 1,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "run" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = true,
                    Tier = PluginTier.Extension,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["run"] = new SlotActionMetadata
                    {
                        Name = "run",
                        Label = "Open Target",
                        Description = "Open a path or URL.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "path",
                                Type = "string",
                                Label = "Path",
                                IsRequired = true,
                                SummaryLabel = "Path",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "selected",
                                MissingSummaryText = "missing"
                            }
                        }
                    }
                }
            };
        }

        private static PluginMetadata CreateWinSwitcherMetadata()
        {
            return new PluginMetadata
            {
                Id = "com.pulsar.winswitcher",
                Display = new DisplayInfo
                {
                    Name = "App Switcher",
                    Description = "Switch to an existing app, launch one directly, or switch first and launch only when needed.",
                    IconKey = "E8A7",
                    Category = "Apps",
                    Version = "1.0.0",
                    Author = "Tests",
                    License = "MIT"
                },
                UI = new UIHints
                {
                    Badge = "App",
                    AccentColor = "#2196F3",
                    ShowInQuickAccess = true,
                    SortOrder = 2,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "switch" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = false,
                    Tier = PluginTier.Core,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["switch"] = new SlotActionMetadata
                    {
                        Name = "switch",
                        Label = "Switch Or Launch",
                        Description = "Switch to a running app window, or launch it when no matching window is found."
                    }
                }
            };
        }

        private sealed record SettingsViewModelHarness(
            SettingsViewModel ViewModel,
            Mock<IConfigService> ConfigService,
            Mock<IDialogService> DialogService);
    }
}
