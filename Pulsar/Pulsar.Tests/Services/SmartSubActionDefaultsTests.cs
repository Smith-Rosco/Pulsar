using System;
using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Services;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// Behavior of <see cref="SmartSubActionDefaults"/>: known slot types get a
    /// default catalog, unknown types return null, and the catalog is authored data
    /// (re-creating a draft re-injects afresh).
    /// </summary>
    public class SmartSubActionDefaultsTests
    {
        [Fact]
        public void ForPlugin_ClipboardSendKeysType_ShouldReturnClipboardCatalog()
        {
            var defaults = new SmartSubActionDefaults();

            var catalog = defaults.ForPlugin("com.pulsar.command", "sendkeys");

            catalog.Should().NotBeNull();
            catalog.Should().HaveCountGreaterThan(0);
            catalog.Should().OnlyContain(s => s.PluginId == "com.pulsar.command" && s.Action == "sendkeys");
            catalog.Should().Contain(s => s.Label == "Copy");
            catalog.Should().Contain(s => s.Args != null && s.Args.ContainsKey("keys"));
        }

        [Fact]
        public void ForPlugin_SystemToolsType_ShouldReturnSystemToolsCatalog()
        {
            var defaults = new SmartSubActionDefaults();

            var catalog = defaults.ForPlugin("com.pulsar.system", "open-settings");

            catalog.Should().NotBeNull();
            catalog.Should().Contain(s => s.Label == "Notepad");
            catalog.Should().Contain(s => s.PluginId == "com.pulsar.command" && s.Action == "run");
        }

        [Fact]
        public void ForPlugin_UnknownType_ShouldReturnNull()
        {
            var defaults = new SmartSubActionDefaults();

            var catalog = defaults.ForPlugin("com.pulsar.pki", "fill");

            catalog.Should().BeNull();
        }

        [Fact]
        public void ForPlugin_CommandWithDifferentAction_ShouldStillReturnCatalog_ForDraftInjection()
        {
            // Drafts are injected before the card's default action is applied, so the
            // catalog keys on the plugin's canonical type regardless of the concrete action.
            var defaults = new SmartSubActionDefaults();

            var catalog = defaults.ForPlugin("com.pulsar.command", "run");

            catalog.Should().NotBeNull();
        }

        [Fact]
        public void ForPlugin_RepeatedCalls_ShouldReturnFreshLists()
        {
            var defaults = new SmartSubActionDefaults();

            var first = defaults.ForPlugin("com.pulsar.command", "sendkeys");
            var second = defaults.ForPlugin("com.pulsar.command", "sendkeys");

            first.Should().NotBeSameAs(second, "re-creation must re-inject a fresh list, never a shared instance");
        }

        [Fact]
        public void CreateSlotDraft_KnownType_ShouldInjectDefaults()
        {
            var workspace = CreateWorkspace();

            var draft = workspace.CreateSlotDraft("com.pulsar.system");

            draft.SubActions.Should().NotBeNull();
            draft.SubActions.Should().HaveCountGreaterThan(0);
        }

        [Fact]
        public void CreateSlotDraft_UnknownType_ShouldStayEmpty()
        {
            var workspace = CreateWorkspace();

            var draft = workspace.CreateSlotDraft("com.pulsar.pki");

            draft.SubActions.Should().BeNull();
        }

        [Fact]
        public void EditMode_SetSlotAction_ShouldNotInjectDefaults()
        {
            var workspace = CreateWorkspace();
            var slot = new PluginSlot
            {
                PluginId = "com.pulsar.command",
                Action = "run",
                Label = "Open",
                IconKey = "E756",
                Color = "#32CD32",
                Args = new Dictionary<string, string>(),
                SubActions =
                [
                    new SubSlotDescriptor("com.pulsar.command", "run", null, "Child", string.Empty, string.Empty)
                ]
            };

            workspace.SetSlotAction(slot, "sendkeys");

            slot.SubActions.Should().HaveCount(1, "editing an existing slot must never re-inject defaults");
            slot.SubActions![0].Label.Should().Be("Child");
        }

        [Fact]
        public void CreateSlotDraft_RepeatedCreation_ShouldReInjectAfresh()
        {
            var workspace = CreateWorkspace();

            var first = workspace.CreateSlotDraft("com.pulsar.system");
            var second = workspace.CreateSlotDraft("com.pulsar.system");

            first.SubActions.Should().NotBeSameAs(second.SubActions, "each new draft must receive its own injected list");
            second.SubActions.Should().HaveCount(first.SubActions!.Count);
        }

        private static SlotEditorWorkspace CreateWorkspace()
        {
            var registry = new PluginMetadataRegistry(NullLogger<PluginMetadataRegistry>.Instance);
            registry.Register(CreateCommandMetadata());

            return new SlotEditorWorkspace(
                registry,
                new Mock<IPkiSecretMetadataResolver>().Object,
                () => null,
                smartDefaults: new SmartSubActionDefaults());
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
    }
}
