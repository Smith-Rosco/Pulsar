// [Path]: Pulsar/Pulsar/Services/FocusRestoreMode.cs

namespace Pulsar.Services
{
    /// <summary>
    /// 焦点归还模式
    /// 用于控制 Pulsar 关闭时如何处理窗口焦点
    /// </summary>
    public enum FocusRestoreMode
    {
        /// <summary>
        /// 不归还焦点
        /// 用于 Quick Switch 场景，焦点已由 SwitchToPreviousWindow 处理
        /// </summary>
        NoRestore,
        
        /// <summary>
        /// 归还到 Pulsar 唤起前的窗口
        /// 用于用户取消操作的场景（按 Esc 或点击外部）
        /// </summary>
        RestorePrevious,
        
        /// <summary>
        /// 归还到指定的目标窗口
        /// 用于插件执行后需要切换到特定窗口的场景
        /// </summary>
        RestoreTarget
    }
}
