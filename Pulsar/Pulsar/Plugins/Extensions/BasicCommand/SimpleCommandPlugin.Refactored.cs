// [Path]: Pulsar/Pulsar/Plugins/Extensions/BasicCommand/SimpleCommandPlugin.Refactored.cs
// 
// 这是重构后的 SimpleCommandPlugin 示例
// 展示如何使用 PluginBase<T> 和构造函数注入

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Native;

namespace Pulsar.Plugins.Extensions.BasicCommand
{
    /// <summary>
    /// 简单命令插件 (重构版) - 展示最佳实践
    /// 
    /// 重构改进:
    /// 1. 继承 PluginBase<T> - 消除样板代码
    /// 2. 构造函数注入 - 替代 Service Locator
    /// 3. 使用辅助方法 - TryGetRequiredArg, UnknownActionError
    /// 4. 更清晰的错误处理
    /// </summary>
    public class SimpleCommandPluginRefactored : PluginBase<SimpleCommandPluginRefactored>
    {
        // ===== 构造函数注入 (替代 Service Locator) =====
        public SimpleCommandPluginRefactored(ILogger<SimpleCommandPluginRefactored> logger) 
            : base(logger)
        {
            // Logger 已由基类自动注入
            // 无需手动从 IServiceProvider 获取
        }

        // ===== 插件元数据 =====
        public override string Id => "com.pulsar.command.refactored";
        public override string DisplayName => "Simple Command (Refactored)";
        public override string Version => "2.0.0";
        public override string Author => "Pulsar Team";
        public override string Description => "Execute shell commands or simulate keystrokes (refactored with best practices).";
        public override string Icon => "\uE756"; // Command Prompt
        public override bool CanDisable => true;
        public override PluginTier Tier => PluginTier.Extension;
        
        public override IEnumerable<string> Tags => new[] { "Command", "Utility", "General" };
        public override string? DocumentationUrl => Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, 
            "Docs", "Plugins", "BasicCommand.md"
        );

        // ===== 初始化钩子 (可选) =====
        protected override void OnInitialize(IServiceProvider services)
        {
            // 大部分依赖应通过构造函数注入
            // 此方法仅用于可选依赖或轻量级初始化
            Logger.LogInformation("SimpleCommandPlugin (Refactored) initialized");
        }

        // ===== 执行逻辑 =====
        public override async Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default)
        {
            return action.ToLowerInvariant() switch
            {
                "run" => await RunCommandAsync(args, context),
                "sendkeys" => await SendKeysAsync(args, context),
                _ => UnknownActionError(action, "run", "sendkeys") // 使用基类辅助方法
            };
        }

        /// <summary>
        /// 运行外部命令或打开文件/URL
        /// </summary>
        private async Task<PluginResult> RunCommandAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 使用基类辅助方法验证参数
            if (!TryGetRequiredArg(args, "path", out var path))
            {
                return MissingParameterError("path");
            }

            args.TryGetValue("arguments", out var arguments);
            args.TryGetValue("workingDir", out var workingDir);

            Logger.LogInformation("Running command: {Path}", path);

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
                Logger.LogInformation("Successfully executed: {Path}", path);
                return PluginResult.Ok($"Executed {path}");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "Execution failed: {Path}", path);
                return PluginResult.Error(
                    $"Execution failed: {ex.Message}",
                    PluginErrorSeverity.Recoverable
                );
            }
        }

        /// <summary>
        /// 发送键盘按键序列 (using native SendInput)
        /// </summary>
        private async Task<PluginResult> SendKeysAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context)
        {
            // 使用基类辅助方法验证参数
            if (!TryGetRequiredArg(args, "keys", out var keys))
            {
                return MissingParameterError("keys");
            }

            // 获取延迟参数（默认 50ms）
            int delay = 50;
            if (args.TryGetValue("delay", out var delayStr))
            {
                if (!int.TryParse(delayStr, out delay) || delay < 0)
                {
                    return PluginResult.Error(
                        "Invalid delay value. Must be a non-negative integer.",
                        PluginErrorSeverity.Recoverable
                    );
                }
            }

            Logger.LogInformation("Sending keys: {Keys} (delay: {Delay}ms)", keys, delay);

            try
            {
                // 等待窗口切换
                await Task.Delay(delay);

                ParseAndSendKeys(keys);

                Logger.LogInformation("Keys sent successfully");
                return PluginResult.Ok("Keys sent");
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "SendKeys failed");
                return PluginResult.Error(
                    $"SendKeys failed: {ex.Message}",
                    PluginErrorSeverity.Recoverable
                );
            }
        }

        private static void ParseAndSendKeys(string keys)
        {
            var sb = new StringBuilder();
            int i = 0;

            while (i < keys.Length)
            {
                char c = keys[i];

                if (c == '{')
                {
                    FlushTextBuffer(sb);
                    int close = keys.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        sb.Append(c);
                        i++;
                        continue;
                    }

                    string token = keys.Substring(i + 1, close - i - 1);
                    i = close + 1;

                    if (InputHelper.GetNamedKey(token) is ushort vk)
                    {
                        InputHelper.SendKeyCombination(vk);
                        continue;
                    }

                    sb.Append('{').Append(token).Append('}');
                    continue;
                }

                if (c == '^' || c == '+' || c == '%')
                {
                    FlushTextBuffer(sb);
                    var modifiers = new List<ushort>();
                    while (i < keys.Length && (keys[i] == '^' || keys[i] == '+' || keys[i] == '%'))
                    {
                        switch (keys[i])
                        {
                            case '^': modifiers.Add(InputHelper.VK_CONTROL); break;
                            case '+': modifiers.Add(InputHelper.VK_SHIFT); break;
                            case '%': modifiers.Add(InputHelper.VK_MENU); break;
                        }
                        i++;
                    }

                    if (i < keys.Length)
                    {
                        if (keys[i] == '{')
                        {
                            int close = keys.IndexOf('}', i + 1);
                            if (close >= 0)
                            {
                                string token = keys.Substring(i + 1, close - i - 1);
                                i = close + 1;
                                if (InputHelper.GetNamedKey(token) is ushort namedVk)
                                {
                                    modifiers.Add(namedVk);
                                    InputHelper.SendKeyCombination(modifiers.ToArray());
                                    continue;
                                }
                            }
                        }
                        else
                        {
                            char keyChar = keys[i];
                            i++;
                            var vk = InputHelper.CharToVkCode(keyChar);
                            modifiers.Add(vk);
                            InputHelper.SendKeyCombination(modifiers.ToArray());
                            continue;
                        }
                    }

                    continue;
                }

                sb.Append(c);
                i++;
            }

            FlushTextBuffer(sb);
        }

        private static void FlushTextBuffer(StringBuilder sb)
        {
            if (sb.Length > 0)
            {
                InputHelper.SendText(sb.ToString());
                sb.Clear();
            }
        }
    }
}
