using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
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

        private static IPluginMetadataRegistry CreateMetadataRegistry()
        {
            var mock = new Mock<IPluginMetadataRegistry>();
            mock.Setup(m => m.GetActionMetadata("com.pulsar.command", "run"))
                .Returns(new SlotActionMetadata
                {
                    Name = "run",
                    Label = "Open Target",
                    SuggestedLabelTemplate = "Open {path}",
                    SuggestedIconKey = "E756",
                    SuggestedColorHex = "#32CD32"
                });
            mock.Setup(m => m.GetActionMetadata("com.pulsar.command", "sendkeys"))
                .Returns(new SlotActionMetadata
                {
                    Name = "sendkeys",
                    Label = "Send Keys",
                    SuggestedLabelTemplate = "Send {keys}",
                    SuggestedIconKey = "E765",
                    SuggestedColorHex = "#32CD32"
                });
            mock.Setup(m => m.GetActionMetadata("com.pulsar.winswitcher", "switch"))
                .Returns(new SlotActionMetadata
                {
                    Name = "switch",
                    Label = "Switch Or Launch",
                    SuggestedLabelTemplate = "Switch to {app}",
                    SuggestedIconKey = "E8AB",
                    SuggestedColorHex = "#2196F3"
                });
            mock.Setup(m => m.GetActionMetadata("com.pulsar.pki", "fill"))
                .Returns(new SlotActionMetadata
                {
                    Name = "fill",
                    Label = "Fill",
                    SuggestedLabelTemplate = "Fill Secret",
                    SuggestedIconKey = "E72E",
                    SuggestedColorHex = "#4CAF50"
                });
            mock.Setup(m => m.GetMetadata("com.pulsar.command"))
                .Returns(new PluginMetadata
                {
                    Id = "com.pulsar.command",
                    Display = new DisplayInfo
                    {
                        Name = "Command Runner",
                        Description = "Open apps, files, folders, or URLs.",
                        IconKey = "E756",
                        Category = "Automation"
                    },
                    UI = new UIHints { AccentColor = "#32CD32", SortOrder = 20, Badge = "Cmd" },
                    Capabilities = new PluginCapabilities { SupportedActions = new List<string> { "run", "sendkeys" } },
                    Actions = new Dictionary<string, SlotActionMetadata>()
                });
            mock.Setup(m => m.GetMetadata("com.pulsar.winswitcher"))
                .Returns(new PluginMetadata
                {
                    Id = "com.pulsar.winswitcher",
                    Display = new DisplayInfo
                    {
                        Name = "WinSwitcher",
                        Description = "Switch to a running app.",
                        IconKey = "E8AB",
                        Category = "Apps"
                    },
                    UI = new UIHints { AccentColor = "#2196F3", SortOrder = 5, Badge = "App" },
                    Capabilities = new PluginCapabilities { SupportedActions = new List<string> { "switch" } },
                    Actions = new Dictionary<string, SlotActionMetadata>()
                });
            return mock.Object;
        }

        private static IReadOnlyList<SlotTypeCard> BuildTestCards(ILocalizationService loc)
        {
            var pluginDisplayModels = new List<BuiltInPluginDisplayModel>
            {
                new BuiltInPluginDisplayModel("com.pulsar.winswitcher", "E8AB", "WinSwitcher",
                    "Switch to a running app window or launch it if needed.",
                    "apps", "Apps", "#2196F3"),
                new BuiltInPluginDisplayModel("com.pulsar.command", "E756", "Command Runner",
                    "Open apps, files, folders, or URLs, or send a key sequence.",
                    "automation", "Automation", "#32CD32")
            };
            return SlotTypeCard.BuildAllCards(loc, pluginDisplayModels);
        }

        [Fact]
        public void SlotEditorViewModel_CreateMode_ShouldStartInPickerPhase()
        {
            var loc = CreateLoc();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                BuildTestCards(loc),
                CreateDraftSlot,
                (slot, action) => { slot.Action = action ?? string.Empty; RefreshSlot(slot); },
                field => { field.Value = "selected.exe"; RefreshSlot(field.Slot); return Task.CompletedTask; },
                slot => { slot.IconKey = "E8A7"; return Task.CompletedTask; },
                slot => { slot.Color = "#32CD32"; return Task.CompletedTask; },
                loc,
                metadataRegistry: CreateMetadataRegistry());

            vm.IsPickerPhase.Should().BeTrue();
            vm.IsConfigurationPhase.Should().BeFalse();
            vm.PrimaryButtonText.Should().Be("Save Slot");
            vm.SecondaryButtonText.Should().Be("Cancel");
            vm.PrimaryCards.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void SlotEditorViewModel_CreateMode_SelectSlotTypeShouldTransitionToConfiguration()
        {
            var loc = CreateLoc();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                BuildTestCards(loc),
                CreateDraftSlot,
                (slot, action) => { slot.Action = action ?? string.Empty; RefreshSlot(slot); },
                field => { field.Value = "selected.exe"; RefreshSlot(field.Slot); return Task.CompletedTask; },
                slot => { slot.IconKey = "E8A7"; return Task.CompletedTask; },
                slot => { slot.Color = "#32CD32"; return Task.CompletedTask; },
                loc,
                metadataRegistry: CreateMetadataRegistry());

            var card = vm.PrimaryCards.First(c => c.PluginId == "com.pulsar.winswitcher");
            vm.SelectSlotTypeCommand.Execute(card);

            vm.IsConfigurationActive.Should().BeTrue();
            vm.IsConfigurationPhase.Should().BeTrue();
            vm.HasSelectedSlot.Should().BeTrue();
            vm.Slot!.PluginId.Should().Be("com.pulsar.winswitcher");
            vm.Slot.Action.Should().Be("switch");
        }

        [Fact]
        public void SlotEditorViewModel_CreateMode_GoBackToPickerShouldReturnToPicker()
        {
            var loc = CreateLoc();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                BuildTestCards(loc),
                CreateDraftSlot,
                (slot, action) => { slot.Action = action ?? string.Empty; RefreshSlot(slot); },
                field => { field.Value = "selected.exe"; RefreshSlot(field.Slot); return Task.CompletedTask; },
                slot => { slot.IconKey = "E8A7"; return Task.CompletedTask; },
                slot => { slot.Color = "#32CD32"; return Task.CompletedTask; },
                loc,
                metadataRegistry: CreateMetadataRegistry());

            var card = vm.PrimaryCards.First(c => c.PluginId == "com.pulsar.winswitcher");
            vm.SelectSlotTypeCommand.Execute(card);
            vm.IsConfigurationActive.Should().BeTrue();

            vm.GoBackToPickerCommand.Execute(null);
            vm.IsPickerPhase.Should().BeTrue();
            vm.IsConfigurationActive.Should().BeFalse();
        }

        [Fact]
        public void SlotEditorViewModel_EditMode_ShouldStartInConfiguration()
        {
            var loc = CreateLoc();
            var slot = CreateConfiguredSlot();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Edit,
                BuildTestCards(loc),
                CreateDraftSlot,
                (s, action) => { s.Action = action ?? string.Empty; RefreshSlot(s); },
                field => { field.Value = "updated-value"; RefreshConfiguredSlot(slot); return Task.CompletedTask; },
                s => { s.IconKey = "E8A7"; return Task.CompletedTask; },
                s => { s.Color = "#123456"; return Task.CompletedTask; },
                loc,
                existingSlot: slot,
                metadataRegistry: CreateMetadataRegistry());

            vm.IsConfigurationActive.Should().BeTrue();
            vm.IsEditMode.Should().BeTrue();
            vm.IsAppearanceExpanded.Should().BeTrue();
            vm.Slot.Should().Be(slot);
            vm.Slot!.PluginId.Should().Be("com.pulsar.pki");
        }

        [Fact]
        public async Task SlotEditorViewModel_EditMode_ShouldExposePreviewMetadataAndAppearance()
        {
            var loc = CreateLoc();
            var slot = CreateConfiguredSlot();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Edit,
                BuildTestCards(loc),
                CreateDraftSlot,
                (s, action) => { s.Action = action ?? string.Empty; RefreshSlot(s); },
                field => { field.Value = "updated-value"; RefreshConfiguredSlot(slot); return Task.CompletedTask; },
                s => { s.IconKey = "E8A7"; return Task.CompletedTask; },
                s => { s.Color = "#123456"; return Task.CompletedTask; },
                loc,
                existingSlot: slot,
                metadataRegistry: CreateMetadataRegistry());

            vm.HeaderStatusText.Should().NotBeNullOrEmpty();
            vm.IsAppearanceExpanded.Should().BeTrue();
            vm.IsAdvancedExpanded.Should().BeFalse();

            await vm.PickParameterValueAsync(vm.RequiredParameters.Single());
            vm.HeaderStatusText.Should().NotBeNullOrEmpty();
        }

        [Fact]
        public void SlotTypeCard_BuildPrimaryCards_ShouldReturn6Cards()
        {
            var loc = CreateLoc();
            var cards = SlotTypeCard.BuildPrimaryCards(loc);

            cards.Should().HaveCount(6);
            cards.All(c => c.IsPrimary).Should().BeTrue();

            cards.Should().ContainSingle(c => c.Id == "switch-app" && c.PluginId == "com.pulsar.winswitcher" && c.DefaultAction == "switch");
            cards.Should().ContainSingle(c => c.Id == "open-target" && c.PluginId == "com.pulsar.command" && c.DefaultAction == "run");
            cards.Should().ContainSingle(c => c.Id == "send-keys" && c.PluginId == "com.pulsar.command" && c.DefaultAction == "sendkeys");
            cards.Should().ContainSingle(c => c.Id == "fill-secret" && c.PluginId == "com.pulsar.pki" && c.DefaultAction == "fill");
            cards.Should().ContainSingle(c => c.Id == "run-script" && c.PluginId == "com.pulsar.command" && c.DefaultAction == "run");
            cards.Should().ContainSingle(c => c.Id == "system" && c.PluginId == "com.pulsar.system" && c.DefaultAction == "open-settings");
        }

        [Fact]
        public void SlotTypeCard_BuildAllCards_ShouldMergePrimaryAndRegistry()
        {
            var loc = CreateLoc();
            var pluginModels = new List<BuiltInPluginDisplayModel>
            {
                new BuiltInPluginDisplayModel("com.pulsar.winswitcher", "E8AB", "WinSwitcher",
                    "Switch to a running app.", "apps", "Apps", "#2196F3"),
                new BuiltInPluginDisplayModel("com.pulsar.command", "E756", "Command Runner",
                    "Open apps and URLs.", "automation", "Automation", "#32CD32", true),
                new BuiltInPluginDisplayModel("com.pulsar.bookmarklet", "E896", "Bookmarklet Runner",
                    "Execute JavaScript.", "browser", "Browser", "#FF6B6B")
            };

            var cards = SlotTypeCard.BuildAllCards(loc, pluginModels);

            cards.Should().Contain(c => c.PluginId == "com.pulsar.winswitcher" && c.IsPrimary);
            cards.Should().Contain(c => c.PluginId == "com.pulsar.command" && c.IsPrimary);
            cards.Should().Contain(c => c.PluginId == "com.pulsar.bookmarklet");
        }

        [Fact]
        public void SlotEditorViewModel_CreateMode_AppearanceCollapsed()
        {
            var loc = CreateLoc();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                BuildTestCards(loc),
                CreateDraftSlot,
                (slot, action) => { slot.Action = action ?? string.Empty; RefreshSlot(slot); },
                field => { field.Value = "selected.exe"; return Task.CompletedTask; },
                slot => { slot.IconKey = "E8A7"; return Task.CompletedTask; },
                slot => { slot.Color = "#32CD32"; return Task.CompletedTask; },
                loc,
                metadataRegistry: CreateMetadataRegistry());

            vm.IsAppearanceExpanded.Should().BeFalse();
        }

        [Fact]
        public void SlotEditorViewModel_EditMode_AppearanceExpanded()
        {
            var loc = CreateLoc();
            var slot = CreateConfiguredSlot();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Edit,
                BuildTestCards(loc),
                CreateDraftSlot,
                (s, action) => { s.Action = action ?? string.Empty; RefreshSlot(s); },
                field => { field.Value = "updated-value"; return Task.CompletedTask; },
                s => { s.IconKey = "E8A7"; return Task.CompletedTask; },
                s => { s.Color = "#123456"; return Task.CompletedTask; },
                loc,
                existingSlot: slot,
                metadataRegistry: CreateMetadataRegistry());

            vm.IsAppearanceExpanded.Should().BeTrue();
        }

        [Fact]
        public void SlotEditorViewModel_SlotTypeCardMapsToCorrectAction()
        {
            var loc = CreateLoc();
            var vm = new SlotEditorViewModel(
                SlotEditorMode.Create,
                BuildTestCards(loc),
                CreateDraftSlot,
                (slot, action) => { slot.Action = action ?? string.Empty; RefreshSlot(slot); },
                field => { field.Value = "selected.exe"; RefreshSlot(field.Slot); return Task.CompletedTask; },
                slot => { slot.IconKey = "E8A7"; return Task.CompletedTask; },
                slot => { slot.Color = "#32CD32"; return Task.CompletedTask; },
                loc,
                metadataRegistry: CreateMetadataRegistry());

            // Switch App → winswitcher + switch
            var switchAppCard = vm.PrimaryCards.First(c => c.Id == "switch-app");
            vm.SelectSlotTypeCommand.Execute(switchAppCard);
            vm.Slot!.PluginId.Should().Be("com.pulsar.winswitcher");
            vm.Slot.Action.Should().Be("switch");

            // Open Target → command + run
            vm.GoBackToPickerCommand.Execute(null);
            var openTargetCard = vm.PrimaryCards.First(c => c.Id == "open-target");
            vm.SelectSlotTypeCommand.Execute(openTargetCard);
            vm.Slot!.PluginId.Should().Be("com.pulsar.command");
            vm.Slot.Action.Should().Be("run");

            // Send Keys → command + sendkeys
            vm.GoBackToPickerCommand.Execute(null);
            var sendKeysCard = vm.PrimaryCards.First(c => c.Id == "send-keys");
            vm.SelectSlotTypeCommand.Execute(sendKeysCard);
            vm.Slot!.PluginId.Should().Be("com.pulsar.command");
            vm.Slot.Action.Should().Be("sendkeys");

            // Fill Secret → pki + fill
            vm.GoBackToPickerCommand.Execute(null);
            var fillSecretCard = vm.PrimaryCards.First(c => c.Id == "fill-secret");
            vm.SelectSlotTypeCommand.Execute(fillSecretCard);
            vm.Slot!.PluginId.Should().Be("com.pulsar.pki");
            vm.Slot.Action.Should().Be("fill");
        }

        // ---- Helper methods ----

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

            if (string.Equals(pluginId, "com.pulsar.system", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SlotActionOption
                {
                    Value = "open-settings",
                    Label = "Open Settings",
                    Description = "Open Pulsar settings.",
                    IsSelected = true
                };
                yield return new SlotActionOption
                {
                    Value = "quick-add-profile",
                    Label = "Quick Add",
                    Description = "Quick add current app.",
                    IsSelected = false
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
                yield return new SlotParameterEditorField(slot, new SlotParameterMetadata
                {
                    Key = "app",
                    Type = "string",
                    Label = "Process Name",
                    IsRequired = true,
                    SummaryLabel = "App",
                    SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                    ConfiguredSummaryText = "selected",
                    MissingSummaryText = "missing"
                });
                yield break;
            }

            if (string.Equals(pluginId, "com.pulsar.pki", StringComparison.OrdinalIgnoreCase))
            {
                yield return new SlotParameterEditorField(slot, new SlotParameterMetadata
                {
                    Key = "secretId",
                    Type = "guid",
                    Label = "Secret",
                    IsRequired = true,
                    SummaryLabel = "Secret",
                    SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                    ConfiguredSummaryText = "selected",
                    MissingSummaryText = "missing",
                    PickerIntent = SlotPickerIntent.Secret
                });
                yield break;
            }

            yield return new SlotParameterEditorField(slot, new SlotParameterMetadata
            {
                Key = "path",
                Type = "string",
                Label = "Path",
                IsRequired = true,
                SummaryLabel = "Path",
                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
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

            slot.RequiredParameters.Add(new SlotParameterEditorField(slot, new SlotParameterMetadata
            {
                Key = "secretId",
                Type = "guid",
                Label = "Secret",
                IsRequired = true,
                SummaryLabel = "Secret",
                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = "selected",
                MissingSummaryText = "missing",
                PickerIntent = SlotPickerIntent.Secret
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
