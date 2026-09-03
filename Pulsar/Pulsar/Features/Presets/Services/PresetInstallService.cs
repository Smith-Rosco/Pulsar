using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Features.Presets.Models;
using Pulsar.Features.Tutorial.Services.Prerequisites;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Features.Presets.Services
{
    /// <summary>
    /// Installs/uninstalls office-action preset packs by appending their command-slot templates
    /// to the Global profile's CommandMode through the revision-guarded <see cref="ConfigEditSession"/>
    /// path. Pack permission tokens are evaluated against persisted per-pack grants (the same
    /// consent boundary external plugins use); install writes nothing until permissions are granted.
    /// </summary>
    public sealed class PresetInstallService : IPresetInstallService
    {
        private const string GlobalProfile = "Global";

        private readonly IConfigService _config;
        private readonly IPluginPermissionService _permissionService;
        private readonly ILocalizationService? _loc;

        public PresetInstallService(
            IConfigService config,
            IPluginPermissionService permissionService,
            ILocalizationService? localizationService = null)
        {
            _config = config;
            _permissionService = permissionService;
            _loc = localizationService;
        }

        public async Task<PresetInstallResult> InstallAsync(PresetPack pack, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(pack);

            PresetInstallResult? result = null;

            await ConfigEditSession.RunAsync(_config, async session =>
            {
                var record = session.Draft.InstalledPresetPacks.FirstOrDefault(r =>
                    string.Equals(r.PackId, pack.Id, StringComparison.OrdinalIgnoreCase));

                if (record != null && record.CommandModeSlotNumbers.Count > 0)
                {
                    throw new PresetPackAlreadyInstalledException(pack.Id, record.Version);
                }

                var evaluation = EvaluatePermissions(pack, record?.GrantedPermissions);
                if (!evaluation.Granted)
                {
                    var missing = evaluation.MissingPermissions.Concat(evaluation.UnknownPermissions).ToList();
                    result = PresetInstallResult.BlockedByPermissions(missing);
                    return;
                }

                var unmet = await CheckPrerequisitesAsync(pack, cancellationToken);
                if (unmet.Count > 0)
                {
                    result = PresetInstallResult.PrerequisiteNotMet(unmet);
                    return;
                }

                var createdSlots = AppendSlots(session, pack);
                if (record == null)
                {
                    record = new InstalledPresetPack { PackId = pack.Id, Version = pack.Version };
                    session.Draft.InstalledPresetPacks.Add(record);
                }
                else
                {
                    record.Version = pack.Version;
                }

                record.GrantedPermissions = new List<string>(record.GrantedPermissions ?? new List<string>());
                foreach (var slotNumber in createdSlots)
                {
                    if (!record.CommandModeSlotNumbers.Contains(slotNumber))
                    {
                        record.CommandModeSlotNumbers.Add(slotNumber);
                    }
                }

                result = PresetInstallResult.Succeeded(createdSlots.Count);
            });

            return result ?? PresetInstallResult.PrerequisiteNotMet(new List<string> { "Install aborted." });
        }

        public async Task<PresetUninstallResult> UninstallAsync(string packId, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packId);

            PresetUninstallResult? result = null;

            await ConfigEditSession.RunAsync(_config, session =>
            {
                var record = session.Draft.InstalledPresetPacks.FirstOrDefault(r =>
                    string.Equals(r.PackId, packId, StringComparison.OrdinalIgnoreCase));

                if (record == null || record.CommandModeSlotNumbers.Count == 0)
                {
                    result = PresetUninstallResult.NotInstalled();
                    return Task.CompletedTask;
                }

                var removed = RemoveSlots(session, record.CommandModeSlotNumbers);
                session.Draft.InstalledPresetPacks.Remove(record);
                result = PresetUninstallResult.Removed(removed);
                return Task.CompletedTask;
            });

            return result ?? PresetUninstallResult.NotInstalled();
        }

        public async Task GrantPermissionsAsync(string packId, IEnumerable<string> permissions, CancellationToken cancellationToken = default)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(packId);
            ArgumentNullException.ThrowIfNull(permissions);

            var tokens = permissions
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .Distinct(StringComparer.Ordinal)
                .ToList();

            await ConfigEditSession.RunAsync(_config, session =>
            {
                var record = session.Draft.InstalledPresetPacks.FirstOrDefault(r =>
                    string.Equals(r.PackId, packId, StringComparison.OrdinalIgnoreCase));

                if (record == null)
                {
                    record = new InstalledPresetPack { PackId = packId };
                    session.Draft.InstalledPresetPacks.Add(record);
                }

                record.GrantedPermissions = tokens;
                return Task.CompletedTask;
            });
        }

        public IReadOnlyList<InstalledPresetPack> GetInstalled()
        {
            return _config.GetSnapshot().InstalledPresetPacks.ToList();
        }

        private List<int> AppendSlots(ConfigEditSession session, PresetPack pack)
        {
            if (!session.Draft.Profiles.TryGetValue(GlobalProfile, out var global))
            {
                global = new ProcessProfile();
                session.Draft.Profiles[GlobalProfile] = global;
            }

            var commandMode = global.CommandMode ?? new List<PluginSlot>();
            global.CommandMode = commandMode;

            int nextSlot = commandMode.Count == 0
                ? 1
                : commandMode.Max(s => s.Slot) + 1;

            var created = new List<int>();
            foreach (var template in pack.CommandSlotTemplates)
            {
                if (commandMode.Any(existing =>
                    string.Equals(existing.PluginId, template.PluginId, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(existing.Action, template.Action, StringComparison.OrdinalIgnoreCase)
                    && ArgsEqual(existing.Args, template.Args)))
                {
                    continue;
                }

                var slot = new PluginSlot
                {
                    Slot = nextSlot,
                    PluginId = template.PluginId,
                    Action = template.Action,
                    Args = new Dictionary<string, string>(template.Args),
                    Label = _loc != null ? _loc[template.LabelKey] : template.LabelKey,
                    IconKey = template.IconKey
                };

                commandMode.Add(slot);
                created.Add(nextSlot);
                nextSlot++;
            }

            return created;
        }

        private int RemoveSlots(ConfigEditSession session, IReadOnlyCollection<int> slotNumbers)
        {
            if (!session.Draft.Profiles.TryGetValue(GlobalProfile, out var global)
                || global.CommandMode == null)
            {
                return 0;
            }

            var traced = new HashSet<int>(slotNumbers);
            int removed = global.CommandMode.RemoveAll(s => traced.Contains(s.Slot));
            return removed;
        }

        private static bool ArgsEqual(Dictionary<string, string>? left, IReadOnlyDictionary<string, string> right)
        {
            if (left == null && right.Count == 0)
            {
                return true;
            }

            if (left == null || left.Count != right.Count)
            {
                return false;
            }

            return right.All(pair =>
                left.TryGetValue(pair.Key, out var value) && string.Equals(value, pair.Value, StringComparison.Ordinal));
        }

        private PluginPermissionEvaluation EvaluatePermissions(PresetPack pack, IEnumerable<string>? grantedPermissions)
        {
            var descriptor = BuildPackDescriptor(pack);
            return _permissionService.Evaluate(descriptor, grantedPermissions);
        }

        private async Task<List<string>> CheckPrerequisitesAsync(PresetPack pack, CancellationToken cancellationToken)
        {
            if (pack.PrerequisiteProvider == null)
            {
                return new List<string>();
            }

            try
            {
                if (Activator.CreateInstance(pack.PrerequisiteProvider) is not IPrerequisiteProvider provider)
                {
                    return new List<string> { $"Prerequisite provider '{pack.PrerequisiteProvider.Name}' is not supported." };
                }

                var results = await provider.CheckAllAsync();
                return results
                    .Where(r => r.Status == PrerequisiteStatus.NotMet && r.Severity == PrerequisiteSeverity.Required)
                    .Select(r => FormatPrerequisite(r))
                    .ToList();
            }
            catch (Exception)
            {
                return new List<string> { $"Prerequisite provider '{pack.PrerequisiteProvider.Name}' could not be created." };
            }
        }

        private string FormatPrerequisite(PrerequisiteResult result)
        {
            string displayName = _loc != null
                ? _loc[result.DisplayNameKey]
                : result.DisplayNameKey;

            return string.IsNullOrWhiteSpace(result.Details)
                ? displayName
                : $"{displayName}: {result.Details}";
        }

        private static PluginDescriptor BuildPackDescriptor(PresetPack pack)
        {
            return new PluginDescriptor
            {
                Id = pack.Id,
                DisplayName = pack.Id,
                Version = pack.Version,
                Author = "Pulsar",
                Description = pack.DescriptionKey,
                Icon = "\uE756",
                CanDisable = true,
                Tier = PluginTier.Extension,
                IsExternal = true,
                Permissions = pack.RequiredPermissions.ToList(),
                ImplementationType = null,
                Dependencies = Array.Empty<string>(),
                IsConfigurable = false,
                Metadata = new PluginMetadata
                {
                    Id = pack.Id,
                    Display = new DisplayInfo
                    {
                        Name = pack.Id,
                        Description = pack.DescriptionKey,
                        IconKey = "\uE756",
                        Category = "Presets",
                        Version = pack.Version,
                        Author = "Pulsar"
                    },
                    Schema = null,
                    UI = new UIHints
                    {
                        Badge = "Preset",
                        AccentColor = "#4A90E2",
                        ShowInQuickAccess = false,
                        SortOrder = 100
                    },
                    Capabilities = new PluginCapabilities
                    {
                        SupportedActions = new List<string>(),
                        Dependencies = new List<string>(),
                        Tier = PluginTier.Extension,
                        MinPulsarVersion = "1.0.0"
                    },
                    Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                }
            };
        }
    }
}
