// [Path]: Pulsar/Services/Interfaces/IWindowService.cs
using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IWindowService
    {
        // 原有的功能
        Task<bool> SwitchToProcessAsync(string processName);
        Task<bool> LaunchApplicationAsync(string command, string? arguments);

        // [新增] 获取当前前台窗口的信息（用于识别当前所在的软件）
        WindowInfo GetForegroundWindow();
    }

    // [新增] 简单的窗口信息记录
    public record WindowInfo(string ProcessName, string ExePath, string Title);
}