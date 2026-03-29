using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.Pki.Services;

namespace Pulsar.Tests.Plugins.Core.Pki
{
    public class SecretRepositoryTests : IDisposable
    {
        private readonly string _tempDirectory;
        private readonly string _filePath;

        public SecretRepositoryTests()
        {
            _tempDirectory = Path.Combine(Path.GetTempPath(), "PulsarTests", Guid.NewGuid().ToString("N"));
            _filePath = Path.Combine(_tempDirectory, "secrets.json");
        }

        [Fact]
        public async Task LoadAsync_ShouldReadExistingSecretsJsonShape()
        {
            Directory.CreateDirectory(_tempDirectory);
            var secretId = Guid.NewGuid();
            var payload = new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = "Payroll", Account = "ops@example.com", EncryptedData = "cipher" }
            };
            await File.WriteAllTextAsync(_filePath, JsonSerializer.Serialize(payload));

            var repository = new SecretRepository(_filePath);

            var loaded = await repository.LoadAsync();

            loaded.Should().ContainKey(secretId);
            loaded[secretId].Label.Should().Be("Payroll");
            loaded[secretId].Account.Should().Be("ops@example.com");
            loaded[secretId].EncryptedData.Should().Be("cipher");
        }

        [Fact]
        public async Task SaveAsync_ShouldPersistSecretsUsingExistingPayloadShape()
        {
            var secretId = Guid.NewGuid();
            var repository = new SecretRepository(_filePath);

            await repository.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = "Vault", Account = "ops@example.com", EncryptedData = "cipher" }
            });

            var json = await File.ReadAllTextAsync(_filePath);
            var parsed = JsonSerializer.Deserialize<Dictionary<Guid, SecretPayload>>(json);

            parsed.Should().NotBeNull();
            parsed.Should().ContainKey(secretId);
            parsed![secretId].Label.Should().Be("Vault");
            parsed[secretId].Account.Should().Be("ops@example.com");
            parsed[secretId].EncryptedData.Should().Be("cipher");
        }

        public void Dispose()
        {
            if (Directory.Exists(_tempDirectory))
            {
                Directory.Delete(_tempDirectory, true);
            }
        }
    }
}
