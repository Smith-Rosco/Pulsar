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
            // [Fix] 增加重试逻辑
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    if (!File.Exists(_filePath)) return new Dictionary<Guid, SecretPayload>();

                    // 使用 FileShare.Read 允许其他进程同时读取
                    using var stream = new FileStream(_filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
                    return await JsonSerializer.DeserializeAsync<Dictionary<Guid, SecretPayload>>(stream)
                           ?? new Dictionary<Guid, SecretPayload>();
                }
                catch (IOException)
                {
                    if (i == 2) throw; // 最后一次失败则抛出
                    await Task.Delay(50); // 等待 50ms 后重试
                }
            }
            return new Dictionary<Guid, SecretPayload>();
        }

        public async Task SaveAsync(Dictionary<Guid, SecretPayload> secrets)
        {
            var options = new JsonSerializerOptions { WriteIndented = true };

            // [Fix] 增加重试逻辑
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    // Create 会请求独占访问权
                    using var stream = File.Create(_filePath);
                    await JsonSerializer.SerializeAsync(stream, secrets, options);
                    return; // 成功则退出
                }
                catch (IOException)
                {
                    if (i == 2) throw;
                    await Task.Delay(50);
                }
            }
        }
    }
}
