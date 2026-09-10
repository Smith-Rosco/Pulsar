# [SKILL pulsar-ui-runtime-verification] 窗口尺寸注入器
#
# 用途：当缺陷的真实触发源依赖用户环境（例如 Window.DpiChanged 只在真机出现、自动化恒为 0）时，
#       改用「汇聚到同一处理函数」的另一个事件源来制造等价场景。本脚本在 E2E 驱动导航期间
#       持续微调 Pulsar 设置窗口尺寸 → 触发 NavPaneGrid.SizeChanged → 与 DpiChanged 汇聚到
#       同一个 RepositionNavIndicator()。
#
# ⚠ 运行前告知用户：会改变窗口尺寸（约 8px 抖动，不改变位置/不抢焦点）。
# 用法（PowerShell 工具环境是 pwsh，用 & 直接调用；需并发时后台运行）：
#   & "<path>\window-resize-inject.ps1" -Seconds 90 -LogPath "<repo>\artifacts\nav-resize-inject.log"
# 有效性论证：防护加在汇聚点，故「哪种触发源到达」不影响结论；但不等价于在真机见过原始触发。
param(
    [int]$Seconds = 90,
    [string]$LogPath = 'E:\8_Project\10_C#\Pulsar_Project\artifacts\nav-resize-inject.log'
)

$ErrorActionPreference = 'Stop'

Add-Type -TypeDefinition @'
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

public static class NavResizeInjector
{
    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc cb, IntPtr lParam);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint pid);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern bool IsWindow(IntPtr hWnd);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowText(IntPtr hWnd, StringBuilder s, int n);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int cx, int cy, uint flags);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr hWnd, out RECT r);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT { public int Left; public int Top; public int Right; public int Bottom; }

    private const uint SWP_NOZORDER = 0x0004;
    private const uint SWP_NOACTIVATE = 0x0010;

    public static string Run(int seconds, string logPath)
    {
        var sb = new StringBuilder();
        sb.AppendLine("=== injector start " + DateTime.Now.ToString("HH:mm:ss.fff") + " ===");

        var deadline = DateTime.Now.AddSeconds(seconds);
        IntPtr hwnd = IntPtr.Zero;

        while (DateTime.Now < deadline && hwnd == IntPtr.Zero)
        {
            hwnd = FindSettingsWindow();
            if (hwnd == IntPtr.Zero) Thread.Sleep(120);
        }

        if (hwnd == IntPtr.Zero)
        {
            sb.AppendLine("WINDOW NOT FOUND");
            File.WriteAllText(logPath, sb.ToString());
            return "WINDOW NOT FOUND";
        }

        RECT r;
        GetWindowRect(hwnd, out r);
        int x = r.Left, y = r.Top;
        int w = r.Right - r.Left, h = r.Bottom - r.Top;
        sb.AppendLine("found hwnd=0x" + hwnd.ToInt64().ToString("X") + " rect=" + x + "," + y + "," + w + "x" + h
            + " at " + DateTime.Now.ToString("HH:mm:ss.fff"));

        bool tall = true;
        int n = 0;
        while (DateTime.Now < deadline && IsWindow(hwnd))
        {
            int hh = tall ? h + 8 : h - 8;
            SetWindowPos(hwnd, IntPtr.Zero, x, y, w, hh, SWP_NOZORDER | SWP_NOACTIVATE);
            sb.AppendLine(DateTime.Now.ToString("HH:mm:ss.fff") + " resize " + w + "x" + hh);
            tall = !tall;
            n++;
            Thread.Sleep(55);
        }

        sb.AppendLine("=== injector done n=" + n + " at " + DateTime.Now.ToString("HH:mm:ss.fff") + " ===");
        File.WriteAllText(logPath, sb.ToString());
        return "OK n=" + n;
    }

    private static IntPtr FindSettingsWindow()
    {
        var pids = new List<uint>();
        foreach (var p in Process.GetProcessesByName("Pulsar")) pids.Add((uint)p.Id);
        if (pids.Count == 0) return IntPtr.Zero;

        IntPtr found = IntPtr.Zero;
        EnumWindows(delegate (IntPtr hWnd, IntPtr lParam)
        {
            uint pid;
            GetWindowThreadProcessId(hWnd, out pid);
            if (!pids.Contains(pid) || !IsWindowVisible(hWnd)) return true;

            var title = new StringBuilder(256);
            GetWindowText(hWnd, title, 256);
            if (title.Length == 0) return true;

            RECT r;
            GetWindowRect(hWnd, out r);
            if ((r.Right - r.Left) < 600) return true;   // 过滤托盘等窄窗口，命中设置窗口（1000×700）

            found = hWnd;
            return false;
        }, IntPtr.Zero);

        return found;
    }
}
'@

$result = [NavResizeInjector]::Run($Seconds, $LogPath)
Write-Output $result
