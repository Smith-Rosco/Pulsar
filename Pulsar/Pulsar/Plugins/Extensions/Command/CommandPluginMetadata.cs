using System;
using System.Collections.Generic;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;

namespace Pulsar.Plugins.Extensions.Command
{
    public static class CommandPluginMetadata
    {
        public static PluginMetadata Create(
            string id,
            string displayName,
            string description,
            string icon,
            string version,
            string author,
            string? documentationUrl,
            string license,
            bool canDisable,
            PluginTier tier)
        {
            return new PluginMetadata
            {
                Id = id,
                Display = new DisplayInfo
                {
                    Name = displayName,
                    Description = description,
                    IconKey = icon,
                    Category = "Automation",
                    Version = version,
                    Author = author,
                    DocumentationUrl = documentationUrl,
                    License = license,
                    IsPrimary = true
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "Cmd",
                    AccentColor = "#32CD32",
                    ShowInQuickAccess = true,
                    SortOrder = 20,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "run", "sendkeys" },
                    RequiresForegroundWindow = false,
                    Dependencies = new List<string>(),
                    CanDisable = canDisable,
                    Tier = tier,
                    MinPulsarVersion = null!
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["run"] = new SlotActionMetadata
                    {
                        Name = "run",
                        Label = "Open Target",
                        Description = "Open an app, document, folder, or URL through Windows shell execution.",
                        SuggestedLabelTemplate = "Open {path}",
                        SuggestedIconKey = "E756",
                        SuggestedColorHex = "#32CD32",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "path",
                                Type = "string",
                                Label = "Path",
                                Description = "Executable path, file path, or URL to open.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Target",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "target ready",
                                MissingSummaryText = "target missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "cmd.exe",
                                Example = "C:\\Windows\\System32\\cmd.exe",
                                InputHint = "You can use an executable path or shell-openable target.",
                                ValidationHint = "Pick an executable, document, folder, or URL to open.",
                                PickerIntent = SlotPickerIntent.Process,
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            },
                            new()
                            {
                                Key = "arguments",
                                Type = "string",
                                Label = "Arguments",
                                Description = "Optional command-line arguments.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Args",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "args set",
                                MissingSummaryText = "no args",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "/k echo Hello",
                                Example = "/c start https://example.com"
                            },
                            new()
                            {
                                Key = "workingDir",
                                Type = "string",
                                Label = "Folder",
                                Description = "Optional starting directory for the process.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Advanced,
                                SummaryLabel = "Folder",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "folder set",
                                MissingSummaryText = "default folder",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "%USERPROFILE%\\Projects",
                                Example = "C:\\Temp"
                            }
                        }
                    },
                    ["sendkeys"] = new SlotActionMetadata
                    {
                        Name = "sendkeys",
                        Label = "Send Keys",
                        Description = "Send a keystroke sequence to the active window.",
                        SuggestedLabelTemplate = "Send {keys}",
                        SuggestedIconKey = "E765",
                        SuggestedColorHex = "#32CD32",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "keys",
                                Type = "string",
                                Label = "Keys",
                                Description = "SendKeys-compatible key sequence.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Keys",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "sequence ready",
                                MissingSummaryText = "sequence missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "^v",
                                Example = "{ENTER}",
                                InputHint = "Use SendKeys syntax such as ^ for Ctrl.",
                                ValidationHint = "Use SendKeys syntax such as ^ for Ctrl and {ENTER} for Enter.",
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            },
                            new()
                            {
                                Key = "delay",
                                Type = "int",
                                Label = "Delay (ms)",
                                Description = "Delay before sending the key sequence.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Delay",
                                SummaryMode = SlotParameterSummaryMode.RawValue,
                                ConfiguredSummaryText = "custom delay",
                                MissingSummaryText = "50 ms",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 80,
                                Placeholder = "50",
                                Example = "100",
                                InputHint = "Milliseconds.",
                                ValidationHint = "Leave empty to use the default 50 ms delay.",
                                DefaultValue = 50,
                                Validators = new List<ValidationRule> { new RangeValidator(0, 10000) }
                            }
                        }
                    }
                }
            };
        }
    }
}
