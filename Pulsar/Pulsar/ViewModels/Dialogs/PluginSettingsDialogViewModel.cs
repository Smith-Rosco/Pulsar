using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Base;
using Pulsar.ViewModels.Settings;
using DialogButtons = Pulsar.Models.Enums.DialogButtons;
using DialogResult = Pulsar.Models.Enums.DialogResult;

namespace Pulsar.ViewModels.Dialogs
{
    public partial class PluginSettingsDialogViewModel : ObservableObject, IDialogViewModel
    {
        private readonly PluginViewModel _pluginViewModel;

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

        /// <summary>仅 WinSwitcher 显示 "Window Inspector" 入口（诊断不可见窗口 + 一键排除）。</summary>
        public bool IsWindowInspectorVisible => _pluginViewModel.Id == WindowInspectorViewModel.WinSwitcherPluginId;

        public PluginSettingsDialogViewModel(PluginViewModel pluginViewModel)
        {
            _pluginViewModel = pluginViewModel;
            
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

        [RelayCommand]
        private async Task OpenWindowInspectorAsync()
        {
            var serviceProvider = _pluginViewModel.ServiceProvider;
            var dialogService = _pluginViewModel.DialogService;
            if (serviceProvider == null || dialogService == null)
            {
                return;
            }

            var windowService = serviceProvider.GetService<IWindowService>();
            if (windowService == null)
            {
                return;
            }

            var configService = serviceProvider.GetService<IConfigService>() ?? _pluginViewModel.ConfigService;
            var loc = serviceProvider.GetService<ILocalizationService>();
            var logger = serviceProvider.GetService<ILogger<WindowInspectorViewModel>>();

            var inspector = new WindowInspectorViewModel(windowService, configService, loc, logger);
            await inspector.InitializeAsync();

            await dialogService.ShowCustomAsync(
                loc?["Inspector.Title"] ?? "Window Inspector",
                inspector,
                DialogButtons.None,
                new Models.DialogSizeConstraints { Width = 780, Height = 560, MinWidth = 600, MinHeight = 400 });
        }

        public Task<bool> CanCloseAsync(DialogResult result)
        {
            if (result == DialogResult.Confirmed)
            {
                foreach (var setting in Settings)
                {
                    setting.Validate();
                }

                if (!Settings.All(s => s.IsValid))
                {
                    return Task.FromResult(false);
                }

                // Copying the value back raises PluginSettingViewModel.ValueChanged,
                // which PluginViewModel persists through its own config edit session.
                // No extra commit here: an unchanged-draft commit would only bump the
                // store revision and break a concurrent settings-editor save.
                foreach (var dialogSetting in Settings)
                {
                    var originalSetting = _pluginViewModel.Settings.FirstOrDefault(s => s.Key == dialogSetting.Key);
                    if (originalSetting != null)
                    {
                        originalSetting.Value = dialogSetting.Value;
                    }
                }
            }

            return Task.FromResult(true);
        }
    }
}
