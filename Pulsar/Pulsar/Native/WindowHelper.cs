using System;
using System.Runtime.InteropServices;
using System.Text;
using Pulsar.Native;

namespace Pulsar.Native
{
    [Obsolete("Use PulsarNative instead")]
    public static class WindowHelper
    {
        public const int DWMSBT_NONE = 2;
        public const int DWMWA_SYSTEMBACKDROP_TYPE = 38;

        public static bool EnumWindows(PulsarNative.EnumWindowsProc lpEnumFunc, IntPtr lParam) 
            => PulsarNative.EnumWindows(lpEnumFunc, lParam);

        public static uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId) 
            => PulsarNative.GetWindowThreadProcessId(hWnd, out lpdwProcessId);

        public static bool IsWindowVisible(IntPtr hWnd) 
            => PulsarNative.IsWindowVisible(hWnd);

        public static bool IsWindow(IntPtr hWnd) 
            => PulsarNative.IsWindow(hWnd);

        public static bool IsIconic(IntPtr hWnd) 
            => PulsarNative.IsIconic(hWnd);

        public static bool ShowWindow(IntPtr hWnd, int nCmdShow) 
            => PulsarNative.ShowWindow(hWnd, nCmdShow);

        public static bool SetForegroundWindow(IntPtr hWnd) 
            => PulsarNative.SetForegroundWindow(hWnd);

        public static IntPtr GetForegroundWindow() 
            => PulsarNative.GetForegroundWindow();

        public static IntPtr GetWindow(IntPtr hWnd, uint uCmd) 
            => PulsarNative.GetWindow(hWnd, uCmd);

        public static bool GetWindowRect(IntPtr hWnd, out PulsarNative.RECT lpRect) 
            => PulsarNative.GetWindowRect(hWnd, out lpRect);

        public static int GetWindowTextLength(IntPtr hWnd) 
            => PulsarNative.GetWindowTextLength(hWnd);

        public static int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount) 
            => PulsarNative.GetWindowText(hWnd, lpString, nMaxCount);

        public static long GetWindowLong(IntPtr hWnd, int nIndex) 
            => PulsarNative.GetWindowLong(hWnd, nIndex);

        public static IntPtr SetWindowLong(IntPtr hWnd, int nIndex, long dwNewLong) 
            => PulsarNative.SetWindowLong(hWnd, nIndex, dwNewLong);

        public static int SW_RESTORE => PulsarNative.SW_RESTORE;

        public static int GWL_EXSTYLE => PulsarNative.GWL_EXSTYLE;

        public static long WS_EX_TOOLWINDOW => PulsarNative.WS_EX_TOOLWINDOW;

        public static bool SetProcessWorkingSetSize(IntPtr hProcess, IntPtr dwMin, IntPtr dwMax)
            => PulsarNative.SetProcessWorkingSetSize(hProcess, dwMin, dwMax);

        public static IntPtr GetCurrentProcess() 
            => PulsarNative.GetCurrentProcess();

        public static int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize)
            => PulsarNative.DwmSetWindowAttribute(hwnd, attr, ref attrValue, attrSize);

        public static void CheckSystemIntegrity()
        {
        }

        public static void EmergencyRestore()
        {
            try
            {
                var hwnd = PulsarNative.GetForegroundWindow();
                if (hwnd != IntPtr.Zero)
                {
                    PulsarNative.SetForegroundWindow(hwnd);
                }
            }
            catch
            {
            }
        }

        [Obsolete("Use PulsarNative.EnumWindowsProc instead")]
        public delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);
    }
}