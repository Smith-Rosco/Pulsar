using System;
using System.Diagnostics;
using System.Reflection;
using System.Windows.Input;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Pulsar.ViewModels
{
    public partial class AboutViewModel : ObservableObject
    {
        [ObservableProperty]
        private string _appName = "Pulsar";

        [ObservableProperty]
        private string _appVersion;

        [ObservableProperty]
        private string _appDescription = "A high-performance productivity launcher for Windows featuring a radial menu interface.";

        [ObservableProperty]
        private string _copyright = "Copyright © 2026 Pulsar Lab";

        [ObservableProperty]
        private string _framework = ".NET 8.0";

        [ObservableProperty]
        private string _architecture;

        [ObservableProperty]
        private string _buildConfiguration;

        public AboutViewModel()
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            AppVersion = version != null ? $"Version {version.Major}.{version.Minor}.{version.Build}" : "Version 2.4.0";

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
            OpenUrl("https://github.com/pulsar-lab/pulsar");
        }

        [RelayCommand]
        private void OpenDocumentation()
        {
            OpenUrl("https://github.com/pulsar-lab/pulsar/wiki");
        }

        [RelayCommand]
        private void OpenLicense()
        {
            OpenUrl("https://github.com/pulsar-lab/pulsar/blob/main/LICENSE");
        }

        [RelayCommand]
        private void CopySystemInfo()
        {
            var systemInfo = $@"Pulsar {AppVersion}
Framework: {Framework}
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
                // Clipboard operation failed
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
                // Failed to open URL
            }
        }
    }
}
