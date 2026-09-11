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
using Pulsar.Plugins.Core.SecretFill.Contracts;
using Pulsar.Plugins.Core.SecretFill.Models;
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
        public void StageSecret_MarksTheEditorDirty()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            workspace.StageSecret(Guid.NewGuid(), new SecretPayload { Label = "Mail" });

            workspace.HasUnsavedChanges.Should().BeTrue("staging a secret is a user edit");
        }

        [Fact]
        public void UnstageSecret_DropsTheStagedPayload_AndMarksTheEditorDirty()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());

            var id = Guid.NewGuid();
            workspace.StageSecret(id, new SecretPayload { Label = "Mail" });
            workspace.ResetDirty();

            workspace.UnstageSecret(id);

            workspace.PendingSecrets.Should().NotContainKey(id);
            workspace.HasUnsavedChanges.Should().BeTrue("dropping a staged secret is a user edit");
        }

        [Fact]
        public void PendingSecretsView_SeesOnlyStagedPayloads()
        {
            var workspace = CreateWorkspace();
            var persistedId = Guid.NewGuid();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>
            {
                [persistedId] = new SecretPayload { Label = "Persisted" }
            });

            var stagedId = Guid.NewGuid();
            workspace.StageSecret(stagedId, new SecretPayload { Label = "Mail" });

            workspace.PendingSecrets[stagedId].Label.Should().Be("Mail");
            workspace.PendingSecrets.Should().NotContainKey(persistedId, "persisted secrets are not staging state");
        }

        [Fact]
        public void PendingSecrets_SurfaceIsReadOnly()
        {
            // [Architecture review 2026-09-11, candidate #1] The live staging dictionary
            // used to be handed out as a mutable Dictionary, so every caller re-implemented
            // "stage + mark dirty". It is a read-only view now; mutation goes through
            // StageSecret / UnstageSecret.
            var property = typeof(SlotEditorWorkspace).GetProperty(nameof(SlotEditorWorkspace.PendingSecrets));

            property.Should().NotBeNull();
            property!.PropertyType.Should().BeAssignableTo<IReadOnlyDictionary<Guid, SecretPayload>>();
            property.PropertyType.Should().NotBe(
                typeof(Dictionary<Guid, SecretPayload>),
                "the staging dictionary must not be exposed for in-place mutation");
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

        [Fact]
        public void RefreshSlotValidationSummaries_SeverityTravelsWithType_NotFromMessageKeywords()
        {
            var workspace = CreateWorkspace();
            workspace.Load(CreateConfig(), new Dictionary<Guid, SecretPayload>());
            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");
            var slot = workspace.CurrentSlots.Single();

            var validationResult = new ValidationResult();
            // The "expects {type}" AddError site in ConfigValidationPipeline contains
            // none of the old severity-guess keywords ("error"/"required"/"invalid"/
            // "missing") and used to display as Warning despite being an Error
            // (candidate N: severity now travels with the ValidationError record).
            validationResult.AddError(
                "Slot 1 (Global Command) parameter 'Path' expects number",
                slot.PluginId,
                "slot[Global:Command:1].path");

            workspace.RefreshSlotValidationSummaries(validationResult);

            slot.ValidationSummary.Should().Be("Slot 1 (Global Command) parameter 'Path' expects number");
            slot.ValidationSeverity.Should().Be(ValidationSeverity.Error,
                "a message without the old keywords must no longer downgrade to Warning");
        }

        // ============ [C4] Draft write routing ============

        [Fact]
        public void SyncSlotsToConfig_RoutesThroughInjectedWriter_NotThroughTheHeldDraft()
        {
            var writes = new List<(string Key, List<PluginSlot> Slots)>();
            var workspace = CreateWorkspace(writes);
            var config = CreateConfig();
            workspace.Load(config, new Dictionary<Guid, SecretPayload>());

            workspace.CurrentContext = workspace.AvailableContexts.Single(c => c.Key == "Global");
            workspace.CurrentSlots.Add(new PluginSlot { Slot = 2, Label = "Added" });

            workspace.SyncSlotsToConfig();

            writes.Should().Contain(w => w.Key == "Global" && w.Slots.Count == 2,
                "the slot list must travel through the injected writer");

            config.Profiles["Global"].CommandMode.Should().HaveCount(1,
                "the workspace holds the draft for READING only; writing it directly would "
                + "bypass the editor session that owns dirty tracking (C4)");
        }

        private static SlotEditorWorkspace CreateWorkspace(
            List<(string Key, List<PluginSlot> Slots)>? writes = null)
        {
            var sink = writes ?? new List<(string Key, List<PluginSlot> Slots)>();

            var registry = new PluginMetadataRegistry(NullLogger<PluginMetadataRegistry>.Instance);
            registry.Register(CreateCommandMetadata());

            return new SlotEditorWorkspace(
                registry,
                new Mock<ISecretFillMetadataResolver>().Object,
                () => null,
                (key, slots) => sink.Add((key, slots.ToList())));
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
                    Tier = PluginTier.Extension
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
