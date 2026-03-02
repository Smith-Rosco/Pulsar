using System;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;

namespace ExamplePlugin
{
    /// <summary>
    /// 示例插件 - 演示如何创建一个 Pulsar 插件
    /// </summary>
    public class ExamplePlugin : IPulsarPlugin
    {
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
        /// 插件初始化
        /// </summary>
        /// <param name="serviceProvider">依赖注入服务提供者</param>
        public void Initialize(IServiceProvider serviceProvider)
        {
            // 在这里初始化插件
            // 例如：注册服务、加载配置等
            Console.WriteLine($"[{DisplayName}] Plugin initialized");
        }

        /// <summary>
        /// 执行插件动作
        /// </summary>
        /// <param name="action">动作名称</param>
        /// <param name="args">动作参数</param>
        /// <param name="context">Pulsar 上下文（包含窗口信息、剪贴板等）</param>
        /// <returns>插件执行结果</returns>
        public async Task<PluginResult> ExecuteAsync(string action, string[] args, PulsarContext context)
        {
            try
            {
                // 根据 action 执行不同的操作
                switch (action.ToLower())
                {
                    case "hello":
                        return await SayHelloAsync(context);

                    case "info":
                        return await ShowInfoAsync(context);

                    default:
                        return PluginResult.Failed($"Unknown action: {action}");
                }
            }
            catch (Exception ex)
            {
                return PluginResult.Failed($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// 示例动作：打招呼
        /// </summary>
        private async Task<PluginResult> SayHelloAsync(PulsarContext context)
        {
            // 获取当前活动窗口标题
            var windowTitle = context.ActiveWindow?.Title ?? "Unknown";

            var message = $"Hello from {DisplayName}!\n\nCurrent window: {windowTitle}";

            // 返回成功结果（会显示通知）
            return PluginResult.Success(message);
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

            return PluginResult.Success(info);
        }

        /// <summary>
        /// 插件卸载清理
        /// </summary>
        public void Dispose()
        {
            // 在这里清理资源
            Console.WriteLine($"[{DisplayName}] Plugin disposed");
        }
    }
}
