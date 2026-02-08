using Pulsar.Models;
using Pulsar.Services.Interfaces;
using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace Pulsar.Services
{
    /// <summary>
    /// 配置服务 - 读写 Profiles.json
    /// </summary>
    public class ConfigService : IConfigService
    {
        private const string ConfigFileName = "Profiles.json";
        private readonly string _configPath;
        private ProfilesConfig? _cachedConfig;

        public event Action? ConfigUpdated;

        public ProfilesConfig Current => _cachedConfig ?? CreateDefaultConfig();

        public ConfigService()
        {
            string folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Pulsar");
            Directory.CreateDirectory(folder);
            _configPath = Path.Combine(folder, ConfigFileName);
        }

        public async Task<ProfilesConfig> LoadAsync()
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
                _cachedConfig = await JsonSerializer.DeserializeAsync<ProfilesConfig>(stream, options);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ConfigService] Failed to load config: {ex.Message}");
                _cachedConfig = CreateDefaultConfig();
            }

            return _cachedConfig ?? CreateDefaultConfig();
        }

        public async Task SaveAsync(ProfilesConfig config)
        {
            _cachedConfig = config;
            var options = new JsonSerializerOptions 
            { 
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase
            };

            using var stream = File.Create(_configPath);
            await JsonSerializer.SerializeAsync(stream, config, options);

            ConfigUpdated?.Invoke();
        }

        /// <summary>
        /// 创建默认配置
        /// </summary>
        private ProfilesConfig CreateDefaultConfig()
        {
            var config = new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    CenterSlotBehavior = "MRU_Window",
                    TriggerDistance = 100.0,
                    LauncherTheme = "Dark",
                    HoverScale = 1.2,
                    Springiness = 6.0,
                    MaxDisplacement = 20.0
                },
                Profiles = new Dictionary<string, ProcessProfile>
                {
                    // Global 配置 - 窗口切换模式
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = new Dictionary<string, PluginSlot>
                        {
                            ["Slot_1"] = new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "activate",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "chrome",
                                }
                            },
                            ["Slot_2"] = new PluginSlot
                            {
                                Slot = 2,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "activate",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "code",
                                }
                            },
                            ["Slot_3"] = new PluginSlot
                            {
                                Slot = 3,
                                PluginId = "com.pulsar.winswitcher",
                                Action = "activate",
                                Args = new Dictionary<string, string>
                                {
                                    ["app"] = "WindowsTerminal",
                                }
                            }
                        },
                         CommandMode = new Dictionary<string, PluginSlot>
                        {
                            ["Slot_1"] = new PluginSlot
                            {
                                Slot = 1,
                                PluginId = "com.pulsar.command",
                                Action = "run",
                                Args = new Dictionary<string, string>
                                {
                                    ["path"] = "cmd.exe",
                                }
                            }
                        }
                    }
                }
            };

            return config;
        }
    }
}