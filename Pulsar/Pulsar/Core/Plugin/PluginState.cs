namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// 插件生命周期状态
    /// </summary>
    public enum PluginState
    {
        /// <summary>
        /// 未加载
        /// </summary>
        Unloaded = 0,
        
        /// <summary>
        /// 正在加载
        /// </summary>
        Loading = 1,
        
        /// <summary>
        /// 已加载（就绪）
        /// </summary>
        Loaded = 2,

        /// <summary>
        /// 已启用（允许运行）
        /// </summary>
        Enabled = 3,

        /// <summary>
        /// 已禁用（已加载但不可执行）
        /// </summary>
        Disabled = 4,

        /// <summary>
        /// 正在运行
        /// </summary>
        Running = 5,

        /// <summary>
        /// 正在卸载
        /// </summary>
        Unloading = 6,

        /// <summary>
        /// 故障状态（加载失败或运行时崩溃）
        /// </summary>
        Faulted = 7
    }
}
