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
    /// 插件执行结果
    /// </summary>
    public readonly struct PluginResult
    {
        public bool Success { get; init; }
        public string? Message { get; init; }
        public VisualCue Cue { get; init; }

        /// <summary>
        /// 创建成功结果
        /// </summary>
        public static PluginResult Ok(string? message = null) =>
            new() { Success = true, Message = message, Cue = VisualCue.ShowCheckMark };

        /// <summary>
        /// 创建错误结果
        /// </summary>
        public static PluginResult Error(string message) =>
            new() { Success = false, Message = message, Cue = VisualCue.ErrorRed };
    }
}
