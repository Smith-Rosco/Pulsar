using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;

namespace ExamplePlugin
{
    /// <summary>
    /// 示例插件 - 演示如何创建一个 Pulsar 插件
    /// </summary>
    public class ExamplePlugin : IPulsarPlugin, IPluginTiered
    {
        private ILogger<ExamplePlugin>? _logger;
        private ExampleHelper? _helper;

        /// <summary>
        /// 插件唯一标识符（必须与 manifest.json 中的 Id 一致）
        /// </summary>
        public string Id => "com.example.exampleplugin";

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public string DisplayName => "Example Plugin";

        /// <summary>
        /// 插件描述
        /// </summary>
        public string Description => "A simple example plugin that demonstrates the Pulsar plugin system";

        /// <summary>
        /// 插件版本
        /// </summary>
        public string Version => "1.0.0";

        /// <summary>
        /// 插件作者
        /// </summary>
        public string Author => "Your Name";

        /// <summary>
        /// 插件图标（可以是 Emoji、图片路径或图标 Key）
        /// </summary>
        public string Icon => "🔌";

        /// <summary>
        /// 是否可以禁用（Extension 插件可以禁用）
        /// </summary>
        public bool CanDisable => true;

        /// <summary>
        /// 插件层级（Extension = 扩展插件）
        /// </summary>
        public PluginTier Tier => PluginTier.Extension;

        /// <summary>
        /// 插件初始化
        /// </summary>
        /// <param name="serviceProvider">依赖注入服务提供者</param>
        public void Initialize(IServiceProvider serviceProvider)
        {
            // 获取日志服务
            _logger = serviceProvider.GetService(typeof(ILogger<ExamplePlugin>)) as ILogger<ExamplePlugin>;
            _logger?.LogInformation("[{PluginName}] Plugin initialized", DisplayName);

            // 初始化辅助类
            _helper = new ExampleHelper(_logger);
        }

        /// <summary>
        /// 执行插件动作（新版 API - 使用 IReadOnlyDictionary）
        /// </summary>
        /// <param name="action">动作名称</param>
        /// <param name="args">动作参数（键值对）</param>
        /// <param name="context">Pulsar 上下文（包含窗口信息、剪贴板等）</param>
        /// <returns>插件执行结果</returns>
        public async Task<PluginResult> ExecuteAsync(
            string action, 
            IReadOnlyDictionary<string, string> args, 
            PulsarContext context)
        {
            try
            {
                _logger?.LogDebug("[{PluginName}] Executing action: {Action}", DisplayName, action);

                // 根据 action 执行不同的操作
                return action.ToLowerInvariant() switch
                {
                    "hello" => await SayHelloAsync(args, context),
                    "info" => await ShowInfoAsync(context),
                    "dialog" => await ShowDialogAsync(context),
                    "process" => await ProcessDataAsync(args, context),
                    _ => PluginResult.Error($"Unknown action: {action}")
                };
            }
            catch (Exception ex)
            {
                _logger?.LogError(ex, "[{PluginName}] Execution failed", DisplayName);
                return PluginResult.Error($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例动作：打招呼
        /// </summary>
        private async Task<PluginResult> SayHelloAsync(
            IReadOnlyDictionary<string, string> args, 
            PulsarContext context)
        {
            // 获取当前活动窗口标题
            var windowTitle = context.ActiveWindow?.Title ?? "Unknown";

            // 从参数中获取自定义名称（可选）
            var name = args.TryGetValue("name", out var n) ? n : "World";

            var message = $"Hello {name} from {DisplayName}!\n\nCurrent window: {windowTitle}";

            _logger?.LogInformation("[{PluginName}] Said hello to {Name}", DisplayName, name);

            // 返回成功结果（会显示通知）
            return PluginResult.Ok(message);
        }

        /// <summary>
        /// 示例动作：显示插件信息
        /// </summary>
        private async Task<PluginResult> ShowInfoAsync(PulsarContext context)
        {
            var info = $"Plugin: {DisplayName}\n" +
                       $"Version: {Version}\n" +
                       $"Author: {Author}\n" +
                       $"Description: {Description}";

            return PluginResult.Ok(info);
        }

        /// <summary>
        /// 示例动作：显示对话框
        /// </summary>
        private async Task<PluginResult> ShowDialogAsync(PulsarContext context)
        {
            // 在 UI 线程上显示对话框
            string? result = null;

            await System.Windows.Application.Current.Dispatcher.InvokeAsync(() =>
            {
                var dialog = new ExampleDialog
                {
                    Owner = System.Windows.Application.Current.MainWindow
                };

                if (dialog.ShowDialog() == true)
                {
                    result = dialog.UserInput;
                }
            });

            if (result != null)
            {
                return PluginResult.Ok($"User input: {result}");
            }

            return PluginResult.Ok("Dialog cancelled");
        }

        /// <summary>
        /// 示例动作：处理数据（使用 Helper 类）
        /// </summary>
        private async Task<PluginResult> ProcessDataAsync(
            IReadOnlyDictionary<string, string> args, 
            PulsarContext context)
        {
            if (!args.TryGetValue("data", out var data) || string.IsNullOrEmpty(data))
            {
                return PluginResult.Error("Missing required parameter: data");
            }

            // 使用辅助类处理数据
            var processed = _helper?.ProcessData(data) ?? data;

            return PluginResult.Ok($"Processed: {processed}");
        }

        /// <summary>
        /// 插件卸载清理
        /// </summary>
        public void Dispose()
        {
            _logger?.LogInformation("[{PluginName}] Plugin disposed", DisplayName);
            _helper = null;
        }
    }
}
