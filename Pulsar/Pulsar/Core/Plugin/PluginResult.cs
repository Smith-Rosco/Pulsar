// [Path]: Pulsar/Pulsar/Core/Plugin/PluginResult.cs

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 视觉提示类型
    /// </summary>
    public enum VisualCue
    {
        None,
        ShowCheckMark,      // 绿色勾号 Toast
        ShakeWindow,        // 窗口抖动
        ErrorRed            // 红色边框闪烁
    }

    /// <summary>
    /// 插件错误严重程度
    /// </summary>
    public enum PluginErrorSeverity
    {
        /// <summary>
        /// 可恢复错误 (如用户输入错误)，不计入熔断
        /// </summary>
        Recoverable,

        /// <summary>
        /// 严重错误 (如配置错误、依赖缺失)，计入熔断
        /// </summary>
        Critical
    }

    /// <summary>
    /// Stable, localization-independent error category used by the UX feedback layer.
    /// </summary>
    public enum PluginErrorCode
    {
        None,
        MissingRequiredParameter,
        InvalidConfiguration,
        UnsupportedOperation,
        TemporaryUnavailable,
        NotFound,
        AccessDenied,
        ExecutionFailed,
        DependencyMissing,
        UserCancelled,
        UnknownAction,
        NoTargetWindow,
        NoRunningBrowser,
        UnsafePath
    }

    /// <summary>
    /// 插件执行结果
    /// </summary>
    public readonly struct PluginResult
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public VisualCue Cue { get; init; }
        public PluginErrorSeverity Severity { get; init; }
        public PluginErrorCode ErrorCode { get; init; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static PluginResult Ok(string? message = null) =>
            new() { Success = true, Message = message, Cue = VisualCue.ShowCheckMark, Severity = PluginErrorSeverity.Recoverable, ErrorCode = PluginErrorCode.None };

        /// <summary>
        /// 创建错误结果
        /// </summary>
        public static PluginResult Error(
            string message,
            PluginErrorSeverity severity = PluginErrorSeverity.Recoverable,
            PluginErrorCode errorCode = PluginErrorCode.None) =>
            new() { Success = false, Message = message, Cue = VisualCue.ErrorRed, Severity = severity, ErrorCode = errorCode };
    }
}
