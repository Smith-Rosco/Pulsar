// [Path]: Pulsar/Services/ConfigService.cs
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class ConfigService : IConfigService
    {
        private const string ConfigFileName = "pulsar_config.json";
        private readonly string _configPath;
        private AppConfig? _cachedConfig;

        public ConfigService()
        {
            // 保存到 AppData 目录，避免权限问题
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar");
            Directory.CreateDirectory(folder);
            _configPath = Path.Combine(folder, ConfigFileName);
        }

        public async Task<AppConfig> LoadAsync()
        {
            if (_cachedConfig != null) return _cachedConfig;

            if (!File.Exists(_configPath))
            {
                _cachedConfig = CreateDefaultConfig();
                await SaveAsync(_cachedConfig);
                return _cachedConfig;
            }

            try
            {
                using var stream = File.OpenRead(_configPath);
                var options = new JsonSerializerOptions
                {
                    WriteIndented = true,
                    PropertyNameCaseInsensitive = true
                };
                _cachedConfig = await JsonSerializer.DeserializeAsync<AppConfig>(stream, options);
            }
            catch
            {
                // 如果读取失败 (格式错误/版本不兼容)，生成默认配置
                _cachedConfig = CreateDefaultConfig();
            }

            return _cachedConfig ?? CreateDefaultConfig();
        }

        public async Task SaveAsync(AppConfig config)
        {
            _cachedConfig = config;
            var options = new JsonSerializerOptions { WriteIndented = true };
            using var stream = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(stream, config, options);
        }

        private AppConfig CreateDefaultConfig()
        {
            // [Fix] 生成符合新架构的默认数据
            return new AppConfig
            {
                // 1. 窗口切换器默认项
                Switcher = new List<GridItemBase>
                {
                    new LauncherItem { Slot = 1, Label = "Chrome", ProcessName = "chrome.exe", IconKey = "E710" },
                    new LauncherItem { Slot = 2, Label = "Code", ProcessName = "code.exe", IconKey = "E70F" },
                    new LauncherItem { Slot = 3, Label = "Term", ProcessName = "WindowsTerminal.exe", IconKey = "E756" }
                },

                // 2. 全局命令默认项
                Global = new List<GridItemBase>
                {
                    new CommandItem { Slot = 1, Label = "Copy", ExePath = "cmd.exe", Arguments = "/c echo Copy", IconKey = "E8C8" },
                    new CommandItem { Slot = 4, Label = "Paste", ExePath = "cmd.exe", Arguments = "/c echo Paste", IconKey = "E77F" }
                },

                // 3. 针对特定软件的配置 (示例)
                Profiles = new Dictionary<string, List<GridItemBase>>
                {
                    ["chrome"] = new List<GridItemBase>
                    {
                        new CommandItem { Slot = 1, Label = "New Tab", ExePath = "chrome.exe", Arguments = "--new-tab", IconKey = "E710" },
                        new CommandItem { Slot = 2, Label = "Incognito", ExePath = "chrome.exe", Arguments = "--incognito", IconKey = "E727" }
                    }
                },

                // 4. 默认设置
                Settings = new AppSettings
                {
                    TriggerDistance = 100,
                    Theme = "Dark",
                    HoverScale = 1.2,
                    Springiness = 6.0
                }
            };
        }
    }
}