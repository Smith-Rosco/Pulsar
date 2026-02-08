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
            // 1. 获取并验证脚本路径
            if (!args.TryGetValue("scriptPath", out var scriptPath) || string.IsNullOrEmpty(scriptPath))
            {
                return PluginResult.Error("Missing required parameter: scriptPath");
            }

            // 支持环境变量展开 (如 %USERPROFILE%)
            scriptPath = Environment.ExpandEnvironmentVariables(scriptPath);

            if (!File.Exists(scriptPath))
            {
                return PluginResult.Error($"Script file not found: {scriptPath}");
            }

            Debug.WriteLine($"[VbaRunnerPlugin] Script: {scriptPath}");

            // 2. 解析脚本指令 (如需 UI 交互)
            string directive = ScriptDirectiveParser.ParseDirective(scriptPath);
            Debug.WriteLine($"[VbaRunnerPlugin] Directive: {directive}");

            // 3. 隐藏 Pulsar 主窗口
            _windowService?.HideMainWindow();

            // 4. 尝试恢复目标窗口焦点 (如果已知)
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                WindowHelper.SetForegroundWindow(context.TargetWindowHandle);
                await Task.Delay(100); // 等待窗口切换
            }

            // 注意：COM 操作和 UI 必须在 STA 线程执行
            // ExecuteAsync 通常在线程池线程运行，因此我们需要调度到 UI 线程 (STA)
            // 或者如果不涉及 UI，至少要保证 COM 的单线程公寓模型
            
            // 为了简化 COM 和 WPF UI 的交互，我们将主要逻辑放在 Dispatcher 中执行
            // 这确保了 STA 环境，同时也允许弹出 WPF 窗口
            
            string? errorMessage = null;
            string? successMessage = null;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                try
                {
                    // 5. 连接 Excel/WPS
                    if (!_scriptEngine!.Connect())
                    {
                        errorMessage = "No active Excel/WPS instance found. Please open a workbook first.";
                        return;
                    }

                    object? scriptArg = null;

                    // 6. 处理 UI 交互 (如果在 STA 线程，可以直接显示窗口)
                    if (directive == "ShowSheetSelector")
                    {
                        var sheets = _scriptEngine.GetVisibleSheetNames();
                        if (sheets.Count == 0)
                        {
                            errorMessage = "No visible sheets found in the active workbook.";
                            return;
                        }

                        // 显示选择器窗口
                        // 智能默认值：如果只有一个表，直接选中；或者尝试选中非活动表
                        var selector = new SelectorWindow(sheets);
                        
                        // 显示模态对话框
                        bool? result = selector.ShowDialog();

                        if (result != true)
                        {
                            errorMessage = "Operation cancelled by user.";
                            return;
                        }

                        scriptArg = selector.SelectedSheet;
                    }

                    // 7. 执行脚本
                    // 确保 Excel 在前台 (ScriptEngine 内部也会调用，但这里再次确认)
                    _scriptEngine.BringExcelToFront();
                    
                    _scriptEngine.ExecuteScript(scriptPath, "Main", scriptArg);
                    successMessage = "Script executed successfully";
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[VbaRunnerPlugin] ❌ Error: {ex}");
                    errorMessage = $"Execution failed: {ex.Message}";
                }
            });

            if (errorMessage != null)
            {
                // 如果是用户取消，不算错误，但也没有成功消息
                if (errorMessage.Contains("cancelled"))
                {
                    return PluginResult.Ok(); // 或者返回特定状态
                }
                return PluginResult.Error(errorMessage);
            }

            return PluginResult.Ok(successMessage);
        }
    }
}