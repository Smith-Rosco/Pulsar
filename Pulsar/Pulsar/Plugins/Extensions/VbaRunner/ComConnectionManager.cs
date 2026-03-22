using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Native;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// Manages COM connections to Excel/WPS instances.
    /// Handles discovery via GetActiveObject, Window Handles, and Running Object Table (ROT).
    /// </summary>
    public class ComConnectionManager
    {
        private static ILogger _logger = NullLogger.Instance;
        
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger("VbaRunner.ComConnectionManager") ?? NullLogger.Instance;
        }
        // COM Imports
        [DllImport("oleaut32.dll", PreserveSig = true)]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, PulsarNative.EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint dwObjectID, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject);

        private const uint OBJID_NATIVEOM = 0xFFFFFFF0;

        /// <summary>
        /// Attempts to connect to an Excel or WPS instance targeting a specific process ID.
        /// </summary>
        public bool TryGetApplication(int targetProcessId, out dynamic? app)
        {
            app = null;
            string targetProcessName = "";

            // 1. Resolve Process Name
            if (targetProcessId != 0)
            {
                try
                {
                    using (var proc = Process.GetProcessById(targetProcessId))
                    {
                        targetProcessName = proc.ProcessName.ToLower();
                    }
                }
                catch
                {
                    // Process might have exited
                    return false;
                }
            }

            // Strategy A: Standard COM (GetActiveObject)
            // Fast, but unreliable if multiple instances are running.
            if (TryGetActiveObjectStrategy(targetProcessId, targetProcessName, out app))
            {
                return true;
            }

            // Strategy B: Window Handle Enum (AccessibleObjectFromWindow)
            // Reliable for specific PIDs.
            if (targetProcessId != 0 && TryGetByWindowHandleStrategy(targetProcessId, out app))
            {
                return true;
            }

            // Strategy C: Running Object Table (ROT)
            // Deep search, slowest but finds hidden instances.
            if (targetProcessId != 0 && TryGetByRotStrategy(targetProcessId, out app))
            {
                return true;
            }

            return false;
        }

        private bool TryGetActiveObjectStrategy(int targetProcessId, string processName, out dynamic? app)
        {
            app = null;
            bool isWps = processName.Contains("et") || processName.Contains("wps");
            bool isExcel = processName.Contains("excel");

            // Default order: WPS -> Excel
            if (!isWps && !isExcel) { isWps = true; isExcel = true; }

            if (isWps)
            {
                if (TryGetActiveObject("KET.Application", out app) && ValidatePid(app, targetProcessId)) return true;
                if (TryGetActiveObject("ET.Application", out app) && ValidatePid(app, targetProcessId)) return true;
            }

            if (isExcel)
            {
                if (TryGetActiveObject("Excel.Application", out app) && ValidatePid(app, targetProcessId)) return true;
            }

            app = null;
            return false;
        }

        private bool TryGetByWindowHandleStrategy(int targetProcessId, out dynamic? app)
        {
            app = null;
            _logger.LogDebug("[ComManager] Strategy B: Window Search for PID {Pid}", targetProcessId);

            var candidates = new List<IntPtr>();
            PulsarNative.EnumWindows((hwnd, lParam) =>
            {
                PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);
                if (pid == targetProcessId && PulsarNative.IsWindowVisible(hwnd))
                {
                    candidates.Add(hwnd);
                }
                return true;
            }, IntPtr.Zero);

            // 1. Try Top-Level Windows
            foreach (var hwnd in candidates)
            {
                if (TryGetExcelFromWindow(hwnd, out app)) return true;
            }

            // 2. Try Child Windows (Deep Search)
            foreach (var parentHwnd in candidates)
            {
                dynamic? foundApp = null;
                EnumChildWindows(parentHwnd, (childHwnd, lParam) =>
                {
                    StringBuilder className = new StringBuilder(256);
                    GetClassName(childHwnd, className, className.Capacity);
                    string cls = className.ToString();

                    // Filter common noise
                    if (cls.StartsWith("Button") || cls.StartsWith("Static") || cls.Contains("Scroll")) return true;

                    if (TryGetExcelFromWindow(childHwnd, out foundApp)) return false; // Stop

                    return true;
                }, IntPtr.Zero);

                if (foundApp != null)
                {
                    app = foundApp;
                    return true;
                }
            }

            return false;
        }

        private bool TryGetByRotStrategy(int targetProcessId, out dynamic? app)
        {
            app = null;
            _logger.LogDebug("[ComManager] Strategy C: ROT Search for PID {Pid}", targetProcessId);

            IRunningObjectTable? rot = null;
            IEnumMoniker? enumMoniker = null;
            IBindCtx? bindCtx = null;

            try
            {
                if (GetRunningObjectTable(0, out rot) != 0) return false;
                rot.EnumRunning(out enumMoniker);
                if (enumMoniker == null) return false;
                if (CreateBindCtx(0, out bindCtx) != 0) return false;

                IMoniker[] moniker = new IMoniker[1];
                IntPtr numFetched = IntPtr.Zero;

                while (enumMoniker.Next(1, moniker, numFetched) == 0)
                {
                    try
                    {
                        string displayName = "";
                        moniker[0].GetDisplayName(bindCtx, null, out displayName);

                        if (IsRelevantMonikerName(displayName))
                        {
                            rot.GetObject(moniker[0], out object? obj);
                            if (obj != null)
                            {
                                try
                                {
                                    dynamic dynObj = obj;
                                    // Some ROT objects are workbooks, some are apps. Normalize to App.
                                    dynamic tempApp = IsPropertyExist(dynObj, "Application") ? dynObj.Application : dynObj;
                                    
                                    if (ValidatePid(tempApp, targetProcessId))
                                    {
                                        app = tempApp;
                                        return true;
                                    }
                                }
                                catch { }
                            }
                        }
                    }
                    catch { }
                    finally
                    {
                        if (moniker[0] != null) Marshal.ReleaseComObject(moniker[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[ComManager] ROT Error");
            }
            finally
            {
                if (enumMoniker != null) Marshal.ReleaseComObject(enumMoniker);
                if (rot != null) Marshal.ReleaseComObject(rot);
                if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
            }

            return false;
        }

        // --- Helpers ---

        private bool TryGetActiveObject(string progId, out dynamic? app)
        {
            app = null;
            try
            {
                Type? type = Type.GetTypeFromProgID(progId);
                if (type != null)
                {
                    Guid clsid = type.GUID;
                    GetActiveObject(ref clsid, IntPtr.Zero, out object? obj);
                    if (obj != null)
                    {
                        app = obj;
                        return true;
                    }
                }
            }
            catch { }
            return false;
        }

        private bool TryGetExcelFromWindow(IntPtr hwnd, out dynamic? app)
        {
            app = null;
            try
            {
                Guid IID_IDispatch = new Guid("{00020400-0000-0000-C000-000000000046}");
                object? result = null;
                // OBJID_NATIVEOM retrieves the native object model
                if (AccessibleObjectFromWindow(hwnd, OBJID_NATIVEOM, ref IID_IDispatch, out result) == 0 && result != null)
                {
                    dynamic workbook = result;
                    // Verify it has an Application property
                    app = workbook.Application;
                    return true;
                }
            }
            catch { }
            return false;
        }

        private bool ValidatePid(dynamic? app, int targetPid)
        {
            if (app == null) return false;
            if (targetPid == 0) return true; // Wildcard

            try
            {
                IntPtr hwnd = (IntPtr)app.Hwnd;
                PulsarNative.GetWindowThreadProcessId(hwnd, out uint pid);
                return pid == targetPid;
            }
            catch
            {
                return false;
            }
        }

        private bool IsRelevantMonikerName(string name)
        {
            return name.Contains("Excel") || name.Contains("KET") || name.Contains("ET") ||
                   name.EndsWith(".xlsx") || name.EndsWith(".xls") || name.EndsWith(".xlsm");
        }

        private bool IsPropertyExist(dynamic obj, string name)
        {
            try
            {
                // Quick check
                var _ = obj.Application;
                return true;
            }
            catch
            {
                return false;
            }
        }
    }
}
