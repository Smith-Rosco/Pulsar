using System;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Models;
using Pulsar.Models.Settings;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels;
using Pulsar.ViewModels.Settings;
using Pulsar.Views.Controls;

namespace Pulsar.Views.Pages
{
    public partial class SettingsSlotsPage : Page
    {
        private readonly SettingsViewModel _viewModel;
        private readonly IThemeService _themeService;
        private readonly SlotWheelEditorViewModel _wheelViewModel;
        private ProfileSettings? _hookedGeneralSettings;

        public SettingsSlotsPage()
            : this(
                App.Current.Services.GetRequiredService<SettingsViewModel>(),
                App.Current.Services.GetRequiredService<IThemeService>())
        {
        }

        public SettingsSlotsPage(SettingsViewModel viewModel, IThemeService themeService)
        {
            InitializeComponent();
            _viewModel = viewModel;
            _themeService = themeService;

            DataContext = viewModel;
            _wheelViewModel = App.Current.Services.GetRequiredService<SlotWheelEditorViewModel>();
            WheelEditor.DataContext = _wheelViewModel;

            themeService.ApplyTheme(this, themeService.CurrentTheme);
        }

        protected override void OnInitialized(EventArgs e)
        {
            base.OnInitialized(e);
            Loaded += OnPageLoaded;
            Unloaded += OnPageUnloaded;
        }

        private void OnPageLoaded(object sender, RoutedEventArgs e)
        {
            // Re-subscribe idempotently (Loaded may fire again after navigation returns).
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            SyncWheelToContext();
        }

        private void OnPageUnloaded(object sender, RoutedEventArgs e)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            UnhookGeneralSettings();
        }

        private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(SettingsViewModel.CurrentSlots)
                || e.PropertyName == nameof(SettingsViewModel.CurrentContext)
                || e.PropertyName == nameof(SettingsViewModel.GeneralSettings))
            {
                SyncWheelToContext();
            }
        }

        private void SyncWheelToContext()
        {
            _wheelViewModel.SetSlots(
                _viewModel.CurrentSlots,
                _viewModel.GeneralSettings?.SlotsPerPage ?? 8);
            WireGeneralSettings();
        }

        private void WireGeneralSettings()
        {
            if (ReferenceEquals(_hookedGeneralSettings, _viewModel.GeneralSettings))
            {
                return;
            }

            UnhookGeneralSettings();
            _hookedGeneralSettings = _viewModel.GeneralSettings;
            if (_hookedGeneralSettings != null)
            {
                _hookedGeneralSettings.PropertyChanged += OnGeneralSettingsPropertyChanged;
            }
        }

        private void UnhookGeneralSettings()
        {
            if (_hookedGeneralSettings != null)
            {
                _hookedGeneralSettings.PropertyChanged -= OnGeneralSettingsPropertyChanged;
                _hookedGeneralSettings = null;
            }
        }

        private void OnGeneralSettingsPropertyChanged(object? sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == nameof(ProfileSettings.SlotsPerPage))
            {
                _wheelViewModel.RefreshLayout(_viewModel.GeneralSettings?.SlotsPerPage ?? 8);
            }
        }

        private async void WheelEditor_EditRequested(object? sender, SlotWheelActionEventArgs e)
        {
            await _viewModel.OpenSlotConfiguration(e.Slot);
        }

        private async void WheelEditor_DeleteRequested(object? sender, SlotWheelActionEventArgs e)
        {
            await _viewModel.RemoveSlot(e.Slot);
        }

        private void WheelEditor_AddSlotRequested(object? sender, EventArgs e)
        {
            _viewModel.AddSlotDialogCommand.Execute(null);
        }
    }
}
