using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.ViewModels.Dialogs;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    public class DialogSlotEditorViewModelTests
    {
        [Fact]
        public void AddSlotViewModel_ShouldExposeSingleSurfaceCopyAndPreviewMetadata()
        {
            var viewModel = CreateAddSlotViewModel();

            viewModel.PrimaryButtonText.Should().Be("Save Slot");
            viewModel.SecondaryButtonText.Should().Be("Cancel");
            viewModel.IsAwaitingPluginSelection.Should().BeTrue();
            viewModel.HeaderDescription.Should().Contain("Required setup stays in view");

            viewModel.SelectPluginTypeCommand.Execute(viewModel.PluginTypes[0]);

            viewModel.HasSelectedPlugin.Should().BeTrue();
            viewModel.HasAppearanceOptions.Should().BeTrue();
            viewModel.PreviewMetadataText.Should().Contain("Action: Open Target");
            viewModel.PreviewMetadataText.Should().Contain("Path: missing");
        }

        [Fact]
        public async Task AddSlotViewModel_ShouldKeepSaveFlowAndRefreshPreviewMetadataAfterParameterPick()
        {
            var viewModel = CreateAddSlotViewModel();
            viewModel.SelectPluginTypeCommand.Execute(viewModel.PluginTypes[0]);
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

            viewModel.PluginTypes[0].IconKey.Should().Be("E756");
            viewModel.PluginTypes[0].DisplayName.Should().Be("Command Runner");

            viewModel.SelectPluginTypeCommand.Execute(viewModel.PluginTypes[0]);

            viewModel.Slot.Should().NotBeNull();
            viewModel.Slot!.IconKey.Should().Be("E756");
            viewModel.Slot.Color.Should().BeEmpty();
        }

        [Fact]
        public async Task SlotConfigurationDialogViewModel_ShouldExposePreviewMetadataAndAppearanceCopy()
        {
            var slot = CreateConfiguredSlot();
            var viewModel = new SlotConfigurationDialogViewModel(
                slot,
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
                },
                _ => Task.CompletedTask);

            viewModel.PreviewMetadataText.Should().Contain("Action: Fill");
            viewModel.PreviewMetadataText.Should().Contain("Secret: selected");
            viewModel.AppearanceDisclosureDescription.Should().Contain("suggested presentation");

            await viewModel.PickParameterValueAsync(viewModel.RequiredParameters.Single());

            viewModel.PreviewMetadataText.Should().Contain("Secret: updated");
        }

        private static AddSlotViewModel CreateAddSlotViewModel()
        {
            return new AddSlotViewModel(
                new[]
                {
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
                _ => CreateDraftSlot(),
                (slot, action) =>
                {
                    slot.Action = action ?? string.Empty;
                    RefreshSlot(slot, slot["path"]);
                },
                field =>
                {
                    field.Value = "selected.exe";
                    RefreshSlot(field.Slot, field.Value);
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
                });
        }

        private static PluginSlot CreateDraftSlot()
        {
            var slot = new PluginSlot
            {
                Slot = 3,
                PluginId = "com.pulsar.command",
                Action = "run",
                Label = "Open Target",
                IconKey = "E756",
                Color = string.Empty,
                Args = new Dictionary<string, string>()
            };

            slot.AvailableActions.Add(new SlotActionOption
            {
                Value = "run",
                Label = "Open Target",
                Description = "Open a path or URL.",
                IsSelected = true
            });

            slot.RequiredParameters.Add(new SlotParameterEditorField(slot, new Pulsar.Core.Plugin.Metadata.SlotParameterMetadata
            {
                Key = "path",
                Type = "string",
                Label = "Path",
                IsRequired = true,
                SummaryLabel = "Path",
                SummaryMode = Pulsar.Core.Plugin.Metadata.SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = "configured",
                MissingSummaryText = "missing"
            }));

            RefreshSlot(slot, string.Empty);
            return slot;
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

        private static void RefreshSlot(PluginSlot slot, string summaryState)
        {
            string state = string.IsNullOrWhiteSpace(summaryState) ? "missing" : summaryState;
            slot.ActionLabel = slot.AvailableActions.FirstOrDefault(option => option.IsSelected || option.Value == slot.Action)?.Label ?? slot.Action;
            slot.ActionDescription = slot.AvailableActions.FirstOrDefault(option => option.Value == slot.Action)?.Description ?? string.Empty;
            slot.SummaryTokens = new System.Collections.ObjectModel.ObservableCollection<string>(new[]
            {
                $"Action: {slot.ActionLabel}",
                $"{slot.RequiredParameters.First().SummaryLabel}: {state}"
            });
            slot.ValidationSummary = state == "missing" ? "Complete the required field." : string.Empty;
            slot.SetValidationSummary(slot.ValidationSummary);
            slot.SetPresentation(SlotPresentationBuilder.Build(slot));
        }

        private static void RefreshConfiguredSlot(PluginSlot slot)
        {
            string state = slot.RequiredParameters.First().Value == "updated-value" ? "updated" : "selected";
            RefreshSlot(slot, state);
        }
    }
}
