using System.Collections.Generic;
using System.Linq;
using System;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Messages; // [New]
using CommunityToolkit.Mvvm.Messaging; // [New]
using Pulsar.Models;
using Pulsar.Services.Interfaces;
using Pulsar.Views;
using Pulsar.Helpers; // [New] For IconHelper

namespace Pulsar.ViewModels.Strategies
{
        public class CreateProfileStrategy : IActionStrategy
        {
            private readonly string _processName;
        private readonly Func<Task<string>> _exePathFactory;
        private readonly IConfigService _configService;
        private readonly System.IServiceProvider _serviceProvider;

        public CreateProfileStrategy(string processName, Func<Task<string>> exePathFactory, IConfigService configService, System.IServiceProvider serviceProvider)
        {
            _processName = processName;
            _exePathFactory = exePathFactory;
            _configService = configService;
            _serviceProvider = serviceProvider;
        }

        public async Task ExecuteAsync(SlotViewModel slot, RadialMenuViewModel context, CancellationToken cancellationToken = default)
        {
            // 1. Close Menu
            context.IsVisible = false;

            // 2. Add Profile if missing
            var config = await _configService.LoadAsync();
            if (!config.Profiles.ContainsKey(_processName))
            {
                // [New] Try Extract Icon
                string iconKey = "\uE71D"; // Default AppGeneric
                string exePath = await _exePathFactory();
                if (!string.IsNullOrEmpty(exePath))
                {
                    try
                    {
                        var iconSource = IconHelper.GetIconFromPath(exePath);
                        if (iconSource != null)
                        {
                            var cachePath = IconHelper.SaveIconToCache(iconSource, _processName);
                            if (!string.IsNullOrEmpty(cachePath))
                            {
                                iconKey = cachePath;
                            }
                        }
                    }
                    catch { /* Icon extraction failed, use default */ }
                }

                // Default Profile Template
                config.Profiles[_processName] = new ProcessProfile
                {
                    Icon = iconKey, // Use extracted icon
                    CommandMode = new List<PluginSlot>()
                };
                await _configService.SaveAsync(config);
            }

            // 3. Open Settings Window via Message (Decoupled & Robust)
            
            // Check if window exists, if not create it
            var existing = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            if (existing == null)
            {
                var settingsWindow = _serviceProvider.GetRequiredService<SettingsWindow>();
                settingsWindow.Show();
            }
            else
            {
                existing.Show();
                if (existing.WindowState == WindowState.Minimized) existing.WindowState = WindowState.Normal;
            }
            
            // Activate
            var win = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
            win?.Activate();

            // Send Navigation Message
            // SettingsViewModel subscribes to this and handles Refresh + Selection + View Switch
            WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(_processName, "Slots"));
        }
    }
}
