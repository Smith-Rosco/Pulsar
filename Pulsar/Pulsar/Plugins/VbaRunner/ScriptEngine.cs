using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using Pulsar.Native;

namespace Pulsar.Plugins.VbaRunner
{
    /// <summary>
    /// Orchestrates VBA script execution by coordinating connection, injection, and execution.
    /// </summary>
    public class ScriptEngine : IDisposable
    {
        private readonly ComConnectionManager _connectionManager;
        private readonly VbaModuleInjector _injector;
        
        private dynamic? _app;
        private dynamic? _workbook;

        public ScriptEngine()
        {
            _connectionManager = new ComConnectionManager();
            _injector = new VbaModuleInjector();
        }

        /// <summary>
        /// Connects to the active Excel/WPS instance.
        /// </summary>
        public bool Connect(int targetProcessId = 0)
        {
            Dispose(); // Clean up previous session

            if (_connectionManager.TryGetApplication(targetProcessId, out _app))
            {
                Debug.WriteLine("[ScriptEngine] Connected to Application.");
                
                // Try to get ActiveWorkbook with retry
                return TryGetActiveWorkbook();
            }

            Debug.WriteLine("[ScriptEngine] Failed to connect to any instance.");
            return false;
        }

        private bool TryGetActiveWorkbook()
        {
            if (_app == null) return false;

            try
            {
                return ComRetryHelper.Execute(() =>
                {
                    // Bring to front first to ensure activation
                    try
                    {
                        IntPtr hwnd = (IntPtr)_app.Hwnd;
                        WindowHelper.SetForegroundWindow(hwnd);
                    }
                    catch { }

                    _workbook = _app.ActiveWorkbook;
                    if (_workbook != null)
                    {
                        Debug.WriteLine($"[ScriptEngine] Active workbook: {_workbook.Name}");
                        return true;
                    }
                    return false;
                }, "Get ActiveWorkbook");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] Failed to get ActiveWorkbook: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Gets visible sheet names for the selector UI.
        /// </summary>
        public List<string> GetVisibleSheetNames()
        {
            var names = new List<string>();
            if (_workbook == null) return names;

            try
            {
                ComRetryHelper.Execute(() =>
                {
                    foreach (dynamic sheet in _workbook.Worksheets)
                    {
                        if (sheet.Visible == -1) // xlSheetVisible
                        {
                            names.Add(sheet.Name);
                        }
                    }
                }, "Get Sheets");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[ScriptEngine] Error getting sheets: {ex.Message}");
            }

            return names;
        }

        /// <summary>
        /// Executes the VBA script content.
        /// </summary>
        public void ExecuteScriptContent(string scriptContent, string macroName, object? argument = null)
        {
            if (string.IsNullOrWhiteSpace(scriptContent)) throw new ArgumentException("Script content is empty.", nameof(scriptContent));
            if (_workbook == null) throw new InvalidOperationException("No workbook connected.");

            // Delegate to injector
            _injector.Execute(_workbook, scriptContent, macroName, argument);
        }

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
            GC.SuppressFinalize(this);
        }
    }
}
