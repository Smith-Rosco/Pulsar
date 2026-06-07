using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Windows; // Explicitly use WPF namespace
using CommunityToolkit.Mvvm.Messaging;
using Microsoft.Extensions.DependencyInjection;
using Pulsar.Core.Messages;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Views;

namespace Pulsar.Plugins.Core.SystemCommand
{
    public class SystemCommandPlugin : IPulsarPlugin, IPluginTiered, IPluginMetadataProvider
    {
        private const string OpenSettingsAction = "open-settings";
        private const string QuickAddProfileAction = "quick-add-profile";
        private const string LegacyOpenSettingsAction = "pulsar.system.open_settings";
        private const string LegacyQuickAddProfileAction = "pulsar.system.quick_add_profile";

        public string Id => "com.pulsar.system";
        public string DisplayName => "Pulsar Control";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Open Pulsar settings or jump into quick profile setup using explicit built-in actions.";
        public string Icon => "\uE713"; // Settings Icon
        public bool CanDisable => false; // Core Plugin
        public PluginTier Tier => PluginTier.Core;
        
        // 新增元数据属性
        public IEnumerable<string> Tags => new[] { "System", "Internal", "Core" };
        public string? DocumentationUrl => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "Plugins", "SystemCommand.md");

        private IServiceProvider? _services;

        public void Initialize(IServiceProvider services)
        {
            _services = services;
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            if (_services == null) return PluginResult.Error("System plugin not initialized");

            var command = ResolveCanonicalAction(action, args);

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
                        case OpenSettingsAction:
                            WeakReferenceMessenger.Default.Send(new OpenSettingsMessage("Global", "Settings"));
                            return PluginResult.Ok("Settings opened");

                        case QuickAddProfileAction:
                            if (!string.IsNullOrEmpty(context.TargetProcessName))
                            {
                                WeakReferenceMessenger.Default.Send(new OpenSettingsMessage(context.TargetProcessName, "Slots"));
                                return PluginResult.Ok($"Quick Add for {context.DisplayProcessName}");  // ✅ 使用格式化的进程名
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

        internal static string ResolveCanonicalAction(string action, IReadOnlyDictionary<string, string> args)
        {
            var resolved = action;
            if ((string.Equals(resolved, "run", StringComparison.OrdinalIgnoreCase)
                || string.Equals(resolved, "execute", StringComparison.OrdinalIgnoreCase))
                && args.TryGetValue("command", out var specificCommand)
                && !string.IsNullOrWhiteSpace(specificCommand))
            {
                resolved = specificCommand;
            }

            return resolved.ToLowerInvariant() switch
            {
                OpenSettingsAction => OpenSettingsAction,
                LegacyOpenSettingsAction => OpenSettingsAction,
                QuickAddProfileAction => QuickAddProfileAction,
                LegacyQuickAddProfileAction => QuickAddProfileAction,
                _ => resolved
            };
        }

        public PluginMetadata GetMetadata()
        {
            return new PluginMetadata
            {
                Id = Id,
                Display = new DisplayInfo
                {
                    Name = DisplayName,
                    Description = Description,
                    IconKey = Icon,
                    Category = "System",
                    Version = Version,
                    Author = Author,
                    DocumentationUrl = DocumentationUrl,
                    License = "MIT"
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "System",
                    AccentColor = "#607D8B",
                    ShowInQuickAccess = true,
                    SortOrder = 30,
                    IsFeatured = false
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { OpenSettingsAction, QuickAddProfileAction },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = false,
                    Tier = PluginTier.Core,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    [OpenSettingsAction] = new SlotActionMetadata
                    {
                        Name = OpenSettingsAction,
                        Label = "Open Settings",
                        Description = "Open Pulsar settings and jump to the global settings view.",
                        Aliases = new List<string> { LegacyOpenSettingsAction }
                    },
                    [QuickAddProfileAction] = new SlotActionMetadata
                    {
                        Name = QuickAddProfileAction,
                        Label = "Quick Add Current App",
                        Description = "Open the current app's slot configuration in Pulsar settings so you can add slots quickly.",
                        Aliases = new List<string> { LegacyQuickAddProfileAction }
                    }
                }
            };
        }
    }
}
