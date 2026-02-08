// [Path]: Pulsar/Pulsar/Plugins/VbaRunner/ScriptEngine.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;

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

        [DllImport("user32.dll")]
        static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("oleaut32.dll", PreserveSig = false)]
        private static extern void GetActiveObject(ref Guid rclsid, IntPtr pvReserved, [MarshalAs(UnmanagedType.IUnknown)] out object ppunk);

        /// <summary>
        /// 获取运行中的 COM 对象 (替代 Marshal.GetActiveObject)
        /// </summary>
        private static object? GetActiveObject(Type type)
        {
            try
            {
                Guid clsid = type.GUID;
                GetActiveObject(ref clsid, IntPtr.Zero, out object obj);
                return obj;
            }
            catch
            {
                return null;
            }
        }

        /// <summary>
        /// 连接到当前活动的 Excel 或 WPS 实例
        /// </summary>
        /// <returns>成功连接返回 true，否则返回 false</returns>
        public bool Connect()
        {
            _app = null;

            // 1. 尝试连接 WPS 表格 (KET.Application)
            try
            {
                Type? ketType = Type.GetTypeFromProgID("KET.Application");
                if (ketType != null)
                {
                    _app = GetActiveObject(ketType);
                    if (_app != null)
                    {
                        Debug.WriteLine("[ScriptEngine] Connected to WPS (KET.Application)");
                    }
                }
            }
            catch
            {
                Debug.WriteLine("[ScriptEngine] WPS not found, trying Excel...");
            }

            // 2. 降级尝试连接 Microsoft Excel
            if (_app == null)
            {
                try
                {
                    Type? excelType = Type.GetTypeFromProgID("Excel.Application");
                    if (excelType != null)
                    {
                        _app = GetActiveObject(excelType);
                        if (_app != null)
                        {
                            Debug.WriteLine("[ScriptEngine] Connected to Excel (Excel.Application)");
                        }
                    }
                }
                catch
                {
                    Debug.WriteLine("[ScriptEngine] ❌ Neither WPS nor Excel found");
                }
            }

            // 3. 验证是否有活动工作簿
            if (_app != null)
            {
                try
                {
                    _workbook = _app.ActiveWorkbook;
                    if (_workbook != null)
                    {
                        Debug.WriteLine($"[ScriptEngine] ✓ Active workbook: {_workbook.Name}");
                        return true;
                    }
                    else
                    {
                        Debug.WriteLine("[ScriptEngine] ❌ No active workbook found");
                        return false;
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[ScriptEngine] ❌ Error accessing workbook: {ex.Message}");
                    return false;
                }
            }

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
                SetForegroundWindow(hwnd);
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
        /// <param name="filePath">脚本文件路径</param>
        /// <param name="macroName">宏名称 (默认 "Main")</param>
        /// <param name="argument">传递给宏的可选参数</param>
        /// <exception cref="FileNotFoundException">脚本文件不存在</exception>
        /// <exception cref="InvalidOperationException">未连接到 Excel 或 VBA 权限被拒绝</exception>
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
                vbComponent.CodeModule.AddFromString(scriptContent);

                Debug.WriteLine($"[ScriptEngine] VBA module '{vbComponent.Name}' injected");

                // 5. 执行宏 (支持传递参数)
                if (argument != null)
                {
                    _app?.Run(macroName, argument);
                    Debug.WriteLine($"[ScriptEngine] ✓ Macro executed with argument: {argument}");
                }
                else
                {
                    _app?.Run(macroName);
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
    }
}
