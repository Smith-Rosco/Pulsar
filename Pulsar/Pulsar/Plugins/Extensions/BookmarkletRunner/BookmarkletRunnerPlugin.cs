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
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.Extensions.BookmarkletRunner
{
    /// <summary>
    /// 书签脚本运行器插件 - 在浏览器中执行 Bookmarklet JavaScript 脚本
    /// Refactored to use UI Automation for instant, clipboard-free injection.
    /// </summary>
    public class BookmarkletRunnerPlugin : IPulsarPlugin, IPluginTiered, IPluginMetadataProvider, IPluginConfigurable
    {
        private IWindowService? _windowService;
        private IFocusManager? _focusManager;
        private ILogger<BookmarkletRunnerPlugin>? _logger;
        private ILocalizationService? _loc;
        private readonly BookmarkletRunnerSettings _settings = new();

        internal Func<string, string> ReadScriptFile { get; set; } = File.ReadAllText;
        internal Func<IntPtr, string, IntPtr> ResolveTargetBrowserWindow { get; set; } = BrowserHelper.GetTargetBrowserWindow;
        internal Func<IntPtr, bool> IsBrowserWindowMinimized { get; set; } = PulsarNative.IsIconic;
        internal Func<IntPtr, int, bool> RestoreBrowserWindow { get; set; } = PulsarNative.ShowWindow;
        internal Action<ushort[]> SendKeyCombination { get; set; } = InputHelper.SendKeyCombination;
        internal Func<string, bool> TrySetFocusedElementText { get; set; } = UiaHelper.TrySetFocusedElementText;
        internal Action<int> Sleep { get; set; } = Thread.Sleep;
        internal Func<int, Task> DelayAsync { get; set; } = Task.Delay;

        public string Id => "com.pulsar.bookmarklet";
        public string DisplayName => "Bookmarklet Runner";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Execute JavaScript bookmarklets in the active browser.";
        public string Icon => "\uE896"; // Code/Script Icon
        public bool CanDisable => true; // Extension plugin, can be disabled
        public PluginTier Tier => PluginTier.Extension;
        
        // 新增元数据属性
        public IEnumerable<string> Tags => new[] { "Browser", "JavaScript", "Automation" };
        public IEnumerable<string> Dependencies => new[] { "com.pulsar.winswitcher" };
        public string? DocumentationUrl => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "Plugins", "BookmarkletRunner.md");

        public void Initialize(IServiceProvider services)
        {
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
            _focusManager = services.GetService(typeof(IFocusManager)) as IFocusManager;
            _logger = services.GetService(typeof(ILogger<BookmarkletRunnerPlugin>)) as ILogger<BookmarkletRunnerPlugin>;
            _loc = services.GetService(typeof(ILocalizationService)) as ILocalizationService;

            if (_windowService == null)
            {
                throw new InvalidOperationException("IWindowService is not available");
            }

            _logger?.LogInformation("[BookmarkletRunner] Initialized successfully");
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
                    Category = "Browser",
                    Version = Version,
                    Author = Author,
                    DocumentationUrl = DocumentationUrl,
                    License = "MIT",
                    IsPrimary = true
                },
                Schema = null,
                UI = new UIHints
                {
                    Badge = "JS Script",
                    AccentColor = "#FF6B6B",
                    ShowInQuickAccess = true,
                    SortOrder = 30,
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
                        Label = "Run Bookmarklet",
                        Description = "Load and execute a bookmarklet script file in the active browser.",
                        SuggestedLabelTemplate = "Run {path}",
                        SuggestedIconKey = "E896",
                        SuggestedColorHex = "#FF6B6B",
                        Parameters = new List<SlotParameterMetadata>
                        {
                            new()
                            {
                                Key = "scriptPath",
                                Type = "string",
                                Label = "Script File",
                                Description = "Path to the JavaScript file that contains the bookmarklet.",
                                IsRequired = true,
                                Group = SlotParameterGroup.Required,
                                SummaryLabel = "Script",
                                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                                ConfiguredSummaryText = "file ready",
                                MissingSummaryText = "file missing",
                                PresentationHint = SlotParameterPresentationHint.QuickEdit,
                                QuickEditPriority = 100,
                                Placeholder = "%APPDATA%\\Pulsar\\Scripts\\example.js",
                                Example = "%APPDATA%\\Pulsar\\Scripts\\bookmarklet.js",
                                InputHint = "Choose a .js or supported text file.",
                                ValidationHint = "Choose a local .js or text file that contains the bookmarklet.",
                                PickerIntent = SlotPickerIntent.File,
                                Validators = new List<ValidationRule> { new RequiredValidator() }
                            }
                        }
                    }
                }
            };
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            if (_windowService == null)
            {
                return PluginResult.Error("Plugin not initialized");
            }

            return action.ToLowerInvariant() switch
            {
                "run" => await RunBookmarkletAsync(args, context),
                _ => PluginResult.Error($"Unknown action: {action}")
            };
        }

        /// <summary>
        /// 运行书签脚本
        /// </summary>
        private async Task<PluginResult> RunBookmarkletAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 1. 验证并获取脚本路径
            if (!args.TryGetValue("scriptPath", out var scriptPath) || string.IsNullOrEmpty(scriptPath))
            {
                return PluginResult.Error(_loc?["Bookmarklet.Error.MissingScriptPath"] ?? "Missing required parameter: scriptPath. Please check plugin configuration.");
            }

            _logger?.LogDebug("[BookmarkletRunner] Script path: {ScriptPath}", scriptPath);

            // 2. 安全性检查
            if (!ScriptPreprocessor.IsPathSafe(scriptPath))
            {
                _logger?.LogWarning("[BookmarkletRunner] Unsafe script path detected");
                return PluginResult.Error(_loc?["Bookmarklet.Error.UnsafePath"] ?? "Script path contains unsafe characters or attempts to access restricted directories.");
            }

            // 3. 读取并预处理脚本（使用新的验证系统）
            ScriptPreprocessor.ValidationResult validationResult;
            try
            {
                string rawContent = ReadScriptFile(scriptPath);
                validationResult = ScriptPreprocessor.ProcessScriptContent(rawContent, _logger);

                if (!validationResult.IsValid)
                {
                    _logger?.LogError("[BookmarkletRunner] Script validation failed");
                    
                    // Build detailed error message
                    var errorMsg = new System.Text.StringBuilder();
                    errorMsg.AppendLine(_loc?["Bookmarklet.Error.ValidationFailed"] ?? "Script validation failed:");
                    foreach (var error in validationResult.Errors)
                    {
                        errorMsg.AppendLine($"  • {error}");
                    }
                    
                    return PluginResult.Error(errorMsg.ToString().TrimEnd());
                }

                // Log warnings (non-fatal)
                foreach (var warning in validationResult.Warnings)
                {
                    _logger?.LogWarning("[BookmarkletRunner] {Warning}", warning);
                }

                if (string.IsNullOrEmpty(validationResult.ProcessedScript))
                {
                    _logger?.LogWarning("[BookmarkletRunner] Script is empty after preprocessing");
                    return PluginResult.Error(_loc?["Bookmarklet.Error.EmptyScript"] ?? "Script content is empty. Please check if the file is correct.");
                }

                _logger?.LogDebug("[BookmarkletRunner] Script validated successfully ({Length} chars)", 
                    validationResult.ProcessedScript.Length);
            }
            catch (FileNotFoundException ex)
            {
                _logger?.LogWarning(ex, "[BookmarkletRunner] File not found");
                return PluginResult.Error(string.Format(_loc?["Bookmarklet.Error.FileNotFound"] ?? "Script file not found: {0}. Please confirm the file exists.", scriptPath));
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkletRunner] Error reading script");
                return PluginResult.Error(string.Format(_loc?["Bookmarklet.Error.ReadFailed"] ?? "Failed to read script: {0}", ex.Message));
            }

            string scriptContent = validationResult.ProcessedScript;

            // 4. 智能选择目标浏览器窗口
            IntPtr browserHandle = ResolveTargetBrowserWindow(
                context.TargetWindowHandle,
                context.TargetProcessName
            );

            if (browserHandle == IntPtr.Zero)
            {
                _logger?.LogWarning("[BookmarkletRunner] No browser window found");
                return PluginResult.Error(_loc?["Bookmarklet.Error.NoBrowser"] ?? "No running browser detected. Please open a browser window first.");
            }

            // 5. 隐藏 Pulsar 主窗口
            _windowService?.HideMainWindow();
            _logger?.LogDebug("[BookmarkletRunner] Pulsar window hidden");

            // 6. 聚焦浏览器窗口
            try
            {
                // Only restore if minimized to preserve maximized state
                if (IsBrowserWindowMinimized(browserHandle))
                {
                    RestoreBrowserWindow(browserHandle, PulsarNative.SW_RESTORE);
                }
                
                if (_focusManager != null)
                {
                    await _focusManager.ActivateWindowAsync(browserHandle);
                }
                _logger?.LogDebug("[BookmarkletRunner] Browser window focused");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[BookmarkletRunner] Error focusing browser");
                return PluginResult.Error($"Failed to focus browser: {ex.Message}");
            }

            // 7. 等待窗口切换动画完成
            await DelayAsync(200);

            // 8. 执行智能输入模式 (Smart Input Mode via UIA)
            try
            {
                PluginResult executionResult = ExecuteSmartInput(scriptContent);

                if (!executionResult.Success)
                {
                    return executionResult;
                }

                _logger?.LogInformation("[BookmarkletRunner] Bookmarklet executed successfully");
                return PluginResult.Ok(_loc?["Bookmarklet.Success.Executed"] ?? "Script executed successfully");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkletRunner] Error during execution");
                return PluginResult.Error(string.Format(_loc?["Bookmarklet.Error.ExecutionFailed"] ?? "Execution error: {0}", ex.Message));
            }
        }

        /// <summary>
        /// Executes the bookmarklet using UI Automation for instant injection.
        /// Fails fast if the browser address bar is not ready for UIA injection.
        /// </summary>
        internal PluginResult ExecuteSmartInput(string scriptContent)
        {
            try
            {
                // Ensure prefix
                string fullScript = scriptContent.Trim();
                if (!fullScript.StartsWith("javascript:", StringComparison.OrdinalIgnoreCase))
                {
                    fullScript = "javascript:" + fullScript;
                }

                // --- Step 1: Focus Address Bar ---
                _logger?.LogDebug("[BookmarkletRunner] Sending Ctrl+L...");
                SendKeyCombination(new[] { InputHelper.VK_CONTROL, InputHelper.VK_L });
                
                // Wait for address bar to gain focus and select all text
                Sleep(200);

                // --- Step 2: Try UI Automation Injection ---
                _logger?.LogDebug("[BookmarkletRunner] Attempting UIA injection...");
                bool uiaSuccess = TrySetFocusedElementText(fullScript);

                if (uiaSuccess)
                {
                    _logger?.LogDebug("[BookmarkletRunner] UIA injection success. Executing...");
                    
                    // Small delay to ensure browser UI updates
                    Sleep(50);
                    
                    // Send Enter to execute
                    SendKeyCombination(new[] { InputHelper.VK_RETURN });
                    return PluginResult.Ok();
                }

                _logger?.LogWarning("[BookmarkletRunner] UIA injection failed; aborting bookmarklet execution.");
                return PluginResult.Error(_loc?["Bookmarklet.Error.AddressBarNotReady"] ?? "Browser address bar temporarily not ready to accept bookmarklet script. Please wait for the page or browser to finish loading and try again.");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkletRunner] Smart sequence error");
                return PluginResult.Error(string.Format(_loc?["Bookmarklet.Error.InjectionFailed"] ?? "Error executing bookmarklet script: {0}", ex.Message));
            }
        }

        public IEnumerable<PluginSettingDefinition> GetSettingsDefinition()
        {
            return new List<PluginSettingDefinition>
            {
                new PluginSettingDefinition
                {
                    Key = "inputMethod",
                    Label = "Input Method",
                    Type = PluginSettingType.Selection,
                    DefaultValue = "UIA",
                    Description = "Method to use for input injection",
                    Options = new List<string> { "UIA", "Clipboard", "Fallback" }
                }
            };
        }

        public void UpdateSettings(Dictionary<string, object> settings)
        {
            if (settings.TryGetValue("inputMethod", out var inputMethod))
            {
                _settings.InputMethod = inputMethod?.ToString() ?? "UIA";
            }
        }
    }
}
