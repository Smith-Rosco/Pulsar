using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Core.Plugin;
using Pulsar.Features.Presets.Models;
using Pulsar.Features.Presets.Services;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services.Prerequisites;
using Pulsar.Models;
using Pulsar.Services;
using Xunit;

namespace Pulsar.Tests.Presets
{
    public class PresetInstallServiceTests : IDisposable
    {
        private readonly string _testDirectory;
        private readonly string _configPath;
        private readonly ConfigService _config;

        public PresetInstallServiceTests()
        {
            _testDirectory = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString());
            System.IO.Directory.CreateDirectory(_testDirectory);
            _configPath = System.IO.Path.Combine(_testDirectory, "Profiles.json");
            _config = new ConfigService(NullLogger<ConfigService>.Instance, configPath: _configPath);
        }

        public void Dispose()
        {
            try
            {
                if (System.IO.Directory.Exists(_testDirectory))
                {
                    System.IO.Directory.Delete(_testDirectory, recursive: true);
                }
            }
            catch
            {
            }
        }

        private PresetInstallService CreateService()
        {
            return new PresetInstallService(_config, new PluginPermissionService());
        }

        private async Task SeedAsync(ProcessProfile? global = null)
        {
            var seed = new ProfilesConfig
            {
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = global ?? new ProcessProfile
                    {
                        CommandMode = new List<PluginSlot>()
                    }
                }
            };
            await _config.SaveAsync(seed, expectedRevision: null);
        }

        private static PresetPack CreatePack(string id, IReadOnlyList<CommandSlotTemplate> templates, IReadOnlyList<string>? requiredPermissions = null)
        {
            return new PresetPack
            {
                Id = id,
                Version = "1.0.0",
                TitleKey = "Preset.Pack.Macro.Title",
                DescriptionKey = "Preset.Pack.Macro.Description",
                SlotDescriptionKey = "Preset.Pack.Macro.SlotDescription",
                CommandSlotTemplates = templates,
                RequiredPermissions = requiredPermissions ?? Array.Empty<string>()
            };
        }

        private static CommandSlotTemplate Template(string pluginId, string action, string labelKey, Dictionary<string, string> args)
        {
            return new CommandSlotTemplate
            {
                PluginId = pluginId,
                Action = action,
                LabelKey = labelKey,
                IconKey = "\uE756",
                Args = args
            };
        }

        private static PresetPack CreateTwoTemplatePack()
        {
            return CreatePack("macro", new List<CommandSlotTemplate>
            {
                Template("com.pulsar.vbarunner", "run", "CommandSlot.RunVbaDemo",
                    new Dictionary<string, string> { ["scriptPath"] = "Assets/Presets/macro/excel_macro.txt", ["macro"] = "PulsarDemo" }),
                Template("com.pulsar.command", "sendkeys", "CommandSlot.InsertSampleText",
                    new Dictionary<string, string> { ["keys"] = "Hello from Pulsar!" })
            });
        }

        private async Task SeedGlobalWithUserSlotAsync(int slot = 1)
        {
            var global = new ProcessProfile
            {
                CommandMode = new List<PluginSlot>
                {
                    new()
                    {
                        Slot = slot,
                        PluginId = "com.pulsar.command",
                        Action = "run",
                        Label = "User Slot",
                        Args = new Dictionary<string, string> { ["path"] = "notepad.exe" }
                    }
                }
            };
            await SeedAsync(global);
        }

        [Fact]
        public async Task InstallAsync_TwoTemplatePack_AddsExactlyTwoSlotsAndRecordsInstalledState()
        {
            await SeedAsync();
            var service = CreateService();

            var result = await service.InstallAsync(CreateTwoTemplatePack());

            result.IsSuccess.Should().BeTrue();
            result.AddedSlotCount.Should().Be(2);

            var snapshot = _config.GetSnapshot();
            var global = snapshot.Profiles["Global"];
            global.CommandMode.Should().HaveCount(2);
            global.CommandMode.Should().OnlyContain(slot => slot.PluginId == "com.pulsar.vbarunner" || slot.PluginId == "com.pulsar.command");
            global.CommandMode[0].PluginId.Should().Be("com.pulsar.vbarunner");
            global.CommandMode[0].Action.Should().Be("run");
            global.CommandMode[1].PluginId.Should().Be("com.pulsar.command");

            snapshot.InstalledPresetPacks.Should().ContainSingle();
            var record = snapshot.InstalledPresetPacks.Single();
            record.PackId.Should().Be("macro");
            record.Version.Should().Be("1.0.0");
            record.CommandModeSlotNumbers.Should().BeEquivalentTo(global.CommandMode.Select(s => s.Slot));
        }

        [Fact]
        public async Task InstallAsync_SameVersionReinstall_ThrowsReadableAndNoConfigChange()
        {
            await SeedAsync();
            var service = CreateService();
            await service.InstallAsync(CreateTwoTemplatePack());
            long revisionAfterFirstInstall = _config.CurrentRevision;

            Func<Task> act = () => service.InstallAsync(CreateTwoTemplatePack());

            (await act.Should().ThrowAsync<PresetPackAlreadyInstalledException>())
                .WithMessage("*macro*")
                .And.InstalledVersion.Should().Be("1.0.0");

            _config.CurrentRevision.Should().Be(revisionAfterFirstInstall, "rejected install must not write");
            var snapshot = _config.GetSnapshot();
            snapshot.Profiles["Global"].CommandMode.Should().HaveCount(2);
            snapshot.InstalledPresetPacks.Should().ContainSingle();
        }

        [Fact]
        public async Task UninstallAsync_RemovesOnlyPackSlots_AndKeepsUnrelatedUserSlots()
        {
            await SeedGlobalWithUserSlotAsync(slot: 1);
            var service = CreateService();
            await service.InstallAsync(CreateTwoTemplatePack());
            var snapshot = _config.GetSnapshot();
            snapshot.Profiles["Global"].CommandMode.Should().HaveCount(3, "1 user slot + 2 pack slots");
            snapshot.InstalledPresetPacks.Should().ContainSingle();

            var result = await service.UninstallAsync("macro");

            result.Status.Should().Be(PresetUninstallStatus.Removed);
            result.RemovedSlotCount.Should().Be(2);

            var after = _config.GetSnapshot();
            after.Profiles["Global"].CommandMode.Should().ContainSingle();
            after.Profiles["Global"].CommandMode.Single().Label.Should().Be("User Slot");
            after.InstalledPresetPacks.Should().BeEmpty();
        }

        [Fact]
        public async Task UninstallAsync_NonInstalledPack_ReportsNotInstalledAndNoChange()
        {
            await SeedGlobalWithUserSlotAsync(slot: 1);
            var service = CreateService();
            long revision = _config.CurrentRevision;

            var result = await service.UninstallAsync("macro");

            result.Status.Should().Be(PresetUninstallStatus.NotInstalled);
            _config.CurrentRevision.Should().Be(revision, "not-installed uninstall must not write");
            _config.GetSnapshot().Profiles["Global"].CommandMode.Should().ContainSingle();
        }

        [Fact]
        public async Task InstallAsync_DuringInFlightConfigEdit_PreservesBothChanges()
        {
            await SeedAsync();
            var service = CreateService();

            // In-flight user edit: settings theme, held open (not yet committed).
            var session = await ConfigEditSession.BeginAsync(_config);
            session.UpdateSettings(s => s.Theme = "Dark");

            // Pack install commits on its own session while the user edit is in flight.
            var result = await service.InstallAsync(CreateTwoTemplatePack());
            result.IsSuccess.Should().BeTrue();

            // Committing the stale user session must follow the rebase path, not overwrite.
            await session.CommitAsync();

            var snapshot = _config.GetSnapshot();
            snapshot.Settings.Theme.Should().Be("Dark", "user edit must survive the rebase");
            snapshot.Profiles["Global"].CommandMode.Should().HaveCount(2, "pack slots must survive the rebase");
            snapshot.InstalledPresetPacks.Should().ContainSingle(p => p.PackId == "macro");
        }

        [Fact]
        public async Task InstallAsync_UngrantedPermissions_WritesNoSlotsAndReportsBlocked()
        {
            await SeedAsync();
            var service = CreateService();
            var permissioned = CreatePack(
                "sign-in",
                new List<CommandSlotTemplate>
                {
                    Template("com.pulsar.pki", "fill", "CommandSlot.AutoSignIn",
                        new Dictionary<string, string>())
                },
                new[] { PluginPermissions.InputInject });

            var result = await service.InstallAsync(permissioned);

            result.Status.Should().Be(PresetInstallStatus.BlockedByPermissions);
            result.MissingPermissions.Should().Contain(PluginPermissions.InputInject);
            var snapshot = _config.GetSnapshot();
            snapshot.Profiles["Global"].CommandMode.Should().BeEmpty("ungranted install must not write slots");
            snapshot.InstalledPresetPacks.Should().BeEmpty();
        }

        [Fact]
        public async Task InstallAsync_GrantedPermissions_Completes()
        {
            await SeedAsync();
            var service = CreateService();
            var permissioned = CreatePack(
                "sign-in",
                new List<CommandSlotTemplate>
                {
                    Template("com.pulsar.pki", "fill", "CommandSlot.AutoSignIn",
                        new Dictionary<string, string>())
                },
                new[] { PluginPermissions.InputInject });

            await service.GrantPermissionsAsync("sign-in", new[] { PluginPermissions.InputInject });
            var result = await service.InstallAsync(permissioned);

            result.IsSuccess.Should().BeTrue();
            var snapshot = _config.GetSnapshot();
            snapshot.Profiles["Global"].CommandMode.Should().ContainSingle();
            snapshot.InstalledPresetPacks.Should().ContainSingle();
            snapshot.InstalledPresetPacks.Single().GrantedPermissions.Should().Contain(PluginPermissions.InputInject);
        }

        [Fact]
        public async Task InstallAsync_UnmetPrerequisite_BlocksWithReadableReason()
        {
            await SeedAsync();
            var service = CreateService();
            var pack = CreatePack("macro", new List<CommandSlotTemplate>
            {
                Template("com.pulsar.vbarunner", "run", "CommandSlot.RunVbaDemo",
                    new Dictionary<string, string>())
            });
            pack = new PresetPack
            {
                Id = pack.Id,
                Version = pack.Version,
                TitleKey = pack.TitleKey,
                DescriptionKey = pack.DescriptionKey,
                SlotDescriptionKey = pack.SlotDescriptionKey,
                CommandSlotTemplates = pack.CommandSlotTemplates,
                RequiredPermissions = pack.RequiredPermissions,
                PrerequisiteProvider = typeof(UnmetExcelPrerequisiteProvider)
            };

            var result = await service.InstallAsync(pack);

            result.Status.Should().Be(PresetInstallStatus.PrerequisiteNotMet);
            result.UnmetPrerequisites.Should().NotBeEmpty();
            result.UnmetPrerequisites[0].Should().Contain("Excel");
            var snapshot = _config.GetSnapshot();
            snapshot.Profiles["Global"].CommandMode.Should().BeEmpty("prerequisite-blocked install must not write slots");
            snapshot.InstalledPresetPacks.Should().BeEmpty();
        }

        private sealed class UnmetExcelPrerequisiteProvider : IPrerequisiteProvider
        {
            public Task<IReadOnlyList<IPrerequisiteChecker>> GetCheckersAsync()
            {
                return Task.FromResult<IReadOnlyList<IPrerequisiteChecker>>(new List<IPrerequisiteChecker>
                {
                    new AlwaysMissingExcelChecker()
                });
            }

            public async Task<IReadOnlyList<PrerequisiteResult>> CheckAllAsync()
            {
                var results = new List<PrerequisiteResult>();
                foreach (var checker in await GetCheckersAsync())
                {
                    results.Add(await checker.CheckAsync());
                }
                return results;
            }
        }

        private sealed class AlwaysMissingExcelChecker : IPrerequisiteChecker
        {
            public string Id => "ExcelExists";
            public string DisplayNameKey => "Prerequisite.ExcelExists";
            public PrerequisiteSeverity Severity => PrerequisiteSeverity.Required;

            public Task<PrerequisiteResult> CheckAsync()
            {
                return Task.FromResult(new PrerequisiteResult
                {
                    Id = Id,
                    DisplayNameKey = DisplayNameKey,
                    Severity = Severity,
                    Status = PrerequisiteStatus.NotMet,
                    Details = "Microsoft Excel is not available."
                });
            }
        }
    }
}
