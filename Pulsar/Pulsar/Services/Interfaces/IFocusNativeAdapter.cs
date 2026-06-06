using System;

namespace Pulsar.Services.Interfaces
{
    public interface IFocusNativeAdapter
    {
        IntPtr GetForegroundWindow();
        uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);
        uint GetCurrentThreadId();
        bool IsWindow(IntPtr hWnd);
        bool IsIconic(IntPtr hWnd);
        bool IsWindowVisible(IntPtr hWnd);
        bool ShowWindow(IntPtr hWnd, int nCmdShow);
        bool BringWindowToTop(IntPtr hWnd);
        bool SetForegroundWindowNative(IntPtr hWnd);
        bool AllowSetForegroundWindow(int dwProcessId);
        void KeybdEvent(byte bVk, byte bScan, uint dwFlags, int dwExtraInfo);
        bool AttachThreadInput(uint idAttach, uint idAttachTo, bool fAttach);
        bool FlashWindowEx(ref Pulsar.Native.PulsarNative.FLASHWINFO pfwi);

        void LockForegroundTimeout();
        void UnlockForegroundTimeout();
    }
}
