// [Path]: Pulsar/Pulsar/Plugins/Core/Pki/PkiPlugin.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Plugins.Core.Pki.Contracts;
using Pulsar.Plugins.Core.Pki.Models;
using Pulsar.Plugins.Core.Pki.Models.Execution;

namespace Pulsar.Plugins.Core.Pki
{
    /// <summary>
    /// PKI plugin adapter for credential injection.
    /// </summary>
    public class PkiPlugin : PluginBase<PkiPlugin>, IPluginMetadataProvider, IPluginConfigurable
    {
        private readonly IPkiExecutionService _executionService;
        private readonly PkiPluginSettings _settings = new();
        private readonly IReadOnlyDictionary<string, PluginActionHandler> _actionHandlers;

        private static readonly IReadOnlyDictionary<string, string> ActionAliases =
            new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            {
                ["inject"] = "fill"
            };

        public PkiPlugin(
            ILogger<PkiPlugin> logger,
            ILocalizationService loc,
            IPkiExecutionService executionService)
            : base(logger, loc)
        {
            _executionService = executionService;

            _actionHandlers = new Dictionary<string, PluginActionHandler>(StringComparer.OrdinalIgnoreCase)
            {
                ["fill"] = FillCredentialsAsync
            };
        }

        public override string Id => "com.pulsar.pki";
        public override string DisplayName => "AutoFill";
        public override string Version => "1.0.0";
        public override string Author => "Pulsar Team";
        public override string Description => "Fill a saved password into the active application.";
        public override string Icon => "\uE72E";
        public override bool CanDisable => false;
        public override PluginTier Tier => PluginTier.Core;

        public override IEnumerable<string> Tags => new[] { "Security", "Credentials", "Secrets" };

        public override string? DocumentationUrl => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory,
            "Docs",
            "Plugins",
            "PkiPlugin.md");

        public override Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            var enrichedArgs = new Dictionary<string, string>(args);

            if (!enrichedArgs.ContainsKey("autoSubmit") && _settings.AutoSubmit)
            {
                enrichedArgs["autoSubmit"] = "true";
            }

            if (!enrichedArgs.ContainsKey("injectionDelay") && _settings.InjectionDelay > 0)
            {
                enrichedArgs["injectionDelay"] = _settings.InjectionDelay.ToString();
            }

            return DispatchAsync(action, enrichedArgs, context, cancellationToken, _actionHandlers, ActionAliases);
        }

        private async Task<PluginResult> FillCredentialsAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken)
        {
            PkiExecutionResult result = await _executionService.ExecuteAsync(args, context);

            if (result.Success)
            {
                return PluginResult.Ok(result.Message);
            }

            Logger.LogWarning(
                "[PkiPlugin] PKI execution failed at stage {Stage}: {Message}",
                result.Stage,
                result.Message);

            return PluginResult.Error(result.Message, PluginErrorSeverity.Recoverable, MapStageToErrorCode(result.Stage));
        }

        private static PluginErrorCode MapStageToErrorCode(PkiExecutionStage stage)
        {
            return stage switch
            {
                PkiExecutionStage.Validation => PluginErrorCode.MissingRequiredParameter,
                PkiExecutionStage.SecretLookup => PluginErrorCode.NotFound,
                PkiExecutionStage.Decryption => PluginErrorCode.InvalidConfiguration,
                _ => PluginErrorCode.ExecutionFailed
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
                    Category = "Credentials",
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
                        ["autoSubmit"] = new PropertySchema
                        {
                            Type = "bool",
                            Description = "Automatically press Enter after injecting password",
                            DefaultValue = false
                        },
                        ["injectionDelay"] = new PropertySchema
                        {
                            Type = "int",
                            Description = "Delay in milliseconds between keystrokes (0-1000)",
                            DefaultValue = 50,
                            Validators = new List<ValidationRule> { new RangeValidator(0, 1000) }
                        }
                    },
                    RequiredProperties = Array.Empty<string>()
                },
                UI = new UIHints
                {
                    Badge = "Fill",
                    AccentColor = "#4CAF50",
                    ShowInQuickAccess = true,
                    SortOrder = 10,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "fill" },
                    RequiresForegroundWindow = true,
                    Dependencies = new List<string>(),
                    CanDisable = false,
                    Tier = PluginTier.Core,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["fill"] = new SlotActionMetadata
                    {
                        Name = "fill",
                        Label = "Fill Password",
                        Description = "Fill a saved password into the active application.",
                        SuggestedLabelTemplate = "Fill Password",
                        SuggestedIconKey = "E72E",
                        SuggestedColorHex = "#4CAF50",
                        Aliases = new List<string> { "inject" },
                        ParameterAliases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
                        {
                            ["autoSubmit"] = "autoEnter"
                        },
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "secretId",
                                Type = "guid",
                                Label = "Password",
                                Description = "Saved password to inject.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Password",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "selected",
                                MissingSummaryText = "not selected",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                QuickEditPriority = 100,
                                Placeholder = "Select a saved password",
                                InputHint = "Choose a password from the saved credentials.",
                                ValidationHint = "Choose a saved password from the saved credentials.",
                                PickerIntent = SlotPickerIntent.Secret,
                                IsSensitive = true,
                                Validators = new List<ValidationRule>
                                {
                                    new RequiredValidator(),
                                    new RegexValidator("^[0-9a-fA-F-]{36}$", "Secret must be a valid GUID.")
                                }
                            },
                            new()
                            {
                                Key = "autoEnter",
                                Type = "bool",
                                Label = "Press Enter After Fill",
                                Description = "Press Enter after injecting the secret.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Optional,
                                SummaryLabel = "Submit",
                                SummaryMode = SlotParameterSummaryMode.RawValue,
                                ConfiguredSummaryText = "enter on",
                                MissingSummaryText = "enter off",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 90,
                                Example = "true",
                                InputHint = "Use true or false.",
                                ValidationHint = "Turn this on if the target form should submit right after filling.",
                                DefaultValue = false,
                                Aliases = new List<string> { "autoSubmit" }
                            }
                        }
                    }
                }
            };
        }

        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            return SchemaToSettingAdapter.Convert(GetMetadata().Schema);
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("autoSubmit", out var autoSubmit))
            {
                _settings.AutoSubmit = autoSubmit is bool b ? b : Convert.ToBoolean(autoSubmit);
            }

            if (settings.TryGetValue("injectionDelay", out var injectionDelay))
            {
                _settings.InjectionDelay = injectionDelay is int i ? i : Convert.ToInt32(injectionDelay);
            }

        }
    }
}
