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

        [Fact]
        public async Task LoadAsync_ShouldRethrowAfterAllRetriesExhausted()
        {
            Directory.CreateDirectory(_tempDirectory);
            await File.WriteAllTextAsync(_filePath, "{}");

            using var lockStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.None);

            var repository = new SecretRepository(_filePath);

            var act = async () => await repository.LoadAsync();

            await act.Should().ThrowAsync<IOException>();
        }

        [Fact]
        public async Task SaveAsync_ShouldRethrowAfterAllRetriesExhausted()
        {
            Directory.CreateDirectory(_tempDirectory);
            var repository = new SecretRepository(_filePath);

            await repository.SaveAsync(new Dictionary<Guid, SecretPayload>());

            using var lockStream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.None);

            var act = async () => await repository.SaveAsync(new Dictionary<Guid, SecretPayload>
            {
                [Guid.NewGuid()] = new() { Label = "Test" }
            });

            await act.Should().ThrowAsync<IOException>();
        }

        [Fact]
        public async Task LoadAsync_ShouldReturnEmptyDictionary_WhenFileDoesNotExist()
        {
            var repository = new SecretRepository(_filePath);

            var result = await repository.LoadAsync();

            result.Should().NotBeNull();
            result.Should().BeEmpty();
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
