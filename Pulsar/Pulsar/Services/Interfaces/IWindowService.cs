// [Path]: Pulsar/Pulsar/Services/Interfaces/IWindowService.cs

using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Media;

namespace Pulsar.Services.Interfaces
{
    // [New] 用于选择器的数据传输对象
    public class ProcessWindowInfo
    {
        public string Title { get; set; } = string.Empty;
        public string ProcessName { get; set; } = string.Empty;
        public string ExePath { get; set; } = string.Empty;
        public System.IntPtr Handle { get; set; }

        // [New] 存储转换后的图标
        public ImageSource? AppIcon { get; set; }
    }

    public record WindowInfo(string ProcessName, string ExePath, string Title);

    public interface IWindowService
    {
        // 获取当前前台窗口信息
        WindowInfo GetForegroundWindow();

        // 激活指定进程的窗口
        bool FocusWindow(string processName);

        // 启动应用
        Task<bool> LaunchApplicationAsync(string command, string? arguments);

        // 兼容旧接口（切换）
        Task<bool> SwitchToProcessAsync(string processName);

        // [New] 获取当前活跃窗口列表 (用于选择器)
        Task<List<ProcessWindowInfo>> GetActiveWindowsAsync();
    }
}