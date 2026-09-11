// [Path]: Pulsar/Pulsar/Plugins/Core/WinSwitcher/WinSwitcherPlugin.Metadata.cs

using System;
using System.Collections.Generic;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Plugins.Core.WinSwitcher
{
    // [W3] The app / fallback-path / executable-path specs are defined exactly once here;
    // the three action metadata blocks compose them via named factories so the copies cannot drift.
    // [Architecture review 2026-09-11, candidate #2] The "arguments" spec is shared with other
    // plugins, so it now lives in SlotParameterSpecs instead of here.
    // Localization note: parameter/action Labels resolve via the SlotParam.* / SlotAction.* resx
    // convention — do not reword a Label without checking Resources/Strings*.resx key coverage.
    public partial class WinSwitcherPlugin
    {
        /// <summary>
        /// 获取插件元数据
        /// </summary>
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
                    Category = "Apps",
                    Version = Version,
                    Author = Author,
                    DocumentationUrl = DocumentationUrl,
                    License = "MIT",
                    IsPrimary = true
                },
                Schema = new ConfigSchema
                {
                    Version = 1,
                    Properties = new Dictionary<string, PropertySchema>
                    {
                        ["ExcludeProcesses"] = new PropertySchema
                        {
                            Type = "multiselect",
                            Description = "Process names excluded from discovery lists only; direct activate and switch actions still target them.",
                            DefaultValue = "",
                            Placeholder = "Select processes to exclude..."
                        },
                        ["EnableSwitchDiagnostics"] = new PropertySchema
                        {
                            Type = "bool",
                            Description = "Record detailed window eligibility and activation information for troubleshooting.",
                            DefaultValue = false
                        },
                        ["ExcludeRules"] = new PropertySchema
                        {
                            Type = "string",
                            Description = "JSON array of window-identity exclusion/allow rules. Use the Window Inspector to generate rules.",
                            DefaultValue = "",
                            Placeholder = "[{\"Allow\":false,\"WindowClass\":\"...\"}]"
                        }
                    },
                    RequiredProperties = Array.Empty<string>()
                },
                UI = new UIHints
                {
                    Badge = "Apps",
                    AccentColor = "#2196F3",
                    ShowInQuickAccess = true,
                    SortOrder = 5,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "switch", "launch", "activate" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = false,
                    Tier = PluginTier.Core,
                    MinPulsarVersion = "1.0.0",
                    HasCustomConfigDialog = true,
                    SupportsWindowInspector = true
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["switch"] = SwitchAction(),
                    ["launch"] = LaunchAction(),
                    ["activate"] = ActivateAction()
                }
            };
        }

        private static SlotActionMetadata SwitchAction() => new()
        {
            Name = "switch",
            Label = "Switch Or Launch",
            Description = "Switch to a running app window, or launch it when no matching window is found.",
            SuggestedLabelTemplate = "Switch to {app}",
            SuggestedIconKey = "E8AB",
            SuggestedColorHex = "#2196F3",
            Parameters = new List<SlotParameterMetadata>
            {
                AppParameter(),
                FallbackPathParameter(),
                SlotParameterSpecs.ArgumentsParameter(
                    SlotParameterGroup.Advanced,
                    "Optional command-line arguments passed when launching the app.",
                    "--profile-directory=Default",
                    hint: "Applied only when a new process is launched.")
            }
        };

        private static SlotActionMetadata LaunchAction() => new()
        {
            Name = "launch",
            Label = "Launch App",
            Description = "Always launch an app using an explicit executable path.",
            SuggestedLabelTemplate = "Launch {path}",
            SuggestedIconKey = "E8AB",
            SuggestedColorHex = "#2196F3",
            Parameters = new List<SlotParameterMetadata>
            {
                ExecutablePathParameter(),
                SlotParameterSpecs.ArgumentsParameter(
                    SlotParameterGroup.Optional,
                    "Optional command-line arguments passed to the target application.",
                    "--incognito")
            }
        };

        private static SlotActionMetadata ActivateAction() => new()
        {
            Name = "activate",
            Label = "Switch Existing App",
            Description = "Switch to an already running app window without launching a new instance.",
            SuggestedLabelTemplate = "Switch to {app}",
            SuggestedIconKey = "E8AB",
            SuggestedColorHex = "#2196F3",
            Parameters = new List<SlotParameterMetadata>
            {
                AppParameter()
            }
        };

        // One canonical "app" spec shared by switch and activate.
        private static SlotParameterMetadata AppParameter() => new()
        {
            Key = "app",
            Type = "string",
            Label = "Process Name",
            Description = "Executable process name used to find the target window.",
            IsRequired = true,
            Group = SlotParameterGroup.Required,
            SummaryLabel = "App",
            SummaryMode = SlotParameterSummaryMode.RawValue,
            ConfiguredSummaryText = "app selected",
            MissingSummaryText = "app missing",
            PresentationHint = SlotParameterPresentationHint.QuickEdit,
            QuickEditPriority = 100,
            Placeholder = "chrome",
            Example = "chrome",
            InputHint = "Use the process name without .exe.",
            ValidationHint = "Pick the running app by process name, without .exe.",
            PickerIntent = SlotPickerIntent.Process,
            Validators = new List<ValidationRule> { new RequiredValidator() }
        };

        // switch-only: optional fallback launch path used when no window is found.
        private static SlotParameterMetadata FallbackPathParameter() => new()
        {
            Key = "path",
            Type = "string",
            Label = "Launch Path",
            Description = "Optional fallback executable path used when the app is not already running.",
            IsRequired = false,
            Group = SlotParameterGroup.Optional,
            SummaryLabel = "Launch",
            SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
            ConfiguredSummaryText = "fallback ready",
            MissingSummaryText = "switch only",
            PresentationHint = SlotParameterPresentationHint.DialogOnly,
            Placeholder = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
            Example = "C:\\Program Files\\Google\\Chrome\\Application\\chrome.exe",
            InputHint = "Use an absolute path for reliable launching.",
            ValidationHint = "Add a fallback executable only if this slot should launch when no window is found.",
            PickerIntent = SlotPickerIntent.Process
        };

        // launch-only: required executable path.
        private static SlotParameterMetadata ExecutablePathParameter() => new()
        {
            Key = "path",
            Type = "string",
            Label = "Executable Path",
            Description = "Absolute path to the application to launch.",
            IsRequired = true,
            Group = SlotParameterGroup.Required,
            SummaryLabel = "App",
            SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
            ConfiguredSummaryText = "path ready",
            MissingSummaryText = "path missing",
            PresentationHint = SlotParameterPresentationHint.QuickEdit,
            QuickEditPriority = 100,
            Placeholder = "C:\\Windows\\System32\\notepad.exe",
            Example = "C:\\Windows\\System32\\notepad.exe",
            InputHint = "Use a full path to an executable, shortcut, or script.",
            ValidationHint = "Pick an executable, shortcut, or script to launch.",
            PickerIntent = SlotPickerIntent.Process,
            Validators = new List<ValidationRule> { new RequiredValidator() }
        };
    }
}
