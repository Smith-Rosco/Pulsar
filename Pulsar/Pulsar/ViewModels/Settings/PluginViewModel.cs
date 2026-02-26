using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace Pulsar.ViewModels.Settings
{
    public partial class PluginViewModel : ObservableObject
    {
        private readonly IPulsarPlugin _plugin;
        private readonly PluginRegistry _registry;
        private readonly IConfigService _configService;

        [ObservableProperty]
        private bool _isEnabled;

        [ObservableProperty]
        private bool _hasSettings;

        public string Id => _plugin.Id;
        public string Name => _plugin.DisplayName;
        public string Description => _plugin.Description;
        public string Version => _plugin.Version;
        public string Author => _plugin.Author;
        public string Icon => _plugin.Icon;
        public bool CanDisable => _plugin.CanDisable;

        public ObservableCollection<PluginSettingViewModel> Settings { get; } = new();

        public PluginViewModel(IPulsarPlugin plugin, PluginRegistry registry, IConfigService configService)
        {
            _plugin = plugin;
            _registry = registry;
            _configService = configService;

            // Load Initial State
            _isEnabled = _registry.IsPluginEnabled(plugin.Id);

            // Load Settings if Configurable
            if (plugin is IPluginConfigurable configurable)
            {
                HasSettings = true;
                LoadSettings(configurable);
            }
        }

        private void LoadSettings(IPluginConfigurable configurable)
        {
            var defs = configurable.GetSettingsDefinition();
            var currentConfig = GetCurrentConfig();

            foreach (var def in defs)
            {
                object? value = null;
                
                if (currentConfig.TryGetValue(def.Key, out var rawValue))
                {
                    // JSON deserialization handling
                    if (rawValue is JsonElement element)
                    {
                        value = ConvertJsonElement(element, def.Type);
                    }
                    else
                    {
                        value = rawValue;
                    }
                }

                var vm = PluginSettingViewModel.Create(def, value);
                vm.ValueChanged += OnSettingChanged;
                Settings.Add(vm);
            }
        }

        private object? ConvertJsonElement(JsonElement element, PluginSettingType type)
        {
            try
            {
                return type switch
                {
                    PluginSettingType.Boolean => element.GetBoolean(),
                    PluginSettingType.Integer => element.GetInt32(),
                    _ => element.ToString()
                };
            }
            catch
            {
                return null;
            }
        }

        private Dictionary<string, object> GetCurrentConfig()
        {
            if (_configService.Current.Plugins.TryGetValue(Id, out var profile))
            {
                return profile.Config;
            }
            return new Dictionary<string, object>();
        }

        private void OnSettingChanged(string key, object? newValue)
        {
            // Update Config in Memory
            var config = _configService.Current;
            if (!config.Plugins.TryGetValue(Id, out var profile))
            {
                profile = new PluginProfile();
                config.Plugins[Id] = profile;
            }

            if (newValue != null)
            {
                profile.Config[key] = newValue;
            }
            else
            {
                profile.Config.Remove(key);
            }

            // Notify Plugin
            if (_plugin is IPluginConfigurable configurable)
            {
                configurable.UpdateSettings(profile.Config);
            }

            // Save to Disk (Debounced ideally, but direct for now)
            _ = _configService.SaveAsync(config);
        }

        [RelayCommand]
        private async Task ToggleStateAsync()
        {
            IsEnabled = !IsEnabled; // Optimistic UI update handled by two-way binding or property setter logic?
            // Actually, IsEnabled is bound to ToggleSwitch.
            // But we need to sync with Registry.
            
            // Wait, the property setter updates _isEnabled. 
            // We need to call Registry to persist.
            await _registry.SetPluginStateAsync(Id, IsEnabled);
        }

        // Custom setter logic to handle UI toggle
        partial void OnIsEnabledChanged(bool value)
        {
            // Fire and forget save
            _ = _registry.SetPluginStateAsync(Id, value);
        }
    }
}
