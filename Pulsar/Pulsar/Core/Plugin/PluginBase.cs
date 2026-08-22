// [Path]: Pulsar/Pulsar/Core/Plugin/PluginBase.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Localization;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 一个插件 Action 的处理函数。签名与 <see cref="PluginBase{T}.ExecuteAsync"/> 对齐，
    /// 只是省去了 action 名——action 名由 handler map 的键承担。
    /// </summary>
    public delegate Task<PluginResult> PluginActionHandler(
        IReadOnlyDictionary<string, string> args,
        PulsarContext context,
        CancellationToken cancellationToken);
    /// <summary>
    /// 插件抽象基类 - 提供通用功能和模板方法
    /// 
    /// 优势:
    /// 1. 消除 Service Locator 反模式，使用构造函数注入
    /// 2. 统一日志记录格式
    /// 3. 减少样板代码 (约 30%)
    /// 4. 编译时依赖检查
    /// 5. 提供生命周期钩子的默认实现
    /// 
    /// 使用示例:
    /// <code>
    /// public class MyPlugin : PluginBase&lt;MyPlugin&gt;
    /// {
    ///     private readonly IMyService _myService;
    ///     
    ///     public MyPlugin(ILogger&lt;MyPlugin&gt; logger, IMyService myService) 
    ///         : base(logger)
    ///     {
    ///         _myService = myService;
    ///     }
    ///     
    ///     public override string Id => "com.company.myplugin";
    ///     public override PluginTier Tier => PluginTier.Extension;
    ///     // ... 实现其他抽象成员
    /// }
    /// </code>
    /// </summary>
    /// <typeparam name="T">插件实现类型 (用于泛型日志)</typeparam>
    public abstract class PluginBase<T> : IPulsarPlugin, IPluginTiered where T : PluginBase<T>
    {
        /// <summary>
        /// 结构化日志记录器 (自动注入)
        /// </summary>
        protected ILogger<T> Logger { get; }

        /// <summary>
        /// 本地化服务。所有用户可见文本（包括错误消息）必须经它解析。
        /// </summary>
        protected ILocalizationService Loc { get; }

        /// <summary>
        /// 构造函数 - 自动注入日志记录器与本地化服务
        /// </summary>
        /// <param name="logger">日志记录器 (由 DI 容器注入)</param>
        /// <param name="localizationService">本地化服务 (由 DI 容器注入)</param>
        protected PluginBase(ILogger<T> logger, ILocalizationService localizationService)
        {
            Logger = logger ?? throw new ArgumentNullException(nameof(logger));
            Loc = localizationService ?? throw new ArgumentNullException(nameof(localizationService));
        }

        #region IPulsarPlugin 必需属性

        /// <summary>
        /// 插件唯一标识符 (建议使用反向域名，如 "com.pulsar.xxx")
        /// </summary>
        public abstract string Id { get; }

        /// <summary>
        /// 插件显示名称
        /// </summary>
        public abstract string DisplayName { get; }

        /// <summary>
        /// 插件版本号 (语义化版本，如 "1.0.0")
        /// </summary>
        public abstract string Version { get; }

        /// <summary>
        /// 插件作者/维护者
        /// </summary>
        public abstract string Author { get; }

        /// <summary>
        /// 插件简短描述
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// 插件图标 (Segoe Fluent Icons 字符或 Emoji)
        /// </summary>
        public abstract string Icon { get; }

        /// <summary>
        /// 是否允许禁用 (核心插件应返回 false)
        /// </summary>
        public abstract bool CanDisable { get; }

        /// <summary>
        /// 插件分类标签 (用于分组和筛选)
        /// 默认返回 "General"
        /// </summary>
        public virtual IEnumerable<string> Tags => new[] { "General" };

        /// <summary>
        /// 最低 Pulsar 版本要求 (语义化版本)
        /// 默认为 "1.0.0"
        /// </summary>
        public virtual string MinPulsarVersion => "1.0.0";

        /// <summary>
        /// 文档链接 (可选)
        /// </summary>
        public virtual string? DocumentationUrl => null;

        /// <summary>
        /// 许可证 (如 "MIT"/"Proprietary")
        /// 默认为 "MIT"
        /// </summary>
        public virtual string License => "MIT";

        /// <summary>
        /// 依赖的插件 ID 列表 (可选)
        /// 用于确保插件按正确顺序加载
        /// </summary>
        public virtual IEnumerable<string> Dependencies => Array.Empty<string>();

        #endregion

        #region IPluginTiered 实现

        /// <summary>
        /// 插件层级 (Core 或 Extension)
        /// </summary>
        public abstract PluginTier Tier { get; }

        #endregion

        #region 生命周期管理 (模板方法模式)

        /// <summary>
        /// 初始化插件 (由 PluginRegistry 调用)
        /// 
        /// 注意: 此方法使用模板方法模式，子类应重写 OnInitialize() 而非此方法
        /// </summary>
        /// <param name="services">服务提供者 (用于解析额外依赖)</param>
        public void Initialize(IServiceProvider services)
        {
            Logger.LogInformation("[{PluginId}] Initializing plugin...", Id);

            try
            {
                OnInitialize(services);
                Logger.LogInformation("[{PluginId}] Plugin initialized successfully", Id);
            }
            catch (Exception ex)
            {
                Logger.LogError(ex, "[{PluginId}] Plugin initialization failed", Id);
                throw;
            }
        }

        /// <summary>
        /// 初始化钩子 - 子类可重写以执行自定义初始化逻辑
        /// 
        /// 注意: 大部分依赖应通过构造函数注入，此方法仅用于:
        /// 1. 解析可选依赖 (通过 services.GetService)
        /// 2. 执行轻量级初始化逻辑
        /// 3. 避免在此方法中执行耗时操作
        /// </summary>
        /// <param name="services">服务提供者</param>
        protected virtual void OnInitialize(IServiceProvider services)
        {
            // 默认实现为空，子类可选择性重写
        }

        #endregion

        #region 执行逻辑

        /// <summary>
        /// 执行插件动作 (由 PluginRegistry 调度)
        /// </summary>
        /// <param name="action">动作名 (如 "run", "fill")</param>
        /// <param name="args">静态参数 (来自 Profiles.json)</param>
        /// <param name="context">运行时上下文</param>
        /// <returns>插件执行结果</returns>
        public abstract Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken = default
        );

        #endregion

        #region 辅助方法

        /// <summary>
        /// 按 action 名分发到 handler map。action 名统一小写后查找，可携带别名映射
        /// （例如 PKI 的 "inject" → "fill"）。未命中时返回本地化的 UnknownAction 错误。
        /// handler map 是运行期"存在哪些 Action"的唯一事实源——插件不再手写 switch。
        /// </summary>
        /// <param name="action">动作名（不区分大小写）</param>
        /// <param name="args">静态参数</param>
        /// <param name="context">运行时上下文</param>
        /// <param name="cancellationToken">取消令牌</param>
        /// <param name="handlers">action 名 → 处理函数的映射</param>
        /// <param name="aliases">别名 → 规范名映射</param>
        protected Task<PluginResult> DispatchAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken,
            IReadOnlyDictionary<string, PluginActionHandler> handlers,
            IReadOnlyDictionary<string, string>? aliases = null)
        {
            var normalized = action.ToLowerInvariant();

            if (aliases != null && aliases.TryGetValue(normalized, out var target))
            {
                normalized = target;
            }

            if (handlers.TryGetValue(normalized, out var handler))
            {
                return handler(args, context, cancellationToken);
            }

            return Task.FromResult(UnknownActionError(normalized, handlers.Keys.ToArray()));
        }

        /// <summary>
        /// 验证必需参数是否存在
        /// </summary>
        /// <param name="args">参数字典</param>
        /// <param name="paramName">参数名</param>
        /// <param name="value">输出参数值</param>
        /// <returns>验证结果</returns>
        protected bool TryGetRequiredArg(
            IReadOnlyDictionary<string, string> args,
            string paramName,
            out string value)
        {
            if (args.TryGetValue(paramName, out var val) && !string.IsNullOrEmpty(val))
            {
                value = val;
                return true;
            }

            value = string.Empty;
            Logger.LogWarning("[{PluginId}] Missing required parameter: {ParamName}", Id, paramName);
            return false;
        }

        /// <summary>
        /// 创建参数缺失错误结果（本地化，携带 ErrorCode）
        /// </summary>
        /// <param name="paramName">参数名</param>
        /// <returns>错误结果</returns>
        protected PluginResult MissingParameterError(string paramName)
        {
            return PluginResult.Error(
                string.Format(Loc["Plugin.Common.MissingRequiredParameter"], paramName),
                PluginErrorSeverity.Recoverable,
                PluginErrorCode.MissingRequiredParameter);
        }

        /// <summary>
        /// 创建未知动作错误结果（本地化，携带 ErrorCode）
        /// </summary>
        /// <param name="action">动作名</param>
        /// <param name="supportedActions">支持的动作列表</param>
        /// <returns>错误结果</returns>
        protected PluginResult UnknownActionError(string action, params string[] supportedActions)
        {
            var message = supportedActions.Length > 0
                ? string.Format(Loc["Plugin.Common.UnknownActionSupported"], action, string.Join(", ", supportedActions))
                : string.Format(Loc["Plugin.Common.UnknownAction"], action);

            return PluginResult.Error(
                message,
                PluginErrorSeverity.Recoverable,
                PluginErrorCode.UnknownAction);
        }

        #endregion
    }
}
