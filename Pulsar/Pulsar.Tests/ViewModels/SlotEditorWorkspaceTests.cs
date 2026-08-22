using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Validation;
using Pulsar.ViewModels.Settings;
using Xunit;

namespace Pulsar.Tests.ViewModels
{
    /// <summary>
    /// Drives the Slot Editor Workspace directly through its public surface — no WPF
    /// shell, no Application instance, no reflection into private members. This is the
    /// test surface the workspace's seam is designed for.
    /// </summary>
    public class SlotEditorWorkspaceTests
    {
        [Fact]
        public void Load_PopulatesContexts_AndSelectsFirst()
        {
            var workspace = CreateWorkspace();
            var config = CreateConfig();

            workspace.Load(config, new Dictionary<Guid, SecretPayload>());

            workspace.AvailableContexts.Select(c => c.Key).Should().Contain(new[] { "Launcher", "Global", "notepad" });
            workspace.CurrentContext!.Key.Should().Be("Launcher");
            workspace.HasUnsavedChanges.Should().BeFalse("loading must not mark the editor dirty");
        }

        [Fact]
        public void SelectGlobalContext_LoadsCommandModeSlots()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            workspace.CurrentSlots.Select(s => s.Label).Should().Contain("Global Command");
            workspace.HasUnsavedChanges.Should().BeFalse("switching context is navigation, not an edit");
        }

        [Fact]
        public void SelectProfileContext_LoadsProfileCommandModeSlots()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "notepad");

            workspace.CurrentSlots.Select(s => s.Label).Should().Contain("Profile Command");
        }

        [Fact]
        public void CommitCreatedSlot_AddsSlotRenumbersAndMarksDirty()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            var slot = new PluginSlot
            {
                PluginId = "com.pulsar.command",
                Action = string.Empty,
                Label = "Open Notes",
                IconKey = "E756",
                Color = "#32CD32",
                Args = new Dictionary<string, string>()
            };

            workspace.CommitCreatedSlot(slot);

            workspace.CurrentSlots.Should().Contain(slot);
            slot.Slot.Should().Be(2, "the slot should be assigned the next free number after the existing slot");
            workspace.HasUnsavedChanges.Should().BeTrue();
        }

        [Fact]
        public void CreateSlotDraft_DoesNotTouchCurrentSlots_OrMarkDirty()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            var draft = workspace.CreateSlotDraft("com.pulsar.command");
            workspace.SetSlotDraftAction(draft, "run");

            draft.IconKey.Should().Be("E756");
            draft.Color.Should().BeEmpty();
            workspace.CurrentSlots.Should().NotContain(draft);
            workspace.HasUnsavedChanges.Should().BeFalse();
        }

        [Fact]
        public void EditingSlotLabel_MarksDirty()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            workspace.HasUnsavedChanges.Should().BeFalse();

            var slot = workspace.CurrentSlots.Single();
            slot.Label = "Renamed Slot";

            workspace.HasUnsavedChanges.Should().BeTrue();
        }

        [Fact]
        public void RemoveSlot_RemovesAndMarksDirty()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            var slot = workspace.CurrentSlots.Single();
            workspace.RemoveSlot(slot);

            workspace.CurrentSlots.Should().NotContain(slot);
            workspace.HasUnsavedChanges.Should().BeTrue();
        }

        [Fact]
        public void MoveSlotDown_ReordersAndRenumbers()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            workspace.CommitCreatedSlot(new PluginSlot
            {
                PluginId = "com.pulsar.command",
                Action = "run",
                Label = "Second",
                IconKey = "E756",
                Args = new Dictionary<string, string>()
            });

            var first = workspace.CurrentSlots.Single(s => s.Label == "Global Command");
            workspace.MoveSlotDown(first);

            workspace.CurrentSlots.Select(s => s.Slot).Should().Equal(1, 2);
            workspace.CurrentSlots.Last().Label.Should().Be("Global Command");
        }

        [Fact]
        public void Reorder_FromDragDrop_ComputesInsertPosition()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            workspace.CommitCreatedSlot(new PluginSlot
            {
                PluginId = "com.pulsar.command",
                Action = "run",
                Label = "Second",
                IconKey = "E756",
                Args = new Dictionary<string, string>()
            });

            workspace.Reorder(sourceIndex: 0, insertIndex: 2);

            workspace.CurrentSlots.Select(s => s.Slot).Should().Equal(1, 2);
            workspace.CurrentSlots.Last().Label.Should().Be("Global Command");
        }

        [Fact]
        public void StageSecret_IsExposedInPendingSecrets()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            var id = Guid.NewGuid();
            workspace.StageSecret(id, new SecretPayload { Label = "Mail" });

            workspace.PendingSecrets.Should().ContainKey(id);
            workspace.PendingSecrets[id].Label.Should().Be("Mail");
        }

        [Fact]
        public void ReplacePersistedSecrets_ClearsPending()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            var id = Guid.NewGuid();
            workspace.StageSecret(id, new SecretPayload { Label = "Mail" });

            workspace.ReplacePersistedSecrets(new Dictionary<Guid, SecretPayload>
            {
                [id] = new SecretPayload { Label = "Mail" }
            });

            workspace.PersistedSecrets.Should().ContainKey(id);
            workspace.PendingSecrets.Should().BeEmpty();
        }

        [Fact]
        public void ResetDirty_AfterLoad_AllowsReuseOfWorkspace()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");

            workspace.CurrentSlots.Single().Label = "Edited";

            workspace.ResetDirty();
            workspace.HasUnsavedChanges.Should().BeFalse();
        }

        private static SlotEditorWorkspace CreateWorkspace()
        {
            var registry = new PluginMetadataRegistry(NullLogger<PluginMetadataRegistry>.Instance);
            registry.Register(CreateCommandMetadata());

            return new SlotEditorWorkspace(
                registry,
                new Mock<IPkiSecretMetadataResolver>().Object,
                () => null);
        }

        private static ProfilesConfig CreateConfig()
        {
            return new ProfilesConfig
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
