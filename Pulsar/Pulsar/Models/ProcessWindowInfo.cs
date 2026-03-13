using System;
using System.Windows.Media;

namespace Pulsar.Models
{
    public class ProcessWindowInfo
    {
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public IntPtr Handle { get; set; }
        public ImageSource? AppIcon { get; set; }
        public DateTime StartTime { get; set; }
        
        /// <summary>
        /// 窗口最后一次被激活的时间 (用于智能切换到最近使用的窗口)
        /// 如果无法获取，则回退到 StartTime
        /// [Legacy] 保留用于向后兼容，基于 Z-Order 的合成时间戳
        /// </summary>
        public DateTime LastActivationTime { get; set; }
        
        /// <summary>
        /// 窗口首次被 Pulsar 发现的时间 (用于稳定排序)
        /// 提供肌肉记忆友好的固定顺序，不受窗口激活顺序影响
        /// </summary>
        public DateTime FirstSeenTime { get; set; }
        
        /// <summary>
        /// 窗口真实的最后激活时间 (通过全局注册表追踪)
        /// 反映真实的用户交互顺序，用于智能切换和预览选择
        /// </summary>
        public DateTime RealActivationTime { get; set; }
        
        /// <summary>
        /// 格式化的进程名 - 首字母大写 (如 "Excel")
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string FormattedProcessName => Pulsar.Helpers.ProcessNameFormatter.ToDisplayName(ProcessName);
        
        /// <summary>
        /// 用于 UI 显示的辅助属性 - 优先显示窗口标题，否则显示格式化的进程名
        /// </summary>
        [System.Text.Json.Serialization.JsonIgnore]
        public string DisplayName => string.IsNullOrWhiteSpace(Title) ? FormattedProcessName : Title;
    }
}
