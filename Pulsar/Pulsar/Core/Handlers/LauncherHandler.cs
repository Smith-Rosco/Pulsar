using System;
using System.Diagnostics;
using System.Threading.Tasks;
using Pulsar.Core.Interfaces;
using Pulsar.Models;
using Pulsar.Services.Interfaces;

namespace Pulsar.Core.Handlers
{
    /// <summary>
    /// 负责处理 LauncherItem：智能切换或启动应用程序
    /// </summary>
    public class LauncherHandler : IActionHandler
    {
        private readonly IWindowService _windowService;

        public LauncherHandler(IWindowService windowService)
        {
            _windowService = windowService;
        }

        public async Task ExecuteAsync(GridItemBase item)
        {
            if (item is not LauncherItem launcherItem) return;
            if (string.IsNullOrWhiteSpace(launcherItem.ProcessName)) return;

            // 1. 尝试切换
            bool switched = await _windowService.SwitchToProcessAsync(launcherItem.ProcessName);
            if (switched) return;

            // 2. 切换失败（程序未运行），尝试智能启动
            if (!string.IsNullOrWhiteSpace(launcherItem.ExePath))
            {
                try
                {
                    var startInfo = new ProcessStartInfo
                    {
                        FileName = launcherItem.ExePath,
                        Arguments = launcherItem.Arguments,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Normal // 确保新程序获取焦点
                    };
                    Process.Start(startInfo);
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LauncherHandler] Smart Launch failed: {ex.Message}");
                    // TODO: 可以在这里集成 Toast 服务通知用户
                }
            }
            else
            {
                Debug.WriteLine($"[LauncherHandler] Cannot switch to {launcherItem.ProcessName}: Not running and no ExePath.");
            }
        }
    }
}