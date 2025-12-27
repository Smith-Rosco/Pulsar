using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class ConfigService : IConfigService
    {
        private const string ConfigFileName = "config.json"; // 变更扩展名
        private string ConfigPath => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, ConfigFileName);

        private readonly JsonSerializerOptions _jsonOptions;

        public ConfigService()
        {
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNameCaseInsensitive = true,
                // 这一点至关重要：忽略 null 值以保持配置文件整洁
                DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
            };
        }

        public async Task<AppConfig> LoadAsync()
        {
            if (!File.Exists(ConfigPath))
            {
                var defaultConfig = CreateDefault();
                await SaveAsync(defaultConfig);
                return defaultConfig;
            }

            try
            {
                string content = await File.ReadAllTextAsync(ConfigPath);
                var config = JsonSerializer.Deserialize<AppConfig>(content, _jsonOptions);
                return config ?? CreateDefault();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Config Load Error: {ex.Message}");
                // 出错时备份旧配置并重置，防止应用崩溃
                File.Move(ConfigPath, ConfigPath + ".bak", true);
                return CreateDefault();
            }
        }

        public async Task SaveAsync(AppConfig config)
        {
            // 使用 FileStream 异步写入，更安全
            using var stream = File.Create(ConfigPath);
            await JsonSerializer.SerializeAsync(stream, config, _jsonOptions);
        }

        private AppConfig CreateDefault()
        {
            var config = new AppConfig();

            // 1. 默认 Switcher (LauncherItem)
            config.SwitcherSlots.Add(new LauncherItem { Slot = 1, Label = "Chrome", ProcessName = "chrome" });
            config.SwitcherSlots.Add(new LauncherItem { Slot = 2, Label = "Code", ProcessName = "code" });
            config.SwitcherSlots.Add(new LauncherItem { Slot = 8, Label = "Explorer", ProcessName = "explorer" });

            // 2. 默认 Global Commands (CommandItem)
            config.CommandLayer.GlobalSlots.Add(new CommandItem { Slot = 1, Label = "Task Mgr", ExePath = "taskmgr.exe" });
            config.CommandLayer.GlobalSlots.Add(new CommandItem { Slot = 3, Label = "Terminal", ExePath = "wt.exe" });

            // 3. 默认 Profile (Chrome)
            var chromeProfile = new AppProfile();
            chromeProfile.Slots.Add(new CommandItem { Slot = 1, Label = "New Tab", ExePath = "sendkeys", Arguments = "^t" }); // 暂用 sendkeys 占位
            config.CommandLayer.Profiles.Add("chrome", chromeProfile);

            return config;
        }
    }
}