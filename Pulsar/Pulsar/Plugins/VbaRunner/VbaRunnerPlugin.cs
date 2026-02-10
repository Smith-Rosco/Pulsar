using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Pulsar.Core.Plugin;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.VbaRunner
{
    /// <summary>
    /// VBA Runner Plugin - Executes VBA scripts in Excel/WPS with interactive support
    /// </summary>
    public class VbaRunnerPlugin : IPulsarPlugin
    {
        private IWindowService? _windowService;
        private ScriptEngine? _scriptEngine;

        public string Id => "com.pulsar.vbarunner";
        public string DisplayName => "VBA Script Runner";

        public void Initialize(IServiceProvider services)
        {
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
            _scriptEngine = new ScriptEngine();

            if (_windowService == null)
            {
                Debug.WriteLine("[VbaRunnerPlugin] ⚠️ IWindowService not available");
            }
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (_scriptEngine == null)
            {
                return PluginResult.Error("Plugin initialization failed");
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
            Debug.WriteLine("[VbaRunnerPlugin] === RunScriptAsync started ===");
            Debug.WriteLine($"[VbaRunnerPlugin] Context - TargetWindowHandle: {context.TargetWindowHandle}");
            Debug.WriteLine($"[VbaRunnerPlugin] Context - TargetProcessName: {context.TargetProcessName}");
            Debug.WriteLine($"[VbaRunnerPlugin] Context - TargetProcessId: {context.TargetProcessId}");

            // 1. 获取并验证脚本路径
            if (!args.TryGetValue("scriptPath", out var scriptPath) || string.IsNullOrEmpty(scriptPath))
            {
                Debug.WriteLine("[VbaRunnerPlugin] ❌ Missing scriptPath parameter");
                return PluginResult.Error("Missing required parameter: scriptPath");
            }

            // 支持环境变量展开 (如 %USERPROFILE%)
            scriptPath = Environment.ExpandEnvironmentVariables(scriptPath);

            if (!File.Exists(scriptPath))
            {
                Debug.WriteLine($"[VbaRunnerPlugin] ❌ Script file not found: {scriptPath}");
                return PluginResult.Error($"Script file not found: {scriptPath}");
            }

            Debug.WriteLine($"[VbaRunnerPlugin] Script: {scriptPath}");

            // 2. 解析脚本指令 (如需 UI 交互)
            string directive = ScriptDirectiveParser.ParseDirective(scriptPath);
            Debug.WriteLine($"[VbaRunnerPlugin] Directive: {directive}");

            // 3. 隐藏 Pulsar 主窗口
            Debug.WriteLine("[VbaRunnerPlugin] Hiding main window...");
            _windowService?.HideMainWindow();

            // 4. 尝试恢复目标窗口焦点 (如果已知)
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                Debug.WriteLine($"[VbaRunnerPlugin] Setting foreground window to: {context.TargetWindowHandle}");
                bool success = WindowHelper.SetForegroundWindow(context.TargetWindowHandle);
                Debug.WriteLine($"[VbaRunnerPlugin] SetForegroundWindow result: {success}");
                await Task.Delay(100); // 等待窗口切换
            }
            else
            {
                Debug.WriteLine("[VbaRunnerPlugin] ⚠️ No target window handle in context");
            }

            // 注意：COM 操作和 UI 必须在 STA 线程执行
            // ExecuteAsync 通常在线程池线程运行，因此我们需要调度到 UI 线程 (STA)
            
            string? errorMessage = null;
            string? successMessage = null;

            Debug.WriteLine("[VbaRunnerPlugin] Dispatching to UI thread...");

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                Debug.WriteLine("[VbaRunnerPlugin] Inside UI thread dispatcher");
                try
                {
                    // 5. 连接 Excel/WPS
                    Debug.WriteLine("[VbaRunnerPlugin] Calling ScriptEngine.Connect()...");
                    bool connected = _scriptEngine!.Connect(context.TargetProcessId);
                    
                    if (!connected)
                    {
                        errorMessage = "No active Excel/WPS instance found. Please open a workbook first.";
                        return;
                    }

                    object? scriptArg = null;

                    // 6. 处理 UI 交互
                    if (directive == "ShowSheetSelector")
                    {
                        var sheets = _scriptEngine.GetVisibleSheetNames();
                        if (sheets.Count == 0)
                        {
                            errorMessage = "No visible sheets found.";
                            return;
                        }

                        var selector = new SelectorWindow(sheets);
                        if (selector.ShowDialog() != true)
                        {
                            errorMessage = "Operation cancelled by user.";
                            return;
                        }
                        scriptArg = selector.SelectedSheet;
                    }

                    // 7. 执行脚本
                    string macroName = args.TryGetValue("macro", out var m) && !string.IsNullOrWhiteSpace(m) ? m : "Main";
                    _scriptEngine.ExecuteScript(scriptPath, macroName, scriptArg);
                    successMessage = "Script executed successfully";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VbaRunnerPlugin] ❌ Error: {ex}");
                    errorMessage = $"Execution failed: {ex.Message}";
                }
            });

            // [Fix] 8. 归还焦点给原始窗口 (Professional Architect Recommendation)
            // 无论脚本执行成功与否，都尝试将焦点还给用户之前工作的窗口
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                Debug.WriteLine($"[VbaRunnerPlugin] Restoring focus to original window: {context.TargetWindowHandle}");
                WindowHelper.SetForegroundWindow(context.TargetWindowHandle);
            }

            Debug.WriteLine($"[VbaRunnerPlugin] Dispatcher completed - Result: {errorMessage ?? successMessage}");

            if (errorMessage != null)
            {
                if (errorMessage.Contains("cancelled")) return PluginResult.Ok();
                return PluginResult.Error(errorMessage);
            }

            return PluginResult.Ok(successMessage);
        }
    }
}