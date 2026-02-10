using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.BookmarkletRunner
{
    /// <summary>
    /// 书签脚本运行器插件 - 在浏览器中执行 Bookmarklet JavaScript 脚本
    /// Refactored to use UI Automation for instant, clipboard-free injection.
    /// </summary>
    public class BookmarkletRunnerPlugin : IPulsarPlugin
    {
        private IWindowService? _windowService;

        public string Id => "com.pulsar.bookmarklet";
        public string DisplayName => "Bookmarklet Runner";

        public void Initialize(IServiceProvider services)
        {
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;

            if (_windowService == null)
            {
                throw new InvalidOperationException("IWindowService is not available");
            }

            Debug.WriteLine("[BookmarkletRunner] Initialized successfully");
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
                return PluginResult.Error("Missing required parameter: scriptPath");
            }

            Debug.WriteLine($"[BookmarkletRunner] Script path: {scriptPath}");

            // 2. 安全性检查
            if (!ScriptPreprocessor.IsPathSafe(scriptPath))
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Unsafe script path detected");
                return PluginResult.Error("Invalid or unsafe script path");
            }

            // 3. 读取并预处理脚本
            string scriptContent;
            try
            {
                scriptContent = ScriptPreprocessor.PreprocessScript(scriptPath);
                if (string.IsNullOrEmpty(scriptContent))
                {
                    Debug.WriteLine("[BookmarkletRunner] ❌ Script is empty after preprocessing");
                    return PluginResult.Error("Script file is empty");
                }
                Debug.WriteLine($"[BookmarkletRunner] Script loaded successfully ({scriptContent.Length} chars)");
            }
            catch (FileNotFoundException ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ File not found: {ex.Message}");
                return PluginResult.Error($"Script file not found: {scriptPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Error reading script: {ex.Message}");
                return PluginResult.Error($"Failed to read script: {ex.Message}");
            }

            // 4. 智能选择目标浏览器窗口
            IntPtr browserHandle = BrowserHelper.GetTargetBrowserWindow(
                context.TargetWindowHandle,
                context.TargetProcessName
            );

            if (browserHandle == IntPtr.Zero)
            {
                Debug.WriteLine("[BookmarkletRunner] ❌ No browser window found");
                return PluginResult.Error("No browser window found. Please open a browser first.");
            }

            // 5. 隐藏 Pulsar 主窗口
            _windowService?.HideMainWindow();
            Debug.WriteLine("[BookmarkletRunner] Pulsar window hidden");

            // 6. 聚焦浏览器窗口
            try
            {
                // Only restore if minimized to preserve maximized state
                if (WindowHelper.IsIconic(browserHandle))
                {
                    WindowHelper.ShowWindow(browserHandle, WindowHelper.SW_RESTORE);
                }
                
                WindowHelper.SetForegroundWindow(browserHandle);
                Debug.WriteLine("[BookmarkletRunner] Browser window focused");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Error focusing browser: {ex.Message}");
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
                    return PluginResult.Error("Failed to execute script input");
                }

                Debug.WriteLine("[BookmarkletRunner] ✓ Bookmarklet executed successfully");
                return PluginResult.Ok("Bookmarklet executed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Error during execution: {ex.Message}");
                return PluginResult.Error($"Execution failed: {ex.Message}");
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
                Debug.WriteLine("[BookmarkletRunner] Sending Ctrl+L...");
                InputHelper.SendKeyCombination(InputHelper.VK_CONTROL, InputHelper.VK_L);
                
                // Wait for address bar to gain focus and select all text
                Thread.Sleep(200);

                // --- Step 2: Try UI Automation Injection ---
                Debug.WriteLine("[BookmarkletRunner] Attempting UIA injection...");
                bool uiaSuccess = UiaHelper.TrySetFocusedElementText(fullScript);

                if (uiaSuccess)
                {
                    Debug.WriteLine("[BookmarkletRunner] UIA Injection Success! Executing...");
                    
                    // Small delay to ensure browser UI updates
                    Thread.Sleep(50);
                    
                    // Send Enter to execute
                    InputHelper.SendKeyCombination(InputHelper.VK_RETURN);
                    return true;
                }

                // --- Step 3: Fallback (Simulated Typing) ---
                Debug.WriteLine("[BookmarkletRunner] ⚠️ UIA failed. Fallback to Simulated Typing (Turbo Mode).");
                
                // Note: SendText uses SendInput which is fast, but browser might render it slowly.
                // But it's reliable and doesn't touch clipboard.
                InputHelper.SendText(fullScript);
                Thread.Sleep(50);
                InputHelper.SendKeyCombination(InputHelper.VK_RETURN);
                
                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Smart sequence error: {ex.Message}");
                return false;
            }
        }
    }
}
