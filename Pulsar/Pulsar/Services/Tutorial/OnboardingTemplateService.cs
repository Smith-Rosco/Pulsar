using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Tutorial;

namespace Pulsar.Services.Tutorial
{
    public static class OnboardingProfileExtensions
    {
        public static string GetSlotDescriptionKey(this OnboardingUsageProfile profile) => profile switch
        {
            OnboardingUsageProfile.DeveloperWorkflow => "FirstLaunch.DeveloperWorkflowSlotDesc",
            OnboardingUsageProfile.BrowserAndDocs => "FirstLaunch.BrowserDocsSlotDesc",
            _ => "FirstLaunch.GeneralProductivitySlotDesc"
        };
    }

    public enum OnboardingUsageProfile
    {
        GeneralProductivity,
        DeveloperWorkflow,
        BrowserAndDocs
    }

    public sealed class OnboardingAppSelection
    {
        public required string Id { get; init; }

        public required string DisplayName { get; init; }

        public required string ProcessName { get; init; }

        public required string LaunchPath { get; init; }

        public required string IconKey { get; init; }
    }

    public sealed class OnboardingTemplateRequest
    {
        public required OnboardingUsageProfile Profile { get; init; }

        public required IReadOnlyList<OnboardingAppSelection> SelectedApps { get; init; }
    }

    public interface IOnboardingTemplateService
    {
        IReadOnlyList<OnboardingAppSelection> GetAvailableApps();

        ProfilesConfig BuildInitialConfig(OnboardingTemplateRequest request);

        ProfilesConfig BuildInitialConfig(TutorialScenario scenario, IReadOnlyList<OnboardingAppSelection> selectedApps);
    }

    public sealed class OnboardingTemplateService : IOnboardingTemplateService
    {
        private readonly ILocalizationService? _loc;

        public OnboardingTemplateService(ILocalizationService? localizationService = null)
        {
            _loc = localizationService;
        }

        private static readonly IReadOnlyList<OnboardingAppSelection> AvailableApps = new List<OnboardingAppSelection>
        {
            new() { Id = "chrome", DisplayName = "Google Chrome", ProcessName = "chrome", LaunchPath = "chrome.exe", IconKey = "\uE774" },
            new() { Id = "edge", DisplayName = "Microsoft Edge", ProcessName = "msedge", LaunchPath = "msedge.exe", IconKey = "\uE774" },
            new() { Id = "excel", DisplayName = "Microsoft Excel", ProcessName = "excel", LaunchPath = "EXCEL.EXE", IconKey = "\uE736" },
            new() { Id = "explorer", DisplayName = "File Explorer", ProcessName = "explorer", LaunchPath = "explorer.exe", IconKey = "\uE8B7" },
            new() { Id = "notepad", DisplayName = "Notepad", ProcessName = "notepad", LaunchPath = "notepad.exe", IconKey = "\uE70F" },
            new() { Id = "powershell", DisplayName = "PowerShell", ProcessName = "powershell", LaunchPath = "powershell.exe", IconKey = "\uE756" },
            new() { Id = "vscode", DisplayName = "Visual Studio Code", ProcessName = "code", LaunchPath = "code.exe", IconKey = "\uE943" }
        };

        public IReadOnlyList<OnboardingAppSelection> GetAvailableApps()
        {
            return AvailableApps;
        }

        internal static string ResolveExePath(string processName, string launchPath)
        {
            if (!string.IsNullOrWhiteSpace(launchPath))
            {
                string fullPath = launchPath;
                if (!Path.IsPathRooted(fullPath))
                {
                    string systemDir = Environment.GetFolderPath(Environment.SpecialFolder.System);
                    string sysPath = Path.Combine(systemDir, fullPath);
                    if (File.Exists(sysPath)) return sysPath;

                    var paths = Environment.GetEnvironmentVariable("PATH")?.Split(Path.PathSeparator) ?? [];
                    foreach (var dir in paths)
                    {
                        string candidate = Path.Combine(dir, fullPath);
                        if (File.Exists(candidate)) return candidate;
                    }
                }
                else if (File.Exists(fullPath))
                {
                    return fullPath;
                }
            }

            string systemDir2 = Environment.GetFolderPath(Environment.SpecialFolder.System);
            string exePath2 = Path.Combine(systemDir2, $"{processName}.exe");
            if (File.Exists(exePath2)) return exePath2;

            return launchPath ?? $"{processName}.exe";
        }

        private static string ResolveSystemDisplayName(string processName, string launchPath, string fallback)
        {
            try
            {
                string exePath = ResolveExePath(processName, launchPath);
                if (File.Exists(exePath))
                {
                    var info = FileVersionInfo.GetVersionInfo(exePath);
                    if (!string.IsNullOrWhiteSpace(info.FileDescription))
                        return info.FileDescription;
                }
            }
            catch { }
            return fallback;
        }

        internal static string ResolveIconKey(string processName, string launchPath, string fallbackIconKey)
        {
            try
            {
                string exePath = ResolveExePath(processName, launchPath);
                if (!File.Exists(exePath)) return fallbackIconKey;

                using var icon = Icon.ExtractAssociatedIcon(exePath);
                if (icon == null) return fallbackIconKey;

                string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
                string folder = Path.Combine(appData, "Pulsar", "Cache", "Icons");
                Directory.CreateDirectory(folder);
                string safeName = string.Join("_", processName.Split(Path.GetInvalidFileNameChars()));
                string filePath = Path.Combine(folder, $"{safeName}.png");

                if (!File.Exists(filePath))
                {
                    using var bitmap = icon.ToBitmap();
                    bitmap.Save(filePath, System.Drawing.Imaging.ImageFormat.Png);
                }
                return filePath;
            }
            catch
            {
                return fallbackIconKey;
            }
        }

        public ProfilesConfig BuildInitialConfig(OnboardingTemplateRequest request)
        {
            if (request.SelectedApps == null || request.SelectedApps.Count == 0)
            {
                throw new InvalidOperationException("At least one app must be selected for onboarding.");
            }

            var orderedApps = GetOrderedApps(request.SelectedApps);

            var switchSlots = BuildSwitchSlots(orderedApps);
            var commandSlots = new List<PluginSlot>
            {
                BuildCommandExample(request.Profile, orderedApps)
            };

            return CreateProfilesConfig(switchSlots, commandSlots);
        }

        public ProfilesConfig BuildInitialConfig(TutorialScenario scenario, IReadOnlyList<OnboardingAppSelection> selectedApps)
        {
            if (selectedApps == null || selectedApps.Count == 0)
            {
                throw new InvalidOperationException("At least one app must be selected for onboarding.");
            }

            var orderedApps = GetOrderedApps(selectedApps);
            var switchSlots = BuildSwitchSlots(orderedApps);
            var commandSlots = BuildCommandExamples(scenario.CommandSlotTemplates);

            return CreateProfilesConfig(switchSlots, commandSlots);
        }

        private static List<OnboardingAppSelection> GetOrderedApps(IReadOnlyList<OnboardingAppSelection> apps)
        {
            return apps
                .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(6)
                .ToList();
        }

        private static List<PluginSlot> BuildSwitchSlots(IReadOnlyList<OnboardingAppSelection> orderedApps)
        {
            var switchSlots = new List<PluginSlot>();
            int slotIndex = 1;

            foreach (var app in orderedApps)
            {
                string displayName = ResolveSystemDisplayName(app.ProcessName, app.LaunchPath, app.DisplayName);
                string iconKey = ResolveIconKey(app.ProcessName, app.LaunchPath, app.IconKey);

                switchSlots.Add(new PluginSlot
                {
                    Slot = slotIndex++,
                    PluginId = "com.pulsar.winswitcher",
                    Action = "switch",
                    Args = new Dictionary<string, string>
                    {
                        ["app"] = app.ProcessName,
                        ["path"] = app.LaunchPath
                    },
                    Label = displayName,
                    IconKey = iconKey
                });
            }

            return switchSlots;
        }

        private static ProfilesConfig CreateProfilesConfig(List<PluginSlot> switchSlots, List<PluginSlot> commandSlots)
        {
            return new ProfilesConfig
            {
                Settings = new ProfileSettings
                {
                    CenterSlotBehavior = "MRU_Window",
                    TriggerDistance = 100.0,
                    LauncherTheme = "Light",
                    SettingsTheme = "Light",
                    HoverScale = 1.2,
                    Springiness = 6.0,
                    MaxDisplacement = 20.0,
                    HasCompletedTutorial = false,
                    LastTutorialStep = null,
                    OnboardingState = "SetupWizardComplete",
                    ConfigCreatedAt = DateTime.UtcNow
                },
                Profiles = new Dictionary<string, ProcessProfile>(StringComparer.OrdinalIgnoreCase)
                {
                    ["Global"] = new ProcessProfile
                    {
                        SwitchMode = switchSlots,
                        CommandMode = commandSlots
                    }
                }
            };
        }

        private List<PluginSlot> BuildCommandExamples(IReadOnlyList<CommandSlotTemplate> templates)
        {
            var slots = new List<PluginSlot>();
            int slotIndex = 1;

            foreach (var template in templates)
            {
                slots.Add(new PluginSlot
                {
                    Slot = slotIndex++,
                    PluginId = template.PluginId,
                    Action = template.Action,
                    Args = new Dictionary<string, string>(template.Args),
                    Label = _loc?[template.LabelKey] ?? template.LabelKey,
                    IconKey = template.IconKey
                });
            }

            return slots;
        }

        private PluginSlot BuildCommandExample(OnboardingUsageProfile profile, IReadOnlyList<OnboardingAppSelection> selectedApps)
        {
            var browser = selectedApps.FirstOrDefault(app =>
                app.ProcessName.Equals("chrome", StringComparison.OrdinalIgnoreCase)
                || app.ProcessName.Equals("msedge", StringComparison.OrdinalIgnoreCase));

            return profile switch
            {
                OnboardingUsageProfile.DeveloperWorkflow => new PluginSlot
                {
                    Slot = 1,
                    PluginId = "com.pulsar.command",
                    Action = "run",
                    Args = new Dictionary<string, string>
                    {
                        ["path"] = "powershell.exe",
                        ["arguments"] = "-NoExit -Command Get-Date"
                    },
                    Label = "Open PowerShell",
                    IconKey = "\uE756"
                },
                OnboardingUsageProfile.BrowserAndDocs when browser != null => new PluginSlot
                {
                    Slot = 1,
                    PluginId = "com.pulsar.command",
                    Action = "run",
                    Args = new Dictionary<string, string>
                    {
                        ["path"] = "https://learn.microsoft.com/windows/"
                    },
                    Label = "Open Windows Docs",
                    IconKey = browser.IconKey
                },
                _ => new PluginSlot
                {
                    Slot = 1,
                    PluginId = "com.pulsar.command",
                    Action = "sendkeys",
                    Args = new Dictionary<string, string>
                    {
                        ["keys"] = "Hello from Pulsar!"
                    },
                    Label = _loc?["CommandSlot.InsertSampleText"] ?? "Insert Sample Text",
                    IconKey = "\uE756"
                }
            };
        }
    }
}
