using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// VBA Runner Plugin - Executes VBA scripts in Excel/WPS with interactive support
    /// </summary>
    public class VbaRunnerPlugin : IPulsarPlugin, IPluginTiered, IPluginLifecycle, IPluginMetadataProvider, IPluginConfigurable
    {
        private IWindowService? _windowService;
        private IFocusManager? _focusManager;
        private ScriptEngine? _scriptEngine;
        private ILogger<VbaRunnerPlugin>? _logger;
        private readonly VbaRunnerSettings _settings = new();

        public string Id => "com.pulsar.vbarunner";
        public string DisplayName => "VBA Script Runner";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Execute VBA scripts in Excel/WPS with context awareness.";
        public string Icon => "\uE71D"; // Excel/Table Icon
        public bool CanDisable => true; // Extension plugin, can be disabled
        public PluginTier Tier => PluginTier.Extension;
        
        // 新增元数据属性
        public IEnumerable<string> Tags => new[] { "Automation", "Excel", "Scripting" };
        public IEnumerable<string> Dependencies => new[] { "com.pulsar.winswitcher" };
        public string? DocumentationUrl => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "Plugins", "VbaRunner.md");

        public void Initialize(IServiceProvider services)
        {
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
            _focusManager = services.GetService(typeof(IFocusManager)) as IFocusManager;
            _logger = services.GetService(typeof(ILogger<VbaRunnerPlugin>)) as ILogger<VbaRunnerPlugin>;

            if (_windowService == null)
            {
                _logger?.LogWarning("[VbaRunnerPlugin] IWindowService not available");
            }
            
            _logger?.LogInformation("[VbaRunnerPlugin] Initialized successfully");
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
                    Category = "Automation",
                    Version = Version,
                    Author = Author,
                    DocumentationUrl = DocumentationUrl,
                    License = "MIT"
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "VBA Script",
                    AccentColor = "#FF8C00",
                    ShowInQuickAccess = true,
                    SortOrder = 40,
                    IsFeatured = true
                },
                Capabilities = new PluginCapabilities
                {
                    SupportedActions = new List<string> { "run" },
                    RequiresForegroundWindow = true,
                    Dependencies = new List<string> { "com.pulsar.winswitcher" },
                    CanDisable = CanDisable,
                    Tier = Tier,
                    MinPulsarVersion = "1.0.0"
                },
                Actions = new Dictionary<string, SlotActionMetadata>(StringComparer.OrdinalIgnoreCase)
                {
                    ["run"] = new SlotActionMetadata
                    {
                        Name = "run",
                        Label = "Run VBA Script",
                        Description = "Execute a VBA script file in the target spreadsheet application.",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "scriptPath",
                                Type = "string",
                                Label = "Script File",
                                Description = "Path to the VBA script file.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Script",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "file ready",
                                MissingSummaryText = "file missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "%USERPROFILE%\\Documents\\Pulsar\\Scripts\\example.txt",
                                Example = "%USERPROFILE%\\Documents\\Pulsar\\Scripts\\macro.bas",
                                InputHint = "Choose a .txt, .vbs, or .bas file.",
                                ValidationHint = "Choose a local .txt, .vbs, or .bas file.",
                                PickerIntent = SlotPickerIntent.File,
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            },
                            new()
                            {
                                Key = "macro",
                                Type = "string",
                                Label = "Macro Override",
                                Description = "Optional macro name override when the script defines multiple entry points.",
                                IsRequired = false,
                                Group = SlotParameterGroup.Advanced,
                                SummaryLabel = "Macro",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "override set",
                                MissingSummaryText = "auto detect",
                                PresentationHint = SlotParameterPresentationHint.DialogOnly,
                                Placeholder = "Main",
                                Example = "SetupWorkbook",
                                InputHint = "Leave empty to use the macro declared in the script file.",
                                ValidationHint = "Leave empty to auto-detect the macro entry point."
                            }
                        }
                    }
                }
            };
        }
        
        // IPluginLifecycle 实现
        public async Task OnEnableAsync()
        {
            _scriptEngine = new ScriptEngine(_focusManager);
            _logger?.LogInformation("[VbaRunnerPlugin] Plugin enabled, ScriptEngine created");
            await Task.CompletedTask;
        }

        public async Task OnDisableAsync()
        {
            _logger?.LogInformation("[VbaRunnerPlugin] Plugin disabled, cleaning up resources");
            _scriptEngine = null;
            await Task.CompletedTask;
        }

        public async Task OnUnloadAsync()
        {
            _logger?.LogInformation("[VbaRunnerPlugin] Plugin unloading");
            await OnDisableAsync();
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            if (_scriptEngine == null)
            {
                return PluginResult.Error("Plugin initialization failed");
            }

            if (string.IsNullOrEmpty(action))
            {
                _logger?.LogWarning("[VbaRunnerPlugin] Action parameter is missing or null");
                return PluginResult.Error("Missing action parameter. Please configure the slot with an action (e.g., 'run').");
            }

            return action.ToLowerInvariant() switch
            {
                "run" => await RunScriptAsync(args, context),
                _ => PluginResult.Error($"Unknown action: {action}")
            };
        }

        private async Task<PluginResult> RunScriptAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            _logger?.LogDebug(
                "[VbaRunnerPlugin] RunScriptAsync started. TargetHwnd={TargetHwnd}, TargetProcess={TargetProcess}, TargetPid={TargetPid}",
                context.TargetWindowHandle,
                context.TargetProcessName,
                context.TargetProcessId);

            // 1. 获取并验证脚本路径
            if (!args.TryGetValue("scriptPath", out var scriptPath) || string.IsNullOrEmpty(scriptPath))
            {
                _logger?.LogWarning("[VbaRunnerPlugin] Missing scriptPath parameter");
                return PluginResult.Error("Missing required parameter: scriptPath");
            }

            // 支持环境变量展开 (如 %USERPROFILE%)
            scriptPath = Environment.ExpandEnvironmentVariables(scriptPath);

            if (!File.Exists(scriptPath))
            {
                _logger?.LogWarning("[VbaRunnerPlugin] Script file not found: {ScriptPath}", scriptPath);
                return PluginResult.Error($"Script file not found: {scriptPath}");
            }

            _logger?.LogDebug("[VbaRunnerPlugin] Script: {ScriptPath}", scriptPath);

            // 2. 读取脚本内容 & 解析所有指令
            string scriptContent;
            try
            {
                scriptContent = await File.ReadAllTextAsync(scriptPath);
            }
            catch (Exception ex)
            {
                return PluginResult.Error($"Failed to read script: {ex.Message}");
            }

            ScriptDirectives directives = ScriptDirectiveParser.ParseAllDirectives(scriptContent);
            _logger?.LogDebug(
                "[VbaRunnerPlugin] Directives parsed - Runner={Runner}, Macro={Macro}, Requires={RequiresCount}, OnMissing={OnMissing}",
                directives.Runner, directives.Macro, directives.Requires.Count, directives.OnMissing);

            // 3. 隐藏 Pulsar 主窗口
            _logger?.LogDebug("[VbaRunnerPlugin] Hiding main window...");
            _windowService?.HideMainWindow();

            // 4. 尝试恢复目标窗口焦点 (如果已知)
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                _logger?.LogDebug("[VbaRunnerPlugin] Activating target window: {Hwnd}", context.TargetWindowHandle);
                if (_focusManager != null)
                {
                    await _focusManager.ActivateWindowAsync(context.TargetWindowHandle);
                }
                await Task.Delay(100); // 等待窗口切换
            }
            else
            {
                _logger?.LogDebug("[VbaRunnerPlugin] No target window handle in context");
            }

            // 注意：COM 操作和 UI 必须在 STA 线程执行
            string? errorMessage = null;
            string? successMessage = null;

            _logger?.LogDebug("[VbaRunnerPlugin] Dispatching to UI thread...");

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 5. 连接 Excel/WPS
                    _logger?.LogDebug("[VbaRunnerPlugin] Calling ScriptEngine.Connect()...");
                    bool connected = _scriptEngine!.Connect(context.TargetProcessId);
                    
                    if (!connected)
                    {
                        errorMessage = "No active Excel/WPS instance found. Please open a workbook first.";
                        return;
                    }

                    // **PHASE 1: Validate Prerequisites**
                    if (directives.Requires.Count > 0)
                    {
                        _logger?.LogDebug("[VbaRunnerPlugin] Validating {Count} prerequisites...", directives.Requires.Count);
                        var validationResult = _scriptEngine.ValidatePrerequisites(directives.Requires);
                        
                        if (!validationResult.IsValid)
                        {
                            _logger?.LogInformation(
                                "[VbaRunnerPlugin] Prerequisites not met: {Missing}", 
                                string.Join(", ", validationResult.MissingItems));
                            
                            // Run setup/onMissing macro
                            string setupMacro = directives.OnMissing;
                            _logger?.LogInformation("[VbaRunnerPlugin] Running setup macro: {Macro}", setupMacro);
                            
                            try
                            {
                                _scriptEngine.ExecuteScriptContent(scriptContent, setupMacro, null);
                                successMessage = "Setup completed. Please configure and run again.";
                            }
                            catch (Exception ex)
                            {
                                _logger?.LogError(ex, "[VbaRunnerPlugin] Setup macro failed");
                                errorMessage = $"Setup failed: {ex.Message}";
                            }
                            
                            return; // Exit early - don't continue to main execution
                        }
                        
                        _logger?.LogDebug("[VbaRunnerPlugin] All prerequisites validated successfully");
                    }

                    // **PHASE 2: Handle UI Interaction (if prerequisites met)**
                    object? scriptArg = null;
                    
                    if (directives.Runner == "ShowSheetSelector")
                    {
                        var sheets = _scriptEngine.GetFilteredSheetNames(directives.SheetFilter);
                        
                        if (sheets.Count == 0)
                        {
                            errorMessage = "No valid sheets found.";
                            return;
                        }
                        
                        // Auto-select if only one option and AutoSelectSingle is true
                        if (sheets.Count == 1 && directives.AutoSelectSingle)
                        {
                            scriptArg = sheets[0];
                            _logger?.LogDebug("[VbaRunnerPlugin] Auto-selected single sheet: {Sheet}", scriptArg);
                        }
                        else
                        {
                            var selector = new SelectorWindow(sheets);
                            if (selector.ShowDialog() != true)
                            {
                                errorMessage = "Operation cancelled by user.";
                                return;
                            }
                            scriptArg = selector.SelectedSheet;
                        }
                    }

                    // **PHASE 3: Execute Main Macro**
                    string macroName = args.TryGetValue("macro", out var m) && !string.IsNullOrWhiteSpace(m) 
                        ? m 
                        : directives.Macro;
                    
                    _logger?.LogDebug("[VbaRunnerPlugin] Executing macro: {Macro}", macroName);
                    _scriptEngine.ExecuteScriptContent(scriptContent, macroName, scriptArg);
                    successMessage = "Script executed successfully";
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[VbaRunnerPlugin] Execution failed");
                    errorMessage = $"Execution failed: {ex.Message}";
                }
            });

            // 8. 归还焦点给原始窗口
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                _logger?.LogDebug("[VbaRunnerPlugin] Restoring focus to original window: {Hwnd}", context.TargetWindowHandle);
                if (_focusManager != null)
                {
                    await _focusManager.ActivateWindowAsync(context.TargetWindowHandle);
                }
            }

            _logger?.LogDebug("[VbaRunnerPlugin] Dispatcher completed - Result: {Result}", errorMessage ?? successMessage);

            if (errorMessage != null)
            {
                if (errorMessage.Contains("cancelled")) return PluginResult.Ok();
                return PluginResult.Error(errorMessage);
            }

            return PluginResult.Ok(successMessage);
        }

        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            return new List<PluginSettingDefinition>
            {
                new PluginSettingDefinition
                {
                    Key = "defaultTargetApp",
                    Label = "Default Target Application",
                    Type = PluginSettingType.Selection,
                    DefaultValue = "Auto",
                    Description = "Default target application for VBA scripts",
                    Options = new List<string> { "Auto", "Excel", "WPS" }
                }
            };
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("defaultTargetApp", out var targetApp))
            {
                _settings.DefaultTargetApp = targetApp?.ToString() ?? "Auto";
            }
        }
    }
}
