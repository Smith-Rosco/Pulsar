using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Models;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.Pki.Services;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class ConfigBackupServiceTests
    {
        [Fact]
        public async Task Export_WithSecrets_WritesManifestConfigAndSecrets()
        {
            using var fixture = new Fixture();
            var config = CreateConfig(profileCount: 2);
            await fixture.ConfigService.SaveAsync(config);
            var secrets = new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Payroll", Account = "ops@example.com", EncryptedData = fixture.Protector.Encrypt("p@ss") }
            };
            await fixture.SecretStore.SaveAsync(secrets);

            string zipPath = fixture.GetPath("backup.zip");
            var result = await fixture.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true));

            result.Success.Should().BeTrue();
            result.Summary.Should().NotBeNull();
            result.Summary!.ProfilesCount.Should().Be(2);
            result.Summary.SecretCount.Should().Be(1);
            result.Summary.SecretsProtected.Should().BeFalse();

            using var archive = ZipFile.OpenRead(zipPath);
            archive.GetEntry("manifest.json").Should().NotBeNull();
            archive.GetEntry("Profiles.json").Should().NotBeNull();
            archive.GetEntry("secrets.json").Should().NotBeNull();
            archive.GetEntry("secrets.protected.json").Should().BeNull();

            var manifest = ReadManifest(archive);
            manifest.Should().NotBeNull();
            manifest!.ContainsSecrets.Should().BeTrue();
            manifest.SecretsProtected.Should().BeFalse();
        }

        [Fact]
        public async Task Export_WithoutSecrets_OmitsSecretsEntry()
        {
            using var fixture = new Fixture();
            var config = CreateConfig(profileCount: 1);
            await fixture.ConfigService.SaveAsync(config);

            string zipPath = fixture.GetPath("backup.zip");
            var result = await fixture.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true));

            result.Success.Should().BeTrue();
            result.Summary!.SecretCount.Should().Be(0);
            using var archive = ZipFile.OpenRead(zipPath);
            archive.GetEntry("secrets.json").Should().BeNull();
            archive.GetEntry("secrets.protected.json").Should().BeNull();
        }

        [Fact]
        public async Task Export_WithPassword_WritesProtectedSecretsAndKdf()
        {
            using var fixture = new Fixture();
            var config = CreateConfig(profileCount: 1);
            await fixture.ConfigService.SaveAsync(config);
            await fixture.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Vault", Account = "user", EncryptedData = fixture.Protector.Encrypt("hunter2") }
            });

            string zipPath = fixture.GetPath("protected.zip");
            var result = await fixture.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true, Password: "correct horse"));

            result.Success.Should().BeTrue();
            result.Summary!.SecretsProtected.Should().BeTrue();

            using var archive = ZipFile.OpenRead(zipPath);
            archive.GetEntry("secrets.json").Should().BeNull();
            archive.GetEntry("secrets.protected.json").Should().NotBeNull();
            var manifest = ReadManifest(archive);
            manifest!.SecretsProtected.Should().BeTrue();
            manifest.Kdf.Should().NotBeNull();
            manifest.Kdf!.SaltBase64.Should().NotBeNullOrEmpty();
            manifest.Kdf.Iterations.Should().BeGreaterThan(0);

            // The raw protected entry must not contain the plaintext.
            var raw = ReadEntryText(archive, "secrets.protected.json");
            raw.Should().NotContain("hunter2");
        }

        [Fact]
        public async Task Export_WhenSecretCannotBeDecrypted_FailsWithSecretProtectionFailed()
        {
            using var fixture = new Fixture();
            await fixture.ConfigService.SaveAsync(CreateConfig(profileCount: 1));
            await fixture.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Broken", Account = "u", EncryptedData = "not-a-real-blob" }
            });

            var result = await fixture.Service.ExportAsync(fixture.GetPath("broken.zip"), new ConfigBackupExportOptions(IncludeSecrets: true, Password: "pw"));

            result.Success.Should().BeFalse();
            result.Error.Should().Be(ConfigBackupError.SecretProtectionFailed);
        }

        [Fact]
        public async Task Import_RoundTripsConfigAndSecrets()
        {
            using var source = new Fixture();
            using var target = new Fixture();

            var exportedConfig = CreateConfig(profileCount: 2);
            exportedConfig.Profiles["app0"].Alias = "Backup Alias";
            await source.ConfigService.SaveAsync(exportedConfig);
            var exportedSecrets = new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "GitHub", Account = "octo@example.com", EncryptedData = source.Protector.Encrypt("token123") }
            };
            await source.SecretStore.SaveAsync(exportedSecrets);

            string zipPath = source.GetPath("backup.zip");
            (await source.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true))).Success.Should().BeTrue();

            // Target has a different config + secrets before import.
            await target.ConfigService.SaveAsync(CreateConfig(profileCount: 1));
            await target.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Old", Account = "old", EncryptedData = target.Protector.Encrypt("oldpass") }
            });

            var result = await target.Service.ImportAsync(zipPath);

            result.Success.Should().BeTrue();
            result.Summary!.ProfilesCount.Should().Be(2);
            result.Summary.SecretCount.Should().Be(1);

            var restored = await target.ConfigService.LoadSnapshotAsync(forceReload: true);
            restored.Profiles.Should().HaveCount(2);
            restored.Profiles["app0"].Alias.Should().Be("Backup Alias");

            var restoredSecrets = await target.SecretStore.LoadAsync();
            restoredSecrets.Should().HaveCount(1);
            restoredSecrets.Values.Single().Label.Should().Be("GitHub");
            target.Protector.Decrypt(restoredSecrets.Values.Single().EncryptedData).Should().Be("token123");
        }

        [Fact]
        public async Task Import_ProtectedBackup_RestoresOnAnotherMachine()
        {
            using var source = new Fixture();
            using var target = new Fixture(protectorPrefix: "SEAL-OTHER-MACHINE:");

            await source.ConfigService.SaveAsync(CreateConfig(profileCount: 1));
            var secretId = Guid.NewGuid();
            await source.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = "VPN", Account = "vpn@example.com", EncryptedData = source.Protector.Encrypt("vpnpass") }
            });

            string zipPath = source.GetPath("protected.zip");
            (await source.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true, Password: "portable"))).Success.Should().BeTrue();

            var result = await target.Service.ImportAsync(zipPath, password: "portable");

            result.Success.Should().BeTrue();
            var restored = await target.SecretStore.LoadAsync();
            restored.Should().ContainKey(secretId);
            var payload = restored[secretId];
            payload.EncryptedData.Should().StartWith("SEAL-OTHER-MACHINE:");
            target.Protector.Decrypt(payload.EncryptedData).Should().Be("vpnpass");
        }

        [Fact]
        public async Task Import_ProtectedBackup_WrongPassword_FailsAndLeavesStateUntouched()
        {
            using var source = new Fixture();
            using var target = new Fixture();

            await source.ConfigService.SaveAsync(CreateConfig(profileCount: 1));
            await source.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Vault", Account = "u", EncryptedData = source.Protector.Encrypt("pw") }
            });
            string zipPath = source.GetPath("protected.zip");
            (await source.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true, Password: "right"))).Success.Should().BeTrue();

            var targetConfig = CreateConfig(profileCount: 3);
            await target.ConfigService.SaveAsync(targetConfig);
            await target.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Keep", Account = "k", EncryptedData = target.Protector.Encrypt("keep") }
            });

            var result = await target.Service.ImportAsync(zipPath, password: "wrong");

            result.Success.Should().BeFalse();
            result.Error.Should().Be(ConfigBackupError.WrongPassword);
            var restored = await target.ConfigService.LoadSnapshotAsync(forceReload: true);
            restored.Profiles.Should().HaveCount(3);
            (await target.SecretStore.LoadAsync()).Values.Single().Label.Should().Be("Keep");
        }

        [Fact]
        public async Task Import_PackageWithoutSecrets_LeavesCurrentSecretsUntouched()
        {
            using var source = new Fixture();
            using var target = new Fixture();

            await source.ConfigService.SaveAsync(CreateConfig(profileCount: 2));
            string zipPath = source.GetPath("no-secrets.zip");
            (await source.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true))).Success.Should().BeTrue();

            await target.ConfigService.SaveAsync(CreateConfig(profileCount: 1));
            await target.SecretStore.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Keep", Account = "k", EncryptedData = target.Protector.Encrypt("keep") }
            });

            var result = await target.Service.ImportAsync(zipPath);

            result.Success.Should().BeTrue();
            (await target.SecretStore.LoadAsync()).Values.Single().Label.Should().Be("Keep");
        }

        [Fact]
        public async Task Inspect_ReportsSummaryFromPackage()
        {
            using var fixture = new Fixture();
            await fixture.ConfigService.SaveAsync(CreateConfig(profileCount: 2));
            string zipPath = fixture.GetPath("backup.zip");
            (await fixture.Service.ExportAsync(zipPath, new ConfigBackupExportOptions(IncludeSecrets: true))).Success.Should().BeTrue();

            var result = await fixture.Service.InspectAsync(zipPath);

            result.Success.Should().BeTrue();
            result.Summary!.ProfilesCount.Should().Be(2);
            result.Summary.CreatedAtUtc.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromMinutes(1));
        }

        [Fact]
        public async Task Import_MissingManifest_FailsInvalidPackageAndLeavesStateUntouched()
        {
            using var target = new Fixture();
            await target.ConfigService.SaveAsync(CreateConfig(profileCount: 2));
            string zipPath = target.GetPath("bad.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                await WriteEntryAsync(archive, "Profiles.json", JsonSerializer.SerializeToUtf8Bytes(CreateConfig(profileCount: 9)));
            }

            var before = await target.ConfigService.LoadSnapshotAsync(forceReload: true);
            var result = await target.Service.ImportAsync(zipPath);

            result.Success.Should().BeFalse();
            result.Error.Should().Be(ConfigBackupError.InvalidPackage);
            var after = await target.ConfigService.LoadSnapshotAsync(forceReload: true);
            after.Profiles.Should().HaveCount(before.Profiles.Count);
        }

        [Fact]
        public async Task Import_UnsupportedVersion_FailsAndLeavesStateUntouched()
        {
            using var target = new Fixture();
            await target.ConfigService.SaveAsync(CreateConfig(profileCount: 2));
            string zipPath = target.GetPath("future.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                await WriteEntryAsync(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new ConfigBackupManifest
                {
                    FormatVersion = 99,
                    AppVersion = "99.0.0",
                    CreatedAtUtc = DateTime.UtcNow
                }));
                await WriteEntryAsync(archive, "Profiles.json", JsonSerializer.SerializeToUtf8Bytes(CreateConfig(profileCount: 9)));
            }

            var result = await target.Service.ImportAsync(zipPath);

            result.Success.Should().BeFalse();
            result.Error.Should().Be(ConfigBackupError.UnsupportedVersion);
            var after = await target.ConfigService.LoadSnapshotAsync(forceReload: true);
            after.Profiles.Should().HaveCount(2);
        }

        [Fact]
        public async Task Import_CorruptConfig_FailsInvalidConfigAndLeavesStateUntouched()
        {
            using var target = new Fixture();
            await target.ConfigService.SaveAsync(CreateConfig(profileCount: 2));
            string zipPath = target.GetPath("corrupt.zip");
            using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
            {
                await WriteEntryAsync(archive, "manifest.json", JsonSerializer.SerializeToUtf8Bytes(new ConfigBackupManifest
                {
                    FormatVersion = 1,
                    AppVersion = "1.0.0",
                    CreatedAtUtc = DateTime.UtcNow
                }));
                await WriteEntryAsync(archive, "Profiles.json", Encoding.UTF8.GetBytes("this is not json"));
            }

            var result = await target.Service.ImportAsync(zipPath);

            result.Success.Should().BeFalse();
            result.Error.Should().Be(ConfigBackupError.InvalidConfig);
            var after = await target.ConfigService.LoadSnapshotAsync(forceReload: true);
            after.Profiles.Should().HaveCount(2);
        }

        [Fact]
        public async Task Import_MissingFile_ReturnsFileNotFound()
        {
            using var fixture = new Fixture();
            var result = await fixture.Service.ImportAsync(fixture.GetPath("does-not-exist.zip"));
            result.Success.Should().BeFalse();
            result.Error.Should().Be(ConfigBackupError.FileNotFound);
        }

        // ---- Helpers ----

        private static ProfilesConfig CreateConfig(int profileCount)
        {
            var config = new ProfilesConfig();
            for (int i = 0; i < profileCount; i++)
            {
                config.Profiles[$"app{i}"] = new ProcessProfile
                {
                    Alias = $"App {i}",
                    CommandMode = { new PluginSlot { PluginId = "com.pulsar.system", Action = "settings", Label = "Settings", IconKey = "E713" } },
                    SwitchMode = { new PluginSlot { PluginId = "com.pulsar.command", Action = "launch", Label = "Launch", IconKey = "E768" } }
                };
            }

            return config;
        }

        private static ConfigBackupManifest? ReadManifest(ZipArchive archive)
        {
            var entry = archive.GetEntry("manifest.json");
            if (entry == null) return null;
            using var reader = new StreamReader(entry.Open());
            return JsonSerializer.Deserialize<ConfigBackupManifest>(reader.ReadToEnd());
        }

        private static string ReadEntryText(ZipArchive archive, string name)
        {
            var entry = archive.GetEntry(name)!;
            using var reader = new StreamReader(entry.Open());
            return reader.ReadToEnd();
        }

        private static async Task WriteEntryAsync(ZipArchive archive, string name, byte[] bytes)
        {
            var entry = archive.CreateEntry(name);
            await using var stream = entry.Open();
            await stream.WriteAsync(bytes);
        }

        private sealed class Fixture : IDisposable
        {
            public string Root { get; } = Path.Combine(Path.GetTempPath(), "PulsarTests", "ConfigBackup", Guid.NewGuid().ToString("N"));
            public ConfigService ConfigService { get; }
            public SecretRepository SecretStore { get; }
            public FakeProtector Protector { get; }
            public ConfigBackupService Service { get; }

            public Fixture(string protectorPrefix = "SEAL:")
            {
                Directory.CreateDirectory(Root);
                Protector = new FakeProtector(protectorPrefix);
                ConfigService = new ConfigService(NullLogger<ConfigService>.Instance, configPath: Path.Combine(Root, "Profiles.json"));
                SecretStore = new SecretRepository(Path.Combine(Root, "secrets.json"));
                Service = new ConfigBackupService(ConfigService, SecretStore, Protector, NullLogger<ConfigBackupService>.Instance);
            }

            public string GetPath(string name) => System.IO.Path.Combine(Root, name);

            public void Dispose()
            {
                try { Directory.Delete(Root, recursive: true); } catch { /* best effort */ }
            }
        }

        private sealed class FakeProtector : ISecretProtector
        {
            private readonly string _prefix;

            public FakeProtector(string prefix)
            {
                _prefix = prefix;
            }

            public string Encrypt(string plainText)
            {
                return _prefix + Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
            }

            public string Decrypt(string encryptedBase64)
            {
                if (string.IsNullOrEmpty(encryptedBase64) || !encryptedBase64.StartsWith(_prefix, StringComparison.Ordinal))
                {
                    return string.Empty;
                }

                return Encoding.UTF8.GetString(Convert.FromBase64String(encryptedBase64.Substring(_prefix.Length)));
            }
        }
    }
}