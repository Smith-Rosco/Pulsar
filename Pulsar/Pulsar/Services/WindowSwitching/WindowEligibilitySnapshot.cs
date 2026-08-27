using System;
using System.Diagnostics;
using System.Text;
using Pulsar.Native;

namespace Pulsar.Services.WindowSwitching
{
    /// <summary>
    /// 窗口的结构性事实快照 —— "可切换窗口"判定的唯一输入。
    /// <see cref="WindowEligibilityPolicy"/> 只消费快照、不触碰 native，因此全部规则可纯逻辑单测。
    /// <para>
    /// 读取原则：<see cref="FromHwnd"/> 只抓取判定所需的非阻塞事实（不含 <c>GetWindowText</c>，
    /// 后者是跨进程 SendMessage、可能在无响应应用上阻塞，仅用于日志/展示，由调用方按需填充 <see cref="Title"/>）。
    /// </para>
    /// </summary>
    public sealed record WindowEligibilitySnapshot
    {
        public IntPtr Hwnd { get; init; }

        /// <summary>窗口所属进程 ID；无法取得时为 0。</summary>
        public uint Pid { get; init; }

        /// <summary>进程名（用于进程黑名单）；无法解析时为空串。</summary>
        public string ProcessName { get; init; } = string.Empty;

        /// <summary>窗口类名（用于类名黑名单）。</summary>
        public string ClassName { get; init; } = string.Empty;

        /// <summary>窗口标题。FromHwnd 不填充（避免阻塞读取）；枚举路径/展示路径按需填充。</summary>
        public string Title { get; init; } = string.Empty;

        /// <summary>窗口矩形；<c>GetWindowRect</c> 失败时为 null（视为屏幕外）。</summary>
        public PulsarNative.RECT? Rect { get; init; }

        /// <summary>当前虚拟屏幕矩形（GetSystemMetrics SM_*VIRTUALSCREEN）。</summary>
        public PulsarNative.RECT VirtualScreenRect { get; init; }

        /// <summary>最小化状态（最小化窗口豁免物理可见性规则）。</summary>
        public bool IsIconic { get; init; }

        public bool IsVisible { get; init; }

        public bool IsCloaked { get; init; }

        public long ExStyle { get; init; }

        /// <summary>窗口样式（用于排除 WS_CHILD 子窗口）。</summary>
        public long Style { get; init; }

        public IntPtr OwnerHwnd { get; init; }

        /// <summary>
        /// 从真实 HWND 一次性抓取判定所需的结构事实（唯一 native 适配器之一；
        /// 测试直接构造快照，即为第二个适配器）。
        /// </summary>
        /// <param name="hwnd">目标窗口。</param>
        /// <param name="captureTitle">是否读取窗口标题。GetWindowText 是跨进程 SendMessage、
        /// 可能在无响应应用上阻塞 —— 仅当存在标题依赖规则（TitlePattern）时传入 true。</param>
        public static WindowEligibilitySnapshot FromHwnd(IntPtr hwnd, bool captureTitle = false)
        {
            PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);

            var className = new StringBuilder(256);
            PulsarNative.GetClassName(hwnd, className, className.Capacity);

            return new WindowEligibilitySnapshot
            {
                Hwnd = hwnd,
                Pid = pid,
                ProcessName = ResolveProcessName(pid),
                ClassName = className.ToString(),
                Title = captureTitle ? ReadWindowText(hwnd) : string.Empty,
                IsIconic = PulsarNative.IsIconic(hwnd),
                IsVisible = PulsarNative.IsWindowVisible(hwnd),
                IsCloaked = IsDwmCloaked(hwnd),
                ExStyle = PulsarNative.GetWindowLong(hwnd, PulsarNative.GWL_EXSTYLE),
                Style = PulsarNative.GetWindowLong(hwnd, PulsarNative.GWL_STYLE),
                OwnerHwnd = PulsarNative.GetWindow(hwnd, PulsarNative.GW_OWNER),
                Rect = TryGetWindowRect(hwnd),
                VirtualScreenRect = GetVirtualScreenRect()
            };
        }

        internal static string ReadWindowText(IntPtr hwnd)
        {
            int length = PulsarNative.GetWindowTextLength(hwnd);
            if (length == 0)
            {
                return string.Empty;
            }

            var sb = new StringBuilder(length + 1);
            PulsarNative.GetWindowText(hwnd, sb, sb.Capacity);
            return sb.ToString();
        }

        /// <summary>读取虚拟屏幕矩形（GetSystemMetrics SM_*VIRTUALSCREEN）。</summary>
        public static PulsarNative.RECT GetVirtualScreenRect()
        {
            int left = PulsarNative.GetSystemMetrics(PulsarNative.SM_XVIRTUALSCREEN);
            int top = PulsarNative.GetSystemMetrics(PulsarNative.SM_YVIRTUALSCREEN);
            return new PulsarNative.RECT
            {
                Left = left,
                Top = top,
                Right = left + PulsarNative.GetSystemMetrics(PulsarNative.SM_CXVIRTUALSCREEN),
                Bottom = top + PulsarNative.GetSystemMetrics(PulsarNative.SM_CYVIRTUALSCREEN)
            };
        }

        private static string ResolveProcessName(uint pid)
        {
            if (pid == 0)
            {
                return string.Empty;
            }

            try
            {
                using var process = Process.GetProcessById((int)pid);
                return process.ProcessName;
            }
            catch
            {
                // 进程可能在抓取快照前退出；视为无进程名（黑名单谓词对空串返回 false）。
                return string.Empty;
            }
        }

        internal static bool IsDwmCloaked(IntPtr hwnd)
        {
            return PulsarNative.DwmGetWindowAttribute(hwnd, PulsarNative.DWMWA_CLOAKED, out int value, sizeof(int)) == 0
                && value != 0;
        }

        internal static PulsarNative.RECT? TryGetWindowRect(IntPtr hwnd)
        {
            return PulsarNative.GetWindowRect(hwnd, out var rect) ? rect : null;
        }
    }
}
