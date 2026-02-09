// [Path]: Pulsar/Pulsar/Plugins/BookmarkletRunner/BookmarkletRunnerPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Native;
using Pulsar.Services.Interfaces;
using WinFormsClipboard = System.Windows.Forms.Clipboard;
using WinFormsIDataObject = System.Windows.Forms.IDataObject;
using WinFormsSendKeys = System.Windows.Forms.SendKeys;

namespace Pulsar.Plugins.BookmarkletRunner
{
    /// <summary>
    /// 书签脚本运行器插件 - 在浏览器中执行 Bookmarklet JavaScript 脚本
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

            // 8. 执行剪贴板备份和键盘自动化
            WinFormsIDataObject? originalClipboard = null;
            try
            {
                // 备份剪贴板
                try
                {
                    originalClipboard = WinFormsClipboard.GetDataObject();
                    Debug.WriteLine("[BookmarkletRunner] Clipboard backed up");
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[BookmarkletRunner] ⚠️ Warning: Failed to backup clipboard: {ex.Message}");
                }

                // 执行键盘自动化序列
                bool success = await ExecuteKeyboardSequenceAsync(scriptContent);
                
                if (!success)
                {
                    return PluginResult.Error("Failed to execute keyboard sequence");
                }

                Debug.WriteLine("[BookmarkletRunner] ✓ Bookmarklet executed successfully");
                return PluginResult.Ok("Bookmarklet executed");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Error during execution: {ex.Message}");
                return PluginResult.Error($"Execution failed: {ex.Message}");
            }
            finally
            {
                // 9. 恢复剪贴板（无痕模式）
                await Task.Delay(200); // 等待浏览器接收粘贴内容
                
                if (originalClipboard != null)
                {
                    try
                    {
                        WinFormsClipboard.SetDataObject(originalClipboard, true);
                        Debug.WriteLine("[BookmarkletRunner] Clipboard restored");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[BookmarkletRunner] ⚠️ Warning: Failed to restore clipboard: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 执行键盘自动化序列
        /// </summary>
        private async Task<bool> ExecuteKeyboardSequenceAsync(string scriptContent)
        {
            try
            {
                // 步骤 1: 聚焦地址栏 (Ctrl + L)
                WinFormsSendKeys.SendWait("^l");
                await Task.Delay(150);
                Debug.WriteLine("[BookmarkletRunner] Address bar focused (Ctrl+L)");

                // [Fix] 步骤 2: 粘贴 'j' (绕过浏览器安全限制，同时避免 IME 问题)
                // 替换原有的输入 'j'
                WinFormsClipboard.SetText("j");
                await Task.Delay(50);
                WinFormsSendKeys.SendWait("^v");
                await Task.Delay(50);
                Debug.WriteLine("[BookmarkletRunner] Pasted 'j'");

                // 步骤 3: 构造并粘贴 "avascript:[脚本内容]"
                string pasteContent = "avascript:" + scriptContent;
                WinFormsClipboard.SetText(pasteContent);
                await Task.Delay(50);
                Debug.WriteLine($"[BookmarkletRunner] Clipboard set ({pasteContent.Length} chars)");

                // 步骤 4: 粘贴 (Ctrl + V)
                WinFormsSendKeys.SendWait("^v");
                await Task.Delay(100);
                Debug.WriteLine("[BookmarkletRunner] Content pasted (Ctrl+V)");

                // 步骤 5: 执行 (Enter)
                WinFormsSendKeys.SendWait("{ENTER}");
                Debug.WriteLine("[BookmarkletRunner] Executed (Enter)");

                return true;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[BookmarkletRunner] ❌ Keyboard sequence error: {ex.Message}");
                return false;
            }
        }
    }
}
