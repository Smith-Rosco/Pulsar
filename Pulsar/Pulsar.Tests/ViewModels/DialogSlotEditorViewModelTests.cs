using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.ViewModels.Dialogs;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class DialogSlotEditorViewModelTests
    {
        private static ILocalizationService CreateLoc()
        {
            var loc = new LocalizationService(new Mock<ILogger<LocalizationService>>().Object);
            return loc;
        }
        [Fact]
        public void AddSlotViewModel_ShouldExposeSingleSurfaceCopyAndPreviewMetadata()
        {
            var viewModel = CreateAddSlotViewModel();

            viewModel.PrimaryButtonText.Should().Be("Save Slot");
            viewModel.SecondaryButtonText.Should().Be("Cancel");
            viewModel.IsAwaitingPluginSelection.Should().BeTrue();
            viewModel.IsScenarioMode.Should().BeTrue();
            viewModel.HeaderDescription.Should().Contain("Start with what you want to do");

            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions[1]);

            viewModel.HasSelectedPlugin.Should().BeTrue();
            viewModel.HasAppearanceOptions.Should().BeTrue();
            viewModel.PreviewMetadataText.Should().Contain("Action: Open Target");
            viewModel.PreviewMetadataText.Should().Contain("Path: missing");
        }

        [Fact]
        public async Task AddSlotViewModel_ShouldKeepSaveFlowAndRefreshPreviewMetadataAfterParameterPick()
        {
            var viewModel = CreateAddSlotViewModel();
            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions[1]);
            var field = viewModel.RequiredParameters.Single();

            await viewModel.PickParameterValueAsync(field);

            viewModel.PreviewMetadataText.Should().Contain("Path: selected");
            viewModel.BlockingIssueText.Should().BeEmpty();
            viewModel.PrimaryCommand.Should().BeSameAs(viewModel.SaveCommand);
        }

        [Fact]
        public void AddSlotViewModel_ShouldPreserveCanonicalMetadataAndOptionalColor()
        {
            var viewModel = CreateAddSlotViewModel();

            var commandPlugin = viewModel.PluginTypes.Single(option => option.PluginId == "com.pulsar.command");

            commandPlugin.IconKey.Should().Be("E756");
            commandPlugin.DisplayName.Should().Be("Command Runner");

            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions[1]);

            viewModel.Slot.Should().NotBeNull();
            viewModel.Slot!.IconKey.Should().Be("E756");
            viewModel.Slot.Color.Should().BeEmpty();
        }

        [Fact]
        public void AddSlotViewModel_ShouldMapScenarioSelectionsToCanonicalPluginActions()
        {
            var viewModel = CreateAddSlotViewModel();

            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions.Single(option => option.Key == "switch-app"));
            viewModel.Slot!.PluginId.Should().Be("com.pulsar.winswitcher");
            viewModel.Slot.Action.Should().Be("switch");

            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions.Single(option => option.Key == "open-target"));
            viewModel.Slot!.PluginId.Should().Be("com.pulsar.command");
            viewModel.Slot.Action.Should().Be("run");

            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions.Single(option => option.Key == "send-keys"));
            viewModel.Slot!.PluginId.Should().Be("com.pulsar.command");
            viewModel.Slot.Action.Should().Be("sendkeys");

            viewModel.SelectScenarioCommand.Execute(viewModel.ScenarioOptions.Single(option => option.Key == "fill-credential"));
            viewModel.Slot!.PluginId.Should().Be("com.pulsar.pki");
            viewModel.Slot.Action.Should().Be("fill");
        }

        [Fact]
        public void AddSlotViewModel_ShouldAllowSwitchingToAdvancedPluginFirstFlow()
        {
            var viewModel = CreateAddSlotViewModel();
            var commandPlugin = viewModel.PluginTypes.Single(option => option.PluginId == "com.pulsar.command");

            viewModel.ShowAdvancedFlowCommand.Execute(null);

            viewModel.IsAdvancedMode.Should().BeTrue();
            viewModel.IsScenarioMode.Should().BeFalse();
            viewModel.BlockingIssueText.Should().Be("Choose a slot type to begin.");

            viewModel.SelectPluginTypeCommand.Execute(commandPlugin);

            viewModel.Slot.Should().NotBeNull();
            viewModel.Slot!.PluginId.Should().Be("com.pulsar.command");
            viewModel.Slot.Action.Should().Be("run");
        }

        [Fact]
        public async Task SlotConfigurationDialogViewModel_ShouldExposePreviewMetadataAndAppearanceCopy()
        {
            var slot = CreateConfiguredSlot();
            var viewModel = new SlotConfigurationDialogViewModel(
                slot,
                CreateLoc(),
                (currentSlot, action) => currentSlot.Action = action ?? string.Empty,
                field =>
                {
                    field.Value = "updated-value";
                    RefreshConfiguredSlot(slot);
                    return Task.CompletedTask;
                },
                currentSlot =>
                {
                    currentSlot.IconKey = "E8A7";
                    return Task.CompletedTask;
                },
                currentSlot =>
                {
                    currentSlot.Color = "#123456";
                    return Task.CompletedTask;
                });

            viewModel.PreviewMetadataText.Should().Contain("Action: Fill");
            viewModel.PreviewMetadataText.Should().Contain("Secret: selected");
            viewModel.AppearanceDisclosureDescription.Should().Contain("suggested presentation");

            await viewModel.PickParameterValueAsync(viewModel.RequiredParameters.Single());

            viewModel.PreviewMetadataText.Should().Contain("Secret: selected");
        }

        private static AddSlotViewModel CreateAddSlotViewModel()
        {
            return new AddSlotViewModel(
                new[]
                {
                    new AddSlotViewModel.PluginTypeOption(
                        new BuiltInPluginDisplayModel(
                            "com.pulsar.winswitcher",
                            "E8AB",
                            "WinSwitcher",
                            "Switch to a running app window or launch it if needed.",
                            "switching",
                            "Switching",
                            "#4F8CFF")),
                    new AddSlotViewModel.PluginTypeOption(
                        new BuiltInPluginDisplayModel(
                            "com.pulsar.command",
                            "E756",
                            "Command Runner",
                            "Open apps, files, folders, or URLs, or send a key sequence.",
                            "automation",
                            "Automation",
                            "#32CD32"))
                },
                pluginId => CreateDraftSlot(pluginId),
                (slot, action) =>
                {
                    slot.Action = action ?? string.Empty;
                    RefreshSlot(slot);
                },
                field =>
                {
                    field.Value = string.Equals(field.Metadata.Key, "secretId", StringComparison.OrdinalIgnoreCase)
                        ? Guid.NewGuid().ToString()
                        : "selected.exe";
                    RefreshSlot(field.Slot);
                    return Task.CompletedTask;
                },
                slot =>
                {
                    slot.IconKey = "E8A7";
                    return Task.CompletedTask;
                },
                slot =>
                {
                    slot.Color = "#32CD32";
                    return Task.CompletedTask;
                },
                CreateLoc());
        }

        private static PluginSlot CreateDraftSlot(string pluginId)
        {
            var slot = new PluginSlot
            {
                Slot = 3,
                PluginId = pluginId,
                Color = string.Empty,
                Args = new Dictionary<string, string>()
            };

            foreach (var action in BuildActions(pluginId))
            {
                slot.AvailableActions.Add(action);
            }

            foreach (var parameter in BuildRequiredParameters(slot, pluginId))
            {
                slot.RequiredParameters.Add(parameter);
            }

            slot.Action = slot.AvailableActions.First().Value;
            slot.Label = slot.Action switch
            {
                "switch" => "Switch Or Launch App",
                "sendkeys" => "Send Keys",
                "fill" => "Fill Secret",
                _ => "Open Target"
            };
            slot.IconKey = pluginId switch
            {
                "com.pulsar.winswitcher" => "E8AB",
                "com.pulsar.pki" => "E72E",
                _ => string.Equals(slot.Action, "sendkeys", StringComparison.OrdinalIgnoreCase) ? "E765" : "E756"
            };

            RefreshSlot(slot);
            return slot;
        }

        private static IEnumerable<SlotActionOption> BuildActions(string pluginId)
        {
            if (string.Equals(pluginId, "com.pulsar.winswitcher", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SlotActionOption
                {
                    Value = "switch",
                    Label = "Switch Or Launch",
                    Description = "Switch to a running app or launch it.",
                    IsSelected = true
                };
                yield break;
            }

            if (string.Equals(pluginId, "com.pulsar.pki", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SlotActionOption
                {
                    Value = "fill",
                    Label = "Fill",
                    Description = "Fill a saved credential.",
                    IsSelected = true
                };
                yield break;
            }

            yield return new SlotActionOption
            {
                Value = "run",
                Label = "Open Target",
                Description = "Open a path or URL.",
                IsSelected = true
            };
            yield return new SlotActionOption
            {
                Value = "sendkeys",
                Label = "Send Keys",
                Description = "Send a key sequence.",
                IsSelected = false
            };
        }

        private static IEnumerable<SlotParameterEditorField> BuildRequiredParameters(PluginSlot slot, string pluginId)
        {
            if (string.Equals(pluginId, "com.pulsar.winswitcher", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SlotParameterEditorField(slot, new Pulsar.Core.Plugin.Metadata.SlotParameterMetadata
                {
                    Key = "app",
                    Type = "string",
                    Label = "Process Name",
                    IsRequired = true,
                    SummaryLabel = "App",
                    SummaryMode = Pulsar.Core.Plugin.Metadata.SlotParameterSummaryMode.SafeStateOnly,
                    ConfiguredSummaryText = "selected",
                    MissingSummaryText = "missing"
                });
                yield break;
            }

            if (string.Equals(pluginId, "com.pulsar.pki", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SlotParameterEditorField(slot, new Pulsar.Core.Plugin.Metadata.SlotParameterMetadata
                {
                    Key = "secretId",
                    Type = "guid",
                    Label = "Secret",
                    IsRequired = true,
                    SummaryLabel = "Secret",
                    SummaryMode = Pulsar.Core.Plugin.Metadata.SlotParameterSummaryMode.SafeStateOnly,
                    ConfiguredSummaryText = "selected",
                    MissingSummaryText = "missing",
                    PickerIntent = Pulsar.Core.Plugin.Metadata.SlotPickerIntent.Secret
                });
                yield break;
            }

            yield return new SlotParameterEditorField(slot, new Pulsar.Core.Plugin.Metadata.SlotParameterMetadata
            {
                Key = "path",
                Type = "string",
                Label = "Path",
                IsRequired = true,
                SummaryLabel = "Path",
                SummaryMode = Pulsar.Core.Plugin.Metadata.SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = "configured",
                MissingSummaryText = "missing"
            });
        }

        private static PluginSlot CreateConfiguredSlot()
        {
            var slot = new PluginSlot
            {
                Slot = 2,
                PluginId = "com.pulsar.pki",
                Action = "fill",
                Label = "Fill Secret",
                IconKey = "E72E",
                Color = "#4CAF50",
                Args = new Dictionary<string, string>()
            };

            slot.AvailableActions.Add(new SlotActionOption
            {
                Value = "fill",
                Label = "Fill",
                Description = "Fill a saved secret.",
                IsSelected = true
            });

            slot.RequiredParameters.Add(new SlotParameterEditorField(slot, new Pulsar.Core.Plugin.Metadata.SlotParameterMetadata
            {
                Key = "secretId",
                Type = "guid",
                Label = "Secret",
                IsRequired = true,
                SummaryLabel = "Secret",
                SummaryMode = Pulsar.Core.Plugin.Metadata.SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = "selected",
                MissingSummaryText = "missing",
                PickerIntent = Pulsar.Core.Plugin.Metadata.SlotPickerIntent.Secret
            }));

            slot.SetArgument("secretId", Guid.NewGuid().ToString());
            RefreshConfiguredSlot(slot);
            return slot;
        }

        private static void RefreshSlot(PluginSlot slot)
        {
            var selectedAction = slot.AvailableActions.FirstOrDefault(option => option.IsSelected || option.Value == slot.Action);
            slot.ActionLabel = selectedAction?.Label ?? slot.Action;
            slot.ActionDescription = selectedAction?.Description ?? string.Empty;

            var summaryField = slot.RequiredParameters.First();
            string rawValue = summaryField.Value;
            string state = string.IsNullOrWhiteSpace(rawValue)
                ? "missing"
                : string.Equals(summaryField.Metadata.Key, "secretId", StringComparison.OrdinalIgnoreCase)
                    ? "selected"
                    : rawValue;
            slot.ActionLabel = slot.AvailableActions.FirstOrDefault(option => option.IsSelected || option.Value == slot.Action)?.Label ?? slot.Action;
            slot.SummaryTokens = new System.Collections.ObjectModel.ObservableCollection<string>(new[]
            {
                $"Action: {slot.ActionLabel}",
                $"{summaryField.SummaryLabel}: {state}"
            });
            slot.ValidationSummary = state == "missing" ? "Complete the required field." : string.Empty;
            slot.SetValidationSummary(slot.ValidationSummary);
            slot.SetPresentation(SlotPresentationBuilder.Build(slot));
        }

        private static void RefreshConfiguredSlot(PluginSlot slot)
        {
            slot.RequiredParameters.First().Value = slot.RequiredParameters.First().Value == "updated-value"
                ? "updated-value"
                : Guid.NewGuid().ToString();
            RefreshSlot(slot);
        }
    }
}
