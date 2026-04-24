using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Media;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services.Interfaces;

namespace Pulsar.Services.WindowSwitching
{
    internal sealed class WindowInventoryService
    {
        public Task<List<ProcessWindowInfo>> GetActiveWindowsAsync(
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon,
            IProcessRegistryService? processRegistryService)
        {
            return Task.Run(() =>
            {
                List<ProcessWindowInfo> results = EnumerateWindows(
                    processIdFilter: null,
                    isBlacklisted,
                    snapshotWindow,
                    extractIcon);

                if (processRegistryService != null && results.Count > 0)
                {
                    _ = Task.Run(() => processRegistryService.RegisterProcessesAsync(results));
                }

                return results;
            });
        }

        public Task<List<ProcessWindowInfo>> GetProcessWindowsAsync(
            int targetProcessId,
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon)
        {
            return Task.Run(() => EnumerateWindows(
                targetProcessId,
                isBlacklisted,
                snapshotWindow,
                extractIcon));
        }

        private static List<ProcessWindowInfo> EnumerateWindows(
            int? processIdFilter,
            Func<string, bool> isBlacklisted,
            Func<IntPtr, WindowTrackingSnapshot> snapshotWindow,
            Func<string, ImageSource?> extractIcon)
        {
            List<ProcessWindowInfo> results = new();
            int zOrderIndex = 0;

            PulsarNative.EnumWindows((hWnd, _) =>
            {
                if (!PulsarNative.IsWindowVisible(hWnd)) return true;

                if (PulsarNative.DwmGetWindowAttribute(hWnd, PulsarNative.DWMWA_CLOAKED, out int isCloakedVal, sizeof(int)) == 0 && isCloakedVal != 0)
                {
                    return true;
                }

                long exStyle = PulsarNative.GetWindowLong(hWnd, PulsarNative.GWL_EXSTYLE);
                if ((exStyle & PulsarNative.WS_EX_TOOLWINDOW) != 0) return true;

                IntPtr owner = PulsarNative.GetWindow(hWnd, PulsarNative.GW_OWNER);
                if (owner != IntPtr.Zero && (exStyle & PulsarNative.WS_EX_APPWINDOW) == 0) return true;

                PulsarNative.GetWindowThreadProcessId(hWnd, out uint processId);
                if (processIdFilter.HasValue && processId != processIdFilter.Value) return true;

                int length = PulsarNative.GetWindowTextLength(hWnd);
                if (!processIdFilter.HasValue && length == 0) return true;

                StringBuilder sb = new(length + 1);
                if (length > 0)
                {
                    PulsarNative.GetWindowText(hWnd, sb, sb.Capacity);
                }

                string title = sb.ToString();
                if (!processIdFilter.HasValue && (string.IsNullOrWhiteSpace(title) || title == "Program Manager")) return true;

                try
                {
                    using Process proc = Process.GetProcessById((int)processId);
                    if (proc.HasExited) return true;
                    if (isBlacklisted(proc.ProcessName)) return true;

                    string fullPath = string.Empty;
                    try { fullPath = proc.MainModule?.FileName ?? string.Empty; } catch { }

                    ImageSource? iconSource = string.IsNullOrEmpty(fullPath) ? null : extractIcon(fullPath);

                    DateTime startTime = DateTime.MinValue;
                    try { startTime = proc.StartTime; } catch { }

                    DateTime zOrderActivationTime = DateTime.Now.AddSeconds(-zOrderIndex);
                    WindowTrackingSnapshot tracking = snapshotWindow(hWnd);

                    results.Add(new ProcessWindowInfo
                    {
                        Title = string.IsNullOrEmpty(title) ? "Window" : title,
                        ProcessName = proc.ProcessName,
                        ExePath = fullPath,
                        Handle = hWnd,
                        AppIcon = iconSource,
                        StartTime = startTime,
                        LastActivationTime = zOrderActivationTime,
                        FirstSeenTime = tracking.FirstSeenTime,
                        RealActivationTime = tracking.ActivationTime
                    });

                    zOrderIndex++;
                }
                catch
                {
                }

                return true;
            }, IntPtr.Zero);

            return results;
        }
    }
}
