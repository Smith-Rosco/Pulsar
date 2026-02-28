using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Plugins.VbaRunner
{
    /// <summary>
    /// VBA Runner Plugin - Executes VBA scripts in Excel/WPS with interactive support
    /// </summary>
    public class VbaRunnerPlugin : IPulsarPlugin, IPluginTiered
    {
        private IWindowService? _windowService;
        private ScriptEngine? _scriptEngine;
        private ILogger<VbaRunnerPlugin>? _logger;

        public string Id => "com.pulsar.vbarunner";
        public string DisplayName => "VBA Script Runner";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Execute VBA scripts in Excel/WPS with context awareness.";
        public string Icon => "\uE71D"; // Excel/Table Icon
        public bool CanDisable => true; // Extension plugin, can be disabled
        public PluginTier Tier => PluginTier.Extension;

        public void Initialize(IServiceProvider services)
        {
            _windowService = services.GetService(typeof(IWindowService)) as IWindowService;
            _scriptEngine = new ScriptEngine();
            _logger = services.GetService(typeof(ILogger<VbaRunnerPlugin>)) as ILogger<VbaRunnerPlugin>;

            if (_windowService == null)
            {
                _logger?.LogWarning("[VbaRunnerPlugin] IWindowService not available");
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

            // 2. 读取脚本内容 & 解析指令 (减少 I/O)
            string scriptContent;
            try
            {
                scriptContent = await File.ReadAllTextAsync(scriptPath);
            }
            catch (Exception ex)
            {
                return PluginResult.Error($"Failed to read script: {ex.Message}");
            }

            string directive = ScriptDirectiveParser.ParseDirectiveFromContent(scriptContent);
            _logger?.LogDebug("[VbaRunnerPlugin] Directive: {Directive}", directive);

            // 3. 隐藏 Pulsar 主窗口
            _logger?.LogDebug("[VbaRunnerPlugin] Hiding main window...");
            _windowService?.HideMainWindow();

            // 4. 尝试恢复目标窗口焦点 (如果已知)
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                _logger?.LogDebug("[VbaRunnerPlugin] Setting foreground window to: {Hwnd}", context.TargetWindowHandle);
                bool success = WindowHelper.SetForegroundWindow(context.TargetWindowHandle);
                _logger?.LogDebug("[VbaRunnerPlugin] SetForegroundWindow result: {Success}", success);
                await Task.Delay(100); // 等待窗口切换
            }
            else
            {
                _logger?.LogDebug("[VbaRunnerPlugin] No target window handle in context");
            }

            // 注意：COM 操作和 UI 必须在 STA 线程执行
            // ExecuteAsync 通常在线程池线程运行，因此我们需要调度到 UI 线程 (STA)
            
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

                    // 7. 执行脚本 (传入已读取的内容)
                    string macroName = args.TryGetValue("macro", out var m) && !string.IsNullOrWhiteSpace(m) ? m : "Main";
                    _scriptEngine.ExecuteScriptContent(scriptContent, macroName, scriptArg);
                    successMessage = "Script executed successfully";
                }
                catch (Exception ex)
                {
                    _logger?.LogError(ex, "[VbaRunnerPlugin] Execution failed");
                    errorMessage = $"Execution failed: {ex.Message}";
                }
            });

            // [Fix] 8. 归还焦点给原始窗口 (Professional Architect Recommendation)
            // 无论脚本执行成功与否，都尝试将焦点还给用户之前工作的窗口
            if (context.TargetWindowHandle != IntPtr.Zero)
            {
                _logger?.LogDebug("[VbaRunnerPlugin] Restoring focus to original window: {Hwnd}", context.TargetWindowHandle);
                WindowHelper.SetForegroundWindow(context.TargetWindowHandle);
            }

            _logger?.LogDebug("[VbaRunnerPlugin] Dispatcher completed - Result: {Result}", errorMessage ?? successMessage);

            if (errorMessage != null)
            {
                if (errorMessage.Contains("cancelled")) return PluginResult.Ok();
                return PluginResult.Error(errorMessage);
            }

            return PluginResult.Ok(successMessage);
        }
    }
}
