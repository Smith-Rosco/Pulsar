// [Path]: Pulsar/Pulsar/Plugins/VbaRunner/ScriptEngine.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using Pulsar.Native;

namespace Pulsar.Plugins.VbaRunner
{
    /// <summary>
    /// VBA 脚本执行引擎 - 通过 COM Interop 连接 Excel/WPS 并注入执行 VBA 脚本
    /// </summary>
    public class ScriptEngine
    {
        private dynamic? _app;
        private dynamic? _workbook;
        private const int vbext_ct_StdModule = 1;

        [DllImport("oleaut32.dll", PreserveSig = true)]
        private static extern int GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object? ppunk);

        /// <summary>
        /// 获取运行中的 COM 对象
        /// </summary>
        private static object? GetActiveObjectByType(Type type)
        {
            try
            {
                Guid clsid = type.GUID;
                int hr = GetActiveObject(ref clsid, IntPtr.Zero, out object? obj);
                if (hr == 0 && obj != null)
                {
                    return obj;
                }
            }
            catch
            {
                // Ignore exceptions
            }
            return null;
        }

        /// <summary>
        /// 连接到当前活动的 Excel 或 WPS 实例
        /// </summary>
        /// <param name="targetProcessId">可选的目标进程ID，用于多实例场景</param>
        /// <returns>成功连接返回 true，否则返回 false</returns>
        public bool Connect(int targetProcessId = 0)
        {
            _app = null;
            _workbook = null;

            Debug.WriteLine("[ScriptEngine] === Starting Connect() ===");
            Debug.WriteLine($"[ScriptEngine] Target process ID: {(targetProcessId != 0 ? targetProcessId.ToString() : "not specified")}");

            // 0. 尝试根据 PID 获取进程名，以决定连接策略
            string targetProcessName = "";
            if (targetProcessId != 0)
            {
                try
                {
                    using (var proc = Process.GetProcessById(targetProcessId))
                    {
                        targetProcessName = proc.ProcessName.ToLower();
                        Debug.WriteLine($"[ScriptEngine] Target process name: {targetProcessName}");
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScriptEngine] Could not get process info for ID {targetProcessId}: {ex.Message}");
                }
            }

            bool isWps = targetProcessName == "et" || targetProcessName == "wps";
            bool isExcel = targetProcessName == "excel";
            
            // 如果没有指定进程，默认先试 WPS 再试 Excel
            if (!isWps && !isExcel)
            {
                // 尝试检测系统进程来猜测
                var etProcesses = Process.GetProcessesByName("et");
                if (etProcesses.Length > 0) isWps = true;
                else isExcel = true; 
            }

            // ==========================================
            // 阶段 1: 尝试通过 COM (GetActiveObject) 连接
            // ==========================================
            
            if (isWps || !isExcel) // 优先尝试 WPS
            {
                Debug.WriteLine("[ScriptEngine] Trying KET.Application...");
                if (TryGetActiveObject("KET.Application", out _app))
                {
                    Debug.WriteLine("[ScriptEngine] ✓ Connected to WPS (KET.Application)");
                    if (ValidateConnection(targetProcessId)) goto Success;
                    _app = null; // 验证失败，重置
                }
            }

            if (isExcel || _app == null) // 尝试 Excel
            {
                Debug.WriteLine("[ScriptEngine] Trying Excel.Application...");
                if (TryGetActiveObject("Excel.Application", out _app))
                {
                    Debug.WriteLine("[ScriptEngine] ✓ Connected to Excel (Excel.Application)");
                    if (ValidateConnection(targetProcessId)) goto Success;
                    _app = null; // 验证失败，重置
                }
            }

            // ==========================================
            // 阶段 2: 降级尝试 - 通过窗口句柄/PID 暴力查找
            // ==========================================
            
            if (targetProcessId != 0)
            {
                Debug.WriteLine($"[ScriptEngine] ⚠️ standard COM connection failed or mismatch. Trying to find specific instance for PID {targetProcessId}...");
                if (TryConnectToInstanceByProcessId(targetProcessId, targetProcessName))
                {
                    goto Success;
                }
            }

            Debug.WriteLine("[ScriptEngine] === Connect() failed ===");
            return false;

        Success:
            // 最终检查 ActiveWorkbook
            return GetActiveWorkbook();
        }

        private bool TryGetActiveObject(string progId, out dynamic? app)
        {
            app = null;
            try
            {
                Type? type = Type.GetTypeFromProgID(progId);
                if (type != null)
                {
                    app = GetActiveObjectByType(type);
                    return app != null;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] Error getting {progId}: {ex.Message}");
            }
            return false;
        }

        private bool ValidateConnection(int targetProcessId)
        {
            if (targetProcessId == 0) return true; // 没指定 PID，假设成功
            if (_app == null) return false;

            try
            {
                // 获取 COM 对象的 PID
                IntPtr hwnd = (IntPtr)_app.Hwnd;
                uint processId;
                WindowHelper.GetWindowThreadProcessId(hwnd, out processId);

                Debug.WriteLine($"[ScriptEngine] Validating connection - Target PID: {targetProcessId}, App PID: {processId}");

                if (processId == targetProcessId)
                {
                    return true;
                }
                
                Debug.WriteLine("[ScriptEngine] ❌ PID mismatch.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] ⚠️ Error validating PID: {ex.Message}");
                // 如果无法获取 PID，但能获取对象，暂时认为失败，让其进入暴力查找模式，那个模式更准
                return false;
            }
        }

        private bool GetActiveWorkbook()
        {
            if (_app == null) return false;

            try
            {
                // 1. 尝试将窗口置前，激活 ActiveWorkbook
                try
                {
                    IntPtr hwnd = (IntPtr)_app.Hwnd;
                    WindowHelper.SetForegroundWindow(hwnd);
                }
                catch { }

                // 2. 获取 Workbook
                _workbook = _app.ActiveWorkbook;
                if (_workbook != null)
                {
                    Debug.WriteLine($"[ScriptEngine] ✓ Active workbook: {_workbook.Name}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] ⚠️ Failed to get ActiveWorkbook: {ex.Message}");
            }

            Debug.WriteLine("[ScriptEngine] ❌ ActiveWorkbook is null");
            return false;
        }

        /// <summary>
        /// 获取当前工作簿中所有可见工作表的名称
        /// </summary>
        /// <returns>工作表名称列表</returns>
        public List<string> GetVisibleSheetNames()
        {
            var names = new List<string>();

            if (_workbook == null)
            {
                Debug.WriteLine("[ScriptEngine] ⚠️ GetVisibleSheetNames called but no workbook connected");
                return names;
            }

            try
            {
                foreach (dynamic sheet in _workbook.Worksheets)
                {
                    // xlSheetVisible = -1 (XlSheetVisibility enumeration)
                    if (sheet.Visible == -1)
                    {
                        names.Add(sheet.Name);
                        Debug.WriteLine($"[ScriptEngine] Found visible sheet: {sheet.Name}");
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] ❌ Error getting sheet names: {ex.Message}");
            }

            return names;
        }

        /// <summary>
        /// 将 Excel 窗口置于前台
        /// </summary>
        public void BringExcelToFront()
        {
            if (_app == null) return;

            try
            {
                IntPtr hwnd = (IntPtr)_app.Hwnd;
                WindowHelper.SetForegroundWindow(hwnd);
                Debug.WriteLine("[ScriptEngine] Excel window brought to foreground");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] ⚠️ Failed to bring Excel to front: {ex.Message}");
            }
        }

        /// <summary>
        /// 执行 VBA 脚本
        /// </summary>
        public void ExecuteScript(string filePath, string macroName, object? argument = null)
        {
            if (!File.Exists(filePath))
            {
                throw new FileNotFoundException($"Script file not found: {filePath}");
            }

            if (_workbook == null)
            {
                throw new InvalidOperationException("No workbook connected. Call Connect() first.");
            }

            Debug.WriteLine($"[ScriptEngine] Executing script: {filePath}");
            Debug.WriteLine($"[ScriptEngine] Macro: {macroName}");
            Debug.WriteLine($"[ScriptEngine] Argument: {argument ?? "(none)"}");

            // 1. 将 Excel 窗口置于前台
            BringExcelToFront();

            // 2. 读取脚本内容
            string scriptContent = File.ReadAllText(filePath);
            dynamic? vbComponent = null;

            try
            {
                // 3. 验证 VBA 项目访问权限
                var vbProject = _workbook.VBProject;

                // 4. 动态注入 VBA 模块
                vbComponent = vbProject.VBComponents.Add(vbext_ct_StdModule);
                
                // Rename to ensure ASCII name and avoid localization issues
                string moduleName = $"Pulsar_{Guid.NewGuid().ToString("N").Substring(0, 8)}";
                try { vbComponent.Name = moduleName; } catch { /* Ignore if rename fails */ }
                
                vbComponent.CodeModule.AddFromString(scriptContent);

                Debug.WriteLine($"[ScriptEngine] VBA module '{vbComponent.Name}' injected");

                // 5. 执行宏 (支持传递参数)
                // Use fully qualified name to avoid ambiguity: "ModuleName.MacroName"
                string runMacro = $"{vbComponent.Name}.{macroName}";
                
                if (argument != null)
                {
                    _app?.Run(runMacro, argument);
                    Debug.WriteLine($"[ScriptEngine] ✓ Macro executed with argument: {argument}");
                }
                else
                {
                    _app?.Run(runMacro);
                    Debug.WriteLine($"[ScriptEngine] ✓ Macro executed without arguments");
                }
            }
            catch (COMException ex) when (ex.HResult == unchecked((int)0x800A9C68))
            {
                // VBA 项目访问被拒绝
                Debug.WriteLine("[ScriptEngine] ❌ VBA project access denied");
                throw new InvalidOperationException(
                    "VBA project access denied.\n\n" +
                    "Please enable VBA trust in Excel/WPS:\n" +
                    "File → Options → Trust Center → Trust Center Settings → " +
                    "Macro Settings → Trust access to the VBA project object model",
                    ex
                );
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] ❌ Execution error: {ex.Message}");
                throw;
            }
            finally
            {
                // 6. 清理注入的模块 (无痕执行)
                if (vbComponent != null)
                {
                    try
                    {
                        _workbook.VBProject.VBComponents.Remove(vbComponent);
                        Debug.WriteLine($"[ScriptEngine] ✓ VBA module '{vbComponent.Name}' removed");
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ScriptEngine] ⚠️ Failed to remove VBA module: {ex.Message}");
                    }
                }
            }
        }

        /// <summary>
        /// 释放 COM 对象
        /// </summary>
        public void Dispose()
        {
            if (_workbook != null)
            {
                Marshal.ReleaseComObject(_workbook);
                _workbook = null;
            }

            if (_app != null)
            {
                Marshal.ReleaseComObject(_app);
                _app = null;
            }

            Debug.WriteLine("[ScriptEngine] COM objects released");
        }

        /// <summary>
        /// 尝试通过进程ID连接到特定的 Excel/WPS 实例
        /// </summary>
        private bool TryConnectToInstanceByProcessId(int targetProcessId, string processName)
        {
            Debug.WriteLine($"[ScriptEngine] Searching for process {processName} (ID: {targetProcessId})");

            try
            {
                // 枚举所有窗口
                var windowHandles = new List<IntPtr>();
                WindowHelper.EnumWindows((hwnd, lParam) =>
                {
                    uint processId;
                    WindowHelper.GetWindowThreadProcessId(hwnd, out processId);
                    if (processId == targetProcessId && WindowHelper.IsWindowVisible(hwnd))
                    {
                        windowHandles.Add(hwnd);
                    }
                    return true;
                }, IntPtr.Zero);

                Debug.WriteLine($"[ScriptEngine] Found {windowHandles.Count} visible windows for process");

                // 1. 尝试直接从主窗口获取
                foreach (var hwnd in windowHandles)
                {
                    if (TryGetExcelFromWindow(hwnd, out _app))
                    {
                        Debug.WriteLine($"[ScriptEngine] ✓ Retrieved object from window {hwnd}");
                        return true;
                    }
                }

                // 2. 深度子窗口遍历
                Debug.WriteLine("[ScriptEngine] Trying deep child window enumeration...");
                
                bool foundInChild = false;
                foreach (var parentHwnd in windowHandles)
                {
                    if (foundInChild) break;

                    EnumChildWindows(parentHwnd, (childHwnd, lParam) =>
                    {
                        // 获取类名用于调试和过滤
                        var className = new StringBuilder(256);
                        GetClassName(childHwnd, className, className.Capacity);
                        string cls = className.ToString();

                        // 过滤掉明显无关的控件
                        if (cls.StartsWith("Button") || cls.StartsWith("Static") || cls.StartsWith("Edit") || cls.Contains("Scroll"))
                        {
                            return true;
                        }

                        if (TryGetExcelFromWindow(childHwnd, out _app))
                        {
                            Debug.WriteLine($"[ScriptEngine] ✓ Retrieved object from child window {childHwnd} ({cls})");
                            foundInChild = true;
                            return false; // Stop enumeration
                        }

                        return true;
                    }, IntPtr.Zero);
                }


                if (foundInChild) return true;

                // 3. 尝试 ROT (Running Object Table)
                if (TryConnectViaROT(targetProcessId))
                {
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] Error in TryConnectToInstanceByProcessId: {ex.Message}");
            }

            return false;
        }

        /// <summary>
        /// 尝试通过 Running Object Table (ROT) 连接到特定进程的实例
        /// </summary>
        private bool TryConnectViaROT(int targetProcessId)
        {
            Debug.WriteLine("[ScriptEngine] Enumerating Running Object Table...");
            
            IRunningObjectTable? rot = null;
            IEnumMoniker? enumMoniker = null;
            IBindCtx? bindCtx = null;

            try
            {
                // 获取 ROT
                int hr = GetRunningObjectTable(0, out rot);
                if (hr != 0 || rot == null)
                {
                    Debug.WriteLine($"[ScriptEngine] GetRunningObjectTable failed: {hr}");
                    return false;
                }

                rot.EnumRunning(out enumMoniker);
                if (enumMoniker == null)
                {
                    Debug.WriteLine("[ScriptEngine] EnumRunning returned null");
                    return false;
                }

                hr = CreateBindCtx(0, out bindCtx);
                if (hr != 0 || bindCtx == null)
                {
                    Debug.WriteLine($"[ScriptEngine] CreateBindCtx failed: {hr}");
                    return false;
                }

                IMoniker[] moniker = new IMoniker[1];
                IntPtr numFetched = IntPtr.Zero;
                int count = 0;

                while (enumMoniker.Next(1, moniker, numFetched) == 0)
                {
                    count++;
                    try
                    {
                        string displayName = "";
                        try
                        {
                            moniker[0].GetDisplayName(bindCtx, null, out displayName);
                        }
                        catch { }

                        if (displayName.Contains("Excel") || displayName.Contains("KET") || 
                            displayName.Contains("ET") || displayName.Contains("Spreadsheet") ||
                            displayName.EndsWith(".xlsx") || displayName.EndsWith(".xls") || displayName.EndsWith(".et"))
                        {
                            object? obj = null;
                            try
                            {
                                rot.GetObject(moniker[0], out obj);
                                if (obj != null)
                                {
                                    dynamic? tempApp = null;
                                    try 
                                    {
                                        dynamic dynObj = obj;
                                        tempApp = dynObj.Application;
                                    }
                                    catch { continue; }

                                    if (tempApp != null)
                                    {
                                        try
                                        {
                                            IntPtr hwnd = (IntPtr)tempApp.Hwnd;
                                            uint processId;
                                            WindowHelper.GetWindowThreadProcessId(hwnd, out processId);
                                            
                                            if ((int)processId == targetProcessId)
                                            {
                                                _app = tempApp;
                                                Debug.WriteLine($"[ScriptEngine] ✓ Connected to app in ROT from target process: {displayName}");
                                                return true;
                                            }
                                        }
                                        catch (Exception ex)
                                        {
                                            Debug.WriteLine($"[ScriptEngine] Error checking ROT object properties: {ex.Message}");
                                        }
                                    }
                                }
                            }
                            catch { }
                        }
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[ScriptEngine] Error processing ROT item: {ex.Message}");
                    }
                    finally
                    {
                        if (moniker[0] != null) Marshal.ReleaseComObject(moniker[0]);
                    }
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] Error in TryConnectViaROT: {ex.Message}");
            }
            finally
            {
                if (enumMoniker != null) Marshal.ReleaseComObject(enumMoniker);
                if (rot != null) Marshal.ReleaseComObject(rot);
                if (bindCtx != null) Marshal.ReleaseComObject(bindCtx);
            }

            return false;
        }

        // --- Native Methods ---

        [DllImport("ole32.dll")]
        private static extern int GetRunningObjectTable(uint reserved, out IRunningObjectTable pprot);

        [DllImport("ole32.dll")]
        private static extern int CreateBindCtx(uint reserved, out IBindCtx ppbc);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr FindWindowEx(IntPtr parentHandle, IntPtr childAfter, string? className, string? windowName);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwndParent, WindowHelper.EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("oleacc.dll")]
        private static extern int AccessibleObjectFromWindow(IntPtr hwnd, uint dwObjectID, ref Guid riid, [MarshalAs(UnmanagedType.IUnknown)] out object? ppvObject);

        private const uint OBJID_NATIVEOM = 0xFFFFFFF0;

        /// <summary>
        /// 通过窗口句柄获取 Excel/WPS Application 对象
        /// </summary>
        private bool TryGetExcelFromWindow(IntPtr hwnd, out dynamic? app)
        {
            app = null;
            
            try
            {
                Guid IID_IDispatch = new Guid("{00020400-0000-0000-C000-000000000046}");
                object? result = null;
                int hr = AccessibleObjectFromWindow(hwnd, OBJID_NATIVEOM, ref IID_IDispatch, out result);
                
                if (hr == 0 && result != null)
                {
                    dynamic workbook = result;
                    app = workbook.Application;
                    Debug.WriteLine($"[ScriptEngine] Got Application from window, Workbooks count: {app.Workbooks?.Count}");
                    return true;
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] Error in TryGetExcelFromWindow: {ex.Message}");
            }
            
            return false;
        }
    }
}