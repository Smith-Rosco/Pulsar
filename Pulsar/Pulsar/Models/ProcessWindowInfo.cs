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

        // 用于 UI 显示的辅助属性
        public string DisplayName => string.IsNullOrWhiteSpace(Title) ? ProcessName : Title;
    }
}