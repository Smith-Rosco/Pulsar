using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using Pulsar.ViewModels.Settings;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class PluginSettingsDialogViewModel : ObservableObject, IDialogViewModel
    {
        private readonly PluginViewModel _pluginViewModel;
        private readonly IConfigService _configService;

        [ObservableProperty]
        private string _title;

        [ObservableProperty]
        private string _pluginName;

        [ObservableProperty]
        private string _pluginDescription;

        [ObservableProperty]
        private string _pluginIcon;

        [ObservableProperty]
        private ObservableCollection<PluginSettingViewModel> _settings;

        [ObservableProperty]
        private bool _canSave = true;

        public Action<DialogResult>? RequestClose { get; set; }

        public PluginSettingsDialogViewModel(PluginViewModel pluginViewModel, IConfigService configService)
        {
            _pluginViewModel = pluginViewModel;
            _configService = configService;
            
            _title = $"Configure {pluginViewModel.Name}";
            _pluginName = pluginViewModel.Name;
            _pluginDescription = pluginViewModel.Description;
            _pluginIcon = pluginViewModel.Icon;
            _settings = new ObservableCollection<PluginSettingViewModel>(pluginViewModel.Settings);

            foreach (var setting in _settings)
            {
                setting.PropertyChanged += (s, e) =>
                {
                    if (e.PropertyName == nameof(PluginSettingViewModel.IsValid))
                    {
                        UpdateCanSave();
                    }
                };
            }

            UpdateCanSave();
        }

        private void UpdateCanSave()
        {
            CanSave = Settings.All(s => s.IsValid);
        }

        [RelayCommand]
        private void Save()
        {
            RequestClose?.Invoke(DialogResult.Confirmed);
        }

        [RelayCommand]
        private void Cancel()
        {
            RequestClose?.Invoke(DialogResult.Cancelled);
        }

        [RelayCommand]
        private void ResetToDefaults()
        {
            foreach (var setting in Settings)
            {
                setting.ResetToDefault();
            }
        }

        public async Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                foreach (var setting in Settings)
                {
                    setting.Validate();
                }

                if (!Settings.All(s => s.IsValid))
                {
                    return false;
                }

                foreach (var dialogSetting in Settings)
                {
                    var originalSetting = _pluginViewModel.Settings.FirstOrDefault(s => s.Key == dialogSetting.Key);
                    if (originalSetting != null)
                    {
                        originalSetting.Value = dialogSetting.Value;
                    }
                }

                await _configService.SaveAsync(_configService.Current);
            }

            return true;
        }
    }
}
