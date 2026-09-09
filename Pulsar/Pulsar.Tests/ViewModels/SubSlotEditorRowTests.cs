using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Moq;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// [UX fix 2026-09-09] Regression tests for the sub-action row action selector.
    /// The selector previously bound SelectedValue -> Action (TwoWay); Rebuild()
    /// cleared/repopulated AvailableActions during a selection commit and the reset
    /// pushed null back into Action, swallowing the user's first pick (select twice).
    /// The fix holds the selected item as a VM property synced explicitly in Rebuild.
    /// </summary>
    public class SubSlotEditorRowTests
    {
        private static IPluginMetadataRegistry CreateRegistry()
        {
            var runMetadata = new SlotActionMetadata
            {
                Name = "run",
                Label = "Run",
                Parameters = new[]
                {
                    new SlotParameterMetadata
                    {
                        Key = "path",
                        Type = "text",
                        Label = "Path",
                        Group = SlotParameterGroup.Required
                    },
                    new SlotParameterMetadata
                    {
                        Key = "args",
                        Type = "text",
                        Label = "Arguments",
                        Group = SlotParameterGroup.Optional
                    }
                }
            };

            var sendKeysMetadata = new SlotActionMetadata
            {
                Name = "sendkeys",
                Label = "Send Keys"
            };

            var switchMetadata = new SlotActionMetadata
            {
                Name = "switch",
                Label = "Switch",
                Parameters = new[]
                {
                    new SlotParameterMetadata
                    {
                        Key = "app",
                        Type = "text",
                        Label = "Application",
                        Group = SlotParameterGroup.Required
                    }
                }
            };

            var mock = new Mock<IPluginMetadataRegistry>();
            mock.Setup(m => m.GetMetadata("com.pulsar.command"))
                .Returns(new PluginMetadata
                {
                    Id = "com.pulsar.command",
                    Display = new DisplayInfo { Name = "Command Runner", Description = "Command runner.", IconKey = "E756" },
                    UI = new UIHints { AccentColor = "#32CD32", Badge = "Cmd" },
                    Capabilities = new PluginCapabilities { SupportedActions = new List<string> { "run", "sendkeys" } },
                    Actions = new Dictionary<string, SlotActionMetadata>
                    {
                        ["run"] = runMetadata,
                        ["sendkeys"] = sendKeysMetadata
                    }
                });
            mock.Setup(m => m.GetMetadata("com.pulsar.winswitcher"))
                .Returns(new PluginMetadata
                {
                    Id = "com.pulsar.winswitcher",
                    Display = new DisplayInfo { Name = "WinSwitcher", Description = "Window switcher.", IconKey = "E8AB" },
                    UI = new UIHints { AccentColor = "#2196F3", Badge = "App" },
                    Capabilities = new PluginCapabilities { SupportedActions = new List<string> { "switch" } },
                    Actions = new Dictionary<string, SlotActionMetadata>
                    {
                        ["switch"] = switchMetadata
                    }
                });
            mock.Setup(m => m.GetActionMetadata("com.pulsar.command", "run")).Returns(runMetadata);
            mock.Setup(m => m.GetActionMetadata("com.pulsar.command", "sendkeys")).Returns(sendKeysMetadata);
            mock.Setup(m => m.GetActionMetadata("com.pulsar.winswitcher", "switch")).Returns(switchMetadata);
            return mock.Object;
        }

        private static SubSlotEditorRow CreateRow(IPluginMetadataRegistry registry, string pluginId)
        {
            var row = new SubSlotEditorRow(null, registry, availablePlugins: new List<SubSlotPluginOption>());
            row.PluginId = pluginId;
            return row;
        }

        [Fact]
        public void SelectingActionOptionOnce_ShouldSetActionAndExposeParameters()
        {
            var row = CreateRow(CreateRegistry(), "com.pulsar.command");

            var runOption = row.AvailableActions.Single(o => o.Value == "run");

            row.SelectedActionOption = runOption;

            row.Action.Should().Be("run");
            row.BackingSlot.Action.Should().Be("run");
            row.RequiredParameters.Should().ContainSingle(f => f.Metadata.Key == "path");
            row.OptionalParameters.Should().ContainSingle(f => f.Metadata.Key == "args");
            row.SelectedActionOption.Should().NotBeNull();
            row.SelectedActionOption!.Value.Should().Be("run");
        }

        [Fact]
        public void Rebuild_ShouldRestoreSelectedOptionFromAction()
        {
            var row = CreateRow(CreateRegistry(), "com.pulsar.command");

            // Programmatic Action set (as the materialized descriptor path does) must
            // leave the selector showing the matching option after the rebuild.
            row.Action = "sendkeys";

            row.SelectedActionOption.Should().NotBeNull();
            row.SelectedActionOption!.Value.Should().Be("sendkeys");
        }

        [Fact]
        public void PluginChangeDuringSelection_ShouldNotClobberActionViaNullWriteBack()
        {
            var row = CreateRow(CreateRegistry(), "com.pulsar.command");
            row.SelectedActionOption = row.AvailableActions.Single(o => o.Value == "run");
            row.Action.Should().Be("run");

            // Switching plugins rebuilds the option list. The stale action must survive
            // (surfaced by HasInvalidSelection), not be nulled out by the selection reset.
            row.PluginId = "com.pulsar.winswitcher";

            row.Action.Should().Be("run");
            row.SelectedActionOption.Should().BeNull();
            row.HasInvalidSelection.Should().BeTrue();

            // Coming back restores the projection from the preserved Action.
            row.PluginId = "com.pulsar.command";

            row.SelectedActionOption.Should().NotBeNull();
            row.SelectedActionOption!.Value.Should().Be("run");
            row.RequiredParameters.Should().ContainSingle(f => f.Metadata.Key == "path");
        }

        [Fact]
        public async Task ParameterPickerWritingBackingSlot_ShouldReadBackIntoDescriptor()
        {
            // [Fix 2026-09-09] Pickers (e.g. PickProcess) only see the backing PluginSlot
            // and write the extracted icon cache path plus a suggested Label onto it.
            // The row must read those back into its observables, or ToDescriptor()
            // materialized an empty IconKey and the submenu icon rendered blank.
            IPluginMetadataRegistry registry = CreateRegistry();
            var row = new SubSlotEditorRow(
                null,
                registry,
                field =>
                {
                    // Simulate SettingsViewModel.PickProcess side effects on the slot it sees.
                    field.Slot["app"] = "NOTEPAD";
                    field.Slot["path"] = @"C:\Windows\System32\notepad.exe";
                    field.Slot.Label = "Notepad";
                    field.Slot.IconKey = @"C:\Users\test\Cache\Icons\notepad.png";
                    return Task.CompletedTask;
                },
                availablePlugins: new List<SubSlotPluginOption>());
            row.PluginId = "com.pulsar.winswitcher";
            row.SelectedActionOption = row.AvailableActions.Single(o => o.Value == "switch");

            var field = row.RequiredParameters.Single(f => f.Metadata.Key == "app");
            await row.PickParameterValueCommand.ExecuteAsync(field);

            row.IconKey.Should().Be(@"C:\Users\test\Cache\Icons\notepad.png");
            row.Label.Should().Be("Notepad");

            var descriptor = row.ToDescriptor();
            descriptor.IconKey.Should().Be(@"C:\Users\test\Cache\Icons\notepad.png");
            descriptor.Label.Should().Be("Notepad");
            descriptor.Args!["app"].Should().Be("NOTEPAD");
            descriptor.Args!["path"].Should().Be(@"C:\Windows\System32\notepad.exe");
        }
    }
}
