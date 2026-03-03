// [Path]: Pulsar/Pulsar/Plugins/Extensions/BasicCommand/SimpleCommandPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.Extensions.BasicCommand
{
    /// <summary>
    /// 简单命令插件 - 处理简单的进程启动和 SendKeys 命令
    /// </summary>
    public class SimpleCommandPlugin : IPulsarPlugin, IPluginTiered
    {
        private ILogger<SimpleCommandPlugin>? _logger;
        
        public string Id => "com.pulsar.command";
        public string DisplayName => "Simple Command";
        public string Version => "1.0.0";
        public string Author => "Pulsar Team";
        public string Description => "Execute shell commands or simulate keystrokes.";
        public string Icon => "\uE756"; // Command Prompt (TVMonitor or similar)
        public bool CanDisable => true;
        public PluginTier Tier => PluginTier.Extension;
        
        // 新增元数据属性
        public IEnumerable<string> Tags => new[] { "Command", "Utility", "General" };
        public string? DocumentationUrl => Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Docs", "Plugins", "BasicCommand.md");

        public void Initialize(IServiceProvider services)
        {
            _logger = services.GetService(typeof(ILogger<SimpleCommandPlugin>)) as ILogger<SimpleCommandPlugin>;
            _logger?.LogInformation("[SimpleCommandPlugin] Initialized successfully");
        }

        public async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            return action.ToLowerInvariant() switch
            {
                "run" => await RunCommandAsync(args, context),
                "sendkeys" => await SendKeysAsync(args, context),
                _ => PluginResult.Error($"Unknown action: {action}")
            };
        }

        /// <summary>
        /// 运行外部命令或打开文件/URL
        /// </summary>
        private async Task<PluginResult> RunCommandAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("path", out var path) || string.IsNullOrEmpty(path))
            {
                return PluginResult.Error("Missing required parameter: path");
            }

            args.TryGetValue("arguments", out var arguments);
            args.TryGetValue("workingDir", out var workingDir);

            _logger?.LogInformation("[SimpleCommandPlugin] Running: {Path}", path);

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = path,
                    Arguments = arguments ?? string.Empty,
                    UseShellExecute = true
                };

                if (!string.IsNullOrEmpty(workingDir))
                {
                    startInfo.WorkingDirectory = workingDir;
                }

                Process.Start(startInfo);
                _logger?.LogInformation("[SimpleCommandPlugin] Successfully executed: {Path}", path);
                return PluginResult.Ok($"Executed {path}");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[SimpleCommandPlugin] Execution failed: {Path}", path);
                return PluginResult.Error($"Execution failed: {ex.Message}");
            }
        }

        /// <summary>
        /// 发送键盘按键序列
        /// </summary>
        private async Task<PluginResult> SendKeysAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            if (!args.TryGetValue("keys", out var keys) || string.IsNullOrEmpty(keys))
            {
                return PluginResult.Error("Missing required parameter: keys");
            }

            // 获取延迟参数（默认 50ms）
            int delay = 50;
            if (args.TryGetValue("delay", out var delayStr))
            {
                int.TryParse(delayStr, out delay);
            }

            _logger?.LogInformation("[SimpleCommandPlugin] Sending keys: {Keys}", keys);

            try
            {
                // 等待窗口切换
                await Task.Delay(delay);
                
                SendKeys.SendWait(keys);
                
                _logger?.LogInformation("[SimpleCommandPlugin] Keys sent successfully");
                return PluginResult.Ok("Keys sent");
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[SimpleCommandPlugin] SendKeys failed");
                return PluginResult.Error($"SendKeys failed: {ex.Message}");
            }
        }
    }
}
