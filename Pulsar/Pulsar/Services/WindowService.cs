// [Path]: Pulsar/Services/WindowService.cs
using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services
{
    public class WindowService : IWindowService
    {
        // --- Native Import ---
        [DllImport("user32.dll", EntryPoint = "GetForegroundWindow")]
        private static extern IntPtr GetForegroundWindow_Native();

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        // --- Interface Implementations ---

        public WindowInfo GetForegroundWindow()
        {
            try
            {
                IntPtr hWnd = GetForegroundWindow_Native();
                if (hWnd == IntPtr.Zero) return new WindowInfo("Global", "", "Desktop");

                GetWindowThreadProcessId(hWnd, out uint processId);
                using (var process = Process.GetProcessById((int)processId))
                {
                    string path = "";
                    try { path = process.MainModule?.FileName ?? ""; } catch { }
                    return new WindowInfo(process.ProcessName.ToLower(), path, process.MainWindowTitle);
                }
            }
            catch
            {
                return new WindowInfo("Global", "", "Unknown");
            }
        }

        // [Fix] 必须明确实现 FocusWindow
        public bool FocusWindow(string processName)
        {
            string targetName = processName.ToLower().Replace(".exe", "");
            var processes = Process.GetProcessesByName(targetName);

            foreach (var proc in processes)
            {
                if (proc.MainWindowHandle != IntPtr.Zero)
                {
                    ForceForegroundWindow(proc.MainWindowHandle);
                    return true;
                }
            }
            return false;
        }

        public Task<bool> LaunchApplicationAsync(string command, string? arguments)
        {
            return Task.Run(() =>
            {
                try
                {
                    Process.Start(new ProcessStartInfo
                    {
                        FileName = command,
                        Arguments = arguments,
                        UseShellExecute = true
                    });
                    return true;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"Launch Error: {ex.Message}");
                    return false;
                }
            });
        }

        public Task<bool> SwitchToProcessAsync(string processName)
        {
            return Task.Run(() => FocusWindow(processName));
        }

        // --- Native Helpers ---

        private void ForceForegroundWindow(IntPtr hWnd)
        {
            if (NativeMethods.IsIconic(hWnd)) NativeMethods.ShowWindow(hWnd, 9);
            NativeMethods.keybd_event(0x12, 0, 0, 0); // Alt Down
            NativeMethods.SetForegroundWindow(hWnd);
            NativeMethods.keybd_event(0x12, 0, 2, 0); // Alt Up
        }
    }

    internal static class NativeMethods
    {
        [DllImport("user32.dll")] internal static extern bool SetForegroundWindow(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool IsIconic(IntPtr hWnd);
        [DllImport("user32.dll")] internal static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);
        [DllImport("user32.dll")] internal static extern void keybd_event(byte bVk, byte bScan, uint dwFlags, uint dwExtraInfo);
    }
}