using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;

namespace Pulsar.Services.Tutorial
{
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
    }

    public sealed class OnboardingTemplateService : IOnboardingTemplateService
    {
        private static readonly IReadOnlyList<OnboardingAppSelection> AvailableApps = new List<OnboardingAppSelection>
        {
            new() { Id = "chrome", DisplayName = "Google Chrome", ProcessName = "chrome", LaunchPath = "chrome.exe", IconKey = "\uE774" },
            new() { Id = "edge", DisplayName = "Microsoft Edge", ProcessName = "msedge", LaunchPath = "msedge.exe", IconKey = "\uE774" },
            new() { Id = "explorer", DisplayName = "File Explorer", ProcessName = "explorer", LaunchPath = "explorer.exe", IconKey = "\uE8B7" },
            new() { Id = "notepad", DisplayName = "Notepad", ProcessName = "notepad", LaunchPath = "notepad.exe", IconKey = "\uE70F" },
            new() { Id = "powershell", DisplayName = "PowerShell", ProcessName = "powershell", LaunchPath = "powershell.exe", IconKey = "\uE756" },
            new() { Id = "vscode", DisplayName = "Visual Studio Code", ProcessName = "code", LaunchPath = "code.exe", IconKey = "\uE943" }
        };

        public IReadOnlyList<OnboardingAppSelection> GetAvailableApps()
        {
            return AvailableApps;
        }

        public ProfilesConfig BuildInitialConfig(OnboardingTemplateRequest request)
        {
            if (request.SelectedApps == null || request.SelectedApps.Count == 0)
            {
                throw new InvalidOperationException("At least one app must be selected for onboarding.");
            }

            var orderedApps = request.SelectedApps
                .GroupBy(app => app.Id, StringComparer.OrdinalIgnoreCase)
                .Select(group => group.First())
                .Take(6)
                .ToList();

            var switchSlots = new List<PluginSlot>();
            int slotIndex = 1;

            foreach (var app in orderedApps)
            {
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
                    Label = app.DisplayName,
                    IconKey = app.IconKey
                });
            }

            var commandSlots = new List<PluginSlot>
            {
                BuildCommandExample(request.Profile, orderedApps)
            };

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

        private static PluginSlot BuildCommandExample(OnboardingUsageProfile profile, IReadOnlyList<OnboardingAppSelection> selectedApps)
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
                    Action = "run",
                    Args = new Dictionary<string, string>
                    {
                        ["path"] = "notepad.exe"
                    },
                    Label = "Open Notepad",
                    IconKey = "\uE70F"
                }
            };
        }
    }
}
