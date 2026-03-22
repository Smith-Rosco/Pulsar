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

namespace Pulsar.Plugins.Extensions.BookmarkletRunner
{
    /// <summary>
    /// 书签脚本运行器插件 - 在浏览器中执行 Bookmarklet JavaScript 脚本
    /// Refactored to use UI Automation for instant, clipboard-free injection.
    /// </summary>
    public class BookmarkletRunnerPlugin : IPulsarPlugin, IPluginTiered, IPluginMetadataProvider
    {
        private IWindowService? _windowService;
        private ILogger<BookmarkletRunnerPlugin>? _logger;

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
            _logger = services.GetService(typeof(ILogger<BookmarkletRunnerPlugin>)) as ILogger<BookmarkletRunnerPlugin>;

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
                    License = "MIT"
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
                                ValidationHint = "Required and must point to a readable local file.",
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
            PulsarContext context)
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
                return PluginResult.Error("缺少必要参数: scriptPath。请检查插件配置。");
            }

            _logger?.LogDebug("[BookmarkletRunner] Script path: {ScriptPath}", scriptPath);

            // 2. 安全性检查
            if (!ScriptPreprocessor.IsPathSafe(scriptPath))
            {
                _logger?.LogWarning("[BookmarkletRunner] Unsafe script path detected");
                return PluginResult.Error("脚本路径包含不安全字符或试图访问受限目录。");
            }

            // 3. 读取并预处理脚本（使用新的验证系统）
            ScriptPreprocessor.ValidationResult validationResult;
            try
            {
                string rawContent = File.ReadAllText(scriptPath);
                validationResult = ScriptPreprocessor.ProcessScriptContent(rawContent, _logger);

                if (!validationResult.IsValid)
                {
                    _logger?.LogError("[BookmarkletRunner] Script validation failed");
                    
                    // Build detailed error message
                    var errorMsg = new System.Text.StringBuilder();
                    errorMsg.AppendLine("脚本验证失败:");
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
                    return PluginResult.Error("脚本内容为空。请检查文件是否正确。");
                }

                _logger?.LogDebug("[BookmarkletRunner] Script validated successfully ({Length} chars)", 
                    validationResult.ProcessedScript.Length);
            }
            catch (FileNotFoundException ex)
            {
                _logger?.LogWarning(ex, "[BookmarkletRunner] File not found");
                return PluginResult.Error($"找不到脚本文件: {scriptPath}。请确认文件是否存在。");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkletRunner] Error reading script");
                return PluginResult.Error($"读取脚本失败: {ex.Message}");
            }

            string scriptContent = validationResult.ProcessedScript;

            // 4. 智能选择目标浏览器窗口
            IntPtr browserHandle = BrowserHelper.GetTargetBrowserWindow(
                context.TargetWindowHandle,
                context.TargetProcessName
            );

            if (browserHandle == IntPtr.Zero)
            {
                _logger?.LogWarning("[BookmarkletRunner] No browser window found");
                return PluginResult.Error("未检测到运行中的浏览器。请先打开浏览器窗口。");
            }

            // 5. 隐藏 Pulsar 主窗口
            _windowService?.HideMainWindow();
            _logger?.LogDebug("[BookmarkletRunner] Pulsar window hidden");

            // 6. 聚焦浏览器窗口
            try
            {
                // Only restore if minimized to preserve maximized state
                if (PulsarNative.IsIconic(browserHandle))
                {
                    PulsarNative.ShowWindow(browserHandle, PulsarNative.SW_RESTORE);
                }
                
                PulsarNative.SetForegroundWindow(browserHandle);
                _logger?.LogDebug("[BookmarkletRunner] Browser window focused");
            }
            catch (Exception ex)
            {
                _logger?.LogWarning(ex, "[BookmarkletRunner] Error focusing browser");
                return PluginResult.Error($"Failed to focus browser: {ex.Message}");
            }

            // 7. 等待窗口切换动画完成
            await Task.Delay(200);

            // 8. 执行智能输入模式 (Smart Input Mode via UIA)
            try
            {
                bool success = ExecuteSmartInput(scriptContent);
                
                if (!success)
                {
                    return PluginResult.Error("脚本输入失败。请重试。");
                }

                _logger?.LogInformation("[BookmarkletRunner] Bookmarklet executed successfully");
                return PluginResult.Ok("脚本已执行");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkletRunner] Error during execution");
                return PluginResult.Error($"执行出错: {ex.Message}");
            }
        }

        /// <summary>
        /// Executes the bookmarklet using UI Automation for instant injection.
        /// Falls back to simulated typing if UIA fails.
        /// No clipboard pollution!
        /// </summary>
        private bool ExecuteSmartInput(string scriptContent)
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
                InputHelper.SendKeyCombination(InputHelper.VK_CONTROL, InputHelper.VK_L);
                
                // Wait for address bar to gain focus and select all text
                Thread.Sleep(200);

                // --- Step 2: Try UI Automation Injection ---
                _logger?.LogDebug("[BookmarkletRunner] Attempting UIA injection...");
                bool uiaSuccess = UiaHelper.TrySetFocusedElementText(fullScript);

                if (uiaSuccess)
                {
                    _logger?.LogDebug("[BookmarkletRunner] UIA injection success. Executing...");
                    
                    // Small delay to ensure browser UI updates
                    Thread.Sleep(50);
                    
                    // Send Enter to execute
                    InputHelper.SendKeyCombination(InputHelper.VK_RETURN);
                    return true;
                }

                // --- Step 3: Fallback (Simulated Typing) ---
                _logger?.LogWarning("[BookmarkletRunner] UIA failed. Fallback to simulated typing.");
                
                // Note: SendText uses SendInput which is fast, but browser might render it slowly.
                // But it's reliable and doesn't touch clipboard.
                InputHelper.SendText(fullScript);
                Thread.Sleep(50);
                InputHelper.SendKeyCombination(InputHelper.VK_RETURN);
                
                return true;
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[BookmarkletRunner] Smart sequence error");
                return false;
            }
        }
    }
}
