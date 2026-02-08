// [Path]: Pulsar/Pulsar/Plugins/BasicCommand/SimpleCommandPlugin.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Windows.Forms;
using Pulsar.Core.Plugin;

namespace Pulsar.Plugins.BasicCommand
{
    /// <summary>
    /// 简单命令插件 - 处理简单的进程启动和 SendKeys 命令
    /// </summary>
    public class SimpleCommandPlugin : IPulsarPlugin
    {
        public string Id => "com.pulsar.command";
        public string DisplayName => "Simple Command";

        public void Initialize(IServiceProvider services)
        {
            Debug.WriteLine("[SimpleCommandPlugin] Initialized successfully");
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

            Debug.WriteLine($"[SimpleCommandPlugin] Running: {path}");

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
                Debug.WriteLine($"[SimpleCommandPlugin] ✓ Successfully executed: {path}");
                return PluginResult.Ok($"Executed {path}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleCommandPlugin] ❌ Execution failed: {ex.Message}");
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

            Debug.WriteLine($"[SimpleCommandPlugin] Sending keys: {keys}");

            try
            {
                // 等待窗口切换
                await Task.Delay(delay);
                
                SendKeys.SendWait(keys);
                
                Debug.WriteLine($"[SimpleCommandPlugin] ✓ Keys sent successfully");
                return PluginResult.Ok("Keys sent");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[SimpleCommandPlugin] ❌ SendKeys failed: {ex.Message}");
                return PluginResult.Error($"SendKeys failed: {ex.Message}");
            }
        }
    }
}
