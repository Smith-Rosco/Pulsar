// [Path]: Pulsar/Services/Interfaces/IWindowService.cs
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IWindowService
    {
        // 获取当前前台窗口信息
        WindowInfo GetForegroundWindow();

        // [Fix] 之前缺失的定义
        bool FocusWindow(string processName);

        // 启动应用
        Task<bool> LaunchApplicationAsync(string command, string? arguments);
        
        // 兼容旧接口
        Task<bool> SwitchToProcessAsync(string processName);
    }

    public record WindowInfo(string ProcessName, string ExePath, string Title);
}