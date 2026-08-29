using System;
using System.Diagnostics;
using System.Reflection;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using Microsoft.Win32;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.Services.Interfaces;
using Pulsar.ViewModels.Dialogs;

namespace Pulsar.ViewModels
{
    public partial class AboutViewModel : ObservableObject
    {
        private readonly IConfigBackupService? _backupService;
        private readonly IDialogService? _dialogService;
        private readonly ILocalizationService? _loc;
        private readonly ILogger<AboutViewModel>? _logger;

        [ObservableProperty]
        private string _appName = "Pulsar";

        [ObservableProperty]
        private string _productName = "Pulsar Redux";

        [ObservableProperty]
        private string _appVersion;

        [ObservableProperty]
        private string _appDescription = "A high-performance radial menu for productivity.";

        [ObservableProperty]
        private string _copyright = "Copyright © 2026 Smith-Rosco";

        [ObservableProperty]
        private string _framework = ".NET 8.0";

        [ObservableProperty]
        private string _runtimeVersion;

        [ObservableProperty]
        private string _architecture;

        [ObservableProperty]
        private string _buildConfiguration;

        public AboutViewModel(
            IConfigBackupService? backupService = null,
            IDialogService? dialogService = null,
            ILocalizationService? loc = null,
            ILogger<AboutViewModel>? logger = null)
        {
            _backupService = backupService;
            _dialogService = dialogService;
            _loc = loc;
            _logger = logger;

            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            AppVersion = version != null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version 1.0.0";

            RuntimeVersion = Environment.Version.ToString();
            Architecture = Environment.Is64BitProcess ? "x64" : "x86";

#if DEBUG
            BuildConfiguration = "Debug";
#else
            BuildConfiguration = "Release";
#endif
        }

        [RelayCommand]
        private void OpenGitHub()
        {
            OpenUrl("https://github.com/Smith-Rosco/Pulsar");
        }

        [RelayCommand]
        private void OpenDocumentation()
        {
            OpenUrl("https://github.com/Smith-Rosco/Pulsar#readme");
        }

        [RelayCommand]
        private void OpenLicense()
        {
            OpenUrl("https://github.com/Smith-Rosco/Pulsar/blob/main/LICENSE");
        }

        [RelayCommand]
        private void CopySystemInfo()
        {
            var systemInfo = $@"Pulsar {AppVersion}
Product: {ProductName}
Runtime: .NET {RuntimeVersion}
Architecture: {Architecture}
Build: {BuildConfiguration}
OS: {Environment.OSVersion}
Machine: {Environment.MachineName}";

            try
            {
                System.Windows.Clipboard.SetText(systemInfo);
            }
            catch
            {
            }
        }

        [RelayCommand]
        private async Task ExportConfigAsync()
        {
            if (_backupService == null || _dialogService == null || _loc == null) return;

            var dialog = new SaveFileDialog
            {
                Title = _loc["Settings.About.ExportConfig"],
                Filter = _loc["Settings.About.BackupFileFilter"],
                DefaultExt = ".zip",
                FileName = $"Pulsar-Backup-{DateTime.Now:yyyyMMdd-HHmmss}.zip"
            };

            if (dialog.ShowDialog() != true) return;

            var optionsVm = new ConfigBackupOptionsViewModel(
                ConfigBackupOptionsViewModel.BackupOptionsMode.Export,
                _loc["Dialog.ConfigBackup.PasswordRequiredHint"],
                _loc["Dialog.ConfigBackup.PasswordMismatchHint"]);
            var optionsResult = await _dialogService.ShowCustomAsync(
                _loc["Settings.About.ExportOptionsTitle"],
                optionsVm,
                DialogButtons.OkCancel,
                DialogSizeConstraints.Small);
            if (optionsResult != DialogResult.Confirmed) return;

            var result = await _backupService.ExportAsync(
                dialog.FileName,
                new ConfigBackupExportOptions(optionsVm.IncludeSecrets, optionsVm.PasswordResult));
            if (result.Success && result.Summary != null)
            {
                await _dialogService.ShowMessageAsync(
                    _loc["Settings.About.ExportSuccess"],
                    string.Format(
                        _loc["Settings.About.ExportSuccessBodyFormat"],
                        result.Summary.ProfilesCount,
                        result.Summary.SlotsCount,
                        result.Summary.SecretCount),
                    DialogType.Success);
            }
            else
            {
                await ShowBackupErrorAsync(_loc["Settings.About.ExportFailed"], result);
            }
        }

        [RelayCommand]
        private async Task ImportConfigAsync()
        {
            if (_backupService == null || _dialogService == null || _loc == null) return;

            var dialog = new OpenFileDialog
            {
                Title = _loc["Settings.About.ImportConfig"],
                Filter = _loc["Settings.About.BackupFileFilter"],
                DefaultExt = ".zip"
            };

            if (dialog.ShowDialog() != true) return;

            var inspected = await _backupService.InspectAsync(dialog.FileName);
            if (!inspected.Success || inspected.Summary == null)
            {
                await ShowBackupErrorAsync(_loc["Settings.About.ImportFailed"], inspected);
                return;
            }

            var summary = inspected.Summary;
            string? password = null;
            if (summary.SecretsProtected)
            {
                var passwordVm = new ConfigBackupOptionsViewModel(
                    ConfigBackupOptionsViewModel.BackupOptionsMode.ImportPassword,
                    _loc["Dialog.ConfigBackup.PasswordRequiredHint"],
                    _loc["Dialog.ConfigBackup.PasswordMismatchHint"]);
                var passwordResult = await _dialogService.ShowCustomAsync(
                    _loc["Settings.About.BackupPasswordTitle"],
                    passwordVm,
                    DialogButtons.OkCancel,
                    DialogSizeConstraints.Small);
                if (passwordResult != DialogResult.Confirmed) return;
                password = passwordVm.PasswordResult;
                if (string.IsNullOrEmpty(password)) return;
            }

            var confirm = await _dialogService.ShowConfirmationAsync(
                _loc["Settings.About.ImportConfirmTitle"],
                string.Format(
                    _loc["Settings.About.ImportConfirmBodyFormat"],
                    summary.ProfilesCount,
                    summary.SlotsCount,
                    summary.SecretCount,
                    summary.SourceAppVersion,
                    summary.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm")),
                _loc["Settings.About.ImportConfirmYes"],
                _loc["Dialog.Button.Cancel"]);
            if (confirm != DialogResult.Confirmed) return;

            var result = await _backupService.ImportAsync(dialog.FileName, password);
            if (!result.Success)
            {
                await ShowBackupErrorAsync(_loc["Settings.About.ImportFailed"], result);
                return;
            }

            var restart = await _dialogService.ShowConfirmationAsync(
                _loc["Settings.About.ImportSuccess"],
                _loc["Settings.About.ImportRestartBody"],
                _loc["Settings.About.RestartNow"],
                _loc["Dialog.Button.Cancel"]);
            if (restart == DialogResult.Confirmed)
            {
                await RestartApplicationAsync();
            }
        }

        private async Task ShowBackupErrorAsync(string title, ConfigBackupResult result)
        {
            if (_dialogService == null || _loc == null) return;

            var message = result.Error switch
            {
                ConfigBackupError.FileNotFound => _loc["Settings.About.BackupError.FileNotFound"],
                ConfigBackupError.InvalidPackage => _loc["Settings.About.BackupError.InvalidPackage"],
                ConfigBackupError.UnsupportedVersion => _loc["Settings.About.BackupError.UnsupportedVersion"],
                ConfigBackupError.InvalidConfig => _loc["Settings.About.BackupError.InvalidConfig"],
                ConfigBackupError.InvalidSecrets => _loc["Settings.About.BackupError.InvalidSecrets"],
                ConfigBackupError.WrongPassword => _loc["Settings.About.BackupError.WrongPassword"],
                ConfigBackupError.SecretProtectionFailed => _loc["Settings.About.BackupError.SecretProtectionFailed"],
                ConfigBackupError.IoError => _loc["Settings.About.BackupError.IoError"],
                _ => _loc["Settings.About.BackupError.IoError"]
            };

            _logger?.LogWarning("[AboutViewModel] Backup operation failed with {Error}: {Detail}", result.Error, result.Detail);
            await _dialogService.ShowMessageAsync(title, message, DialogType.Error, DialogButtons.Ok);
        }

        private async Task RestartApplicationAsync()
        {
            var processPath = Environment.ProcessPath;
            if (string.IsNullOrEmpty(processPath)) return;

            try
            {
                Process.Start(processPath);
                System.Windows.Application.Current.Shutdown();
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[AboutViewModel] Failed to restart Pulsar");
                if (_dialogService != null && _loc != null)
                {
                    await _dialogService.ShowMessageAsync(
                        _loc["Settings.About.RestartFailedTitle"],
                        string.Format(_loc["Settings.About.RestartFailedBodyFormat"], ex.Message),
                        DialogType.Error,
                        DialogButtons.Ok);
                }
            }
        }

        private void OpenUrl(string url)
        {
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = url,
                    UseShellExecute = true
                });
            }
            catch
            {
            }
        }
    }
}