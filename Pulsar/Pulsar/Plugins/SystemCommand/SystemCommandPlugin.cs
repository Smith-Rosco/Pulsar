using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Windows; // Explicitly use WPF namespace
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin;
using Pulsar.Views;

namespace Pulsar.Plugins.SystemCommand
{
    public class SystemCommandPlugin : IPulsarPlugin
    {
        public string Id => "com.pulsar.system";
        public string DisplayName => "System Command";

        private IServiceProvider? _services;

        public void Initialize(IServiceProvider services)
        {
            _services = services;
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (_services == null) return PluginResult.Error("System plugin not initialized");

            var command = action;
            if ((command == "run" || command == "execute") && args.TryGetValue("command", out var specificCommand))
            {
                command = specificCommand;
            }

            return await System.Windows.Application.Current.Dispatcher.InvokeAsync<PluginResult>(() =>
            {
                try
                {
                    // Ensure Settings Window is Open
                    var settingsWindow = System.Windows.Application.Current.Windows.OfType<SettingsWindow>().FirstOrDefault();
                    if (settingsWindow == null)
                    {
                        settingsWindow = _services.GetRequiredService<SettingsWindow>();
                        settingsWindow.Show();
                    }
                    else
                    {
                        settingsWindow.Show();
                        if (settingsWindow.WindowState == WindowState.Minimized)
                            settingsWindow.WindowState = WindowState.Normal;
                        settingsWindow.Activate();
                    }

                    // Now handle the command
                    switch (command)
                    {
                        case "pulsar.system.open_settings":
                            WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Global", "Settings"));
                            return PluginResult.Ok("Settings opened");

                        case "pulsar.system.quick_add_profile":
                            if (!string.IsNullOrEmpty(context.TargetProcessName))
                            {
                                WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(context.TargetProcessName, "Slots"));
                                return PluginResult.Ok($"Quick Add for {context.TargetProcessName}");
                            }
                            else
                            {
                                return PluginResult.Error("No target process found in context");
                            }

                        default:
                            return PluginResult.Error($"Unknown system command: {command}");
                    }
                }
                catch (Exception ex)
                {
                    return PluginResult.Error($"Failed to execute system command: {ex.Message}");
                }
            });
        }
    }
}
