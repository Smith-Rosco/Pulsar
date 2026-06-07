using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;

namespace Pulsar.Plugins.Core.Pki.Services
{
    public class SecretRepository : IPkiSecretStore
    {
        private const string FileName = "secrets.json";
        private readonly string _filePath;

        public SecretRepository()
            : this(Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                "Pulsar",
                FileName))
        {
        }

        public SecretRepository(string filePath)
        {
            string folder = Path.GetDirectoryName(filePath) ?? throw new InvalidOperationException("Secret repository path must have a parent directory.");
            Directory.CreateDirectory(folder);
            _filePath = filePath;
        }

        public async Task<Dictionary<Guid, SecretPayload>> LoadAsync()
        {
            IOException? lastException = null;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (!File.Exists(_filePath)) return new Dictionary<Guid, SecretPayload>();

                    using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return await JsonSerializer.DeserializeAsync<Dictionary<Guid, SecretPayload>>(stream)
                           ?? new Dictionary<Guid, SecretPayload>();
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    if (i < 2) await Task.Delay(50);
                }
            }

            throw lastException!;
        }

        public async Task SaveAsync(Dictionary<Guid, SecretPayload> secrets)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };

            IOException? lastException = null;

            for (int i = 0; i < 3; i++)
            {
                try
                {
                    using var stream = File.Create(_filePath);
                    await JsonSerializer.SerializeAsync(stream, secrets, options);
                    return;
                }
                catch (IOException ex)
                {
                    lastException = ex;
                    if (i < 2) await Task.Delay(50);
                }
            }

            throw lastException!;
        }
    }
}
