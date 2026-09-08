using System;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// 自启动（开机运行）注册表访问。从 TrayIconService 拆出（C2）：
    /// 托盘服务不应直接读写 HKCU Run 键——该职责独立成服务后，
    /// 注册表语义（键路径、value 名、异常吞掉策略）有了单一归宿与测试缝。
    /// </summary>
    public interface IAutoStartService
    {
        /// <summary>当前是否已注册自启动。</summary>
        bool IsEnabled();

        /// <summary>切换自启动状态（未注册则注册，已注册则撤销）。</summary>
        void Toggle();
    }
}
