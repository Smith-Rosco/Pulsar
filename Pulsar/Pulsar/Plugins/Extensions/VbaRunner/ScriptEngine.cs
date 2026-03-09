using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using Pulsar.Native;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// Orchestrates VBA script execution by coordinating connection, injection, and execution.
    /// </summary>
    public class ScriptEngine : IDisposable
    {
        public static ILogger? Logger { get; set; }
        private readonly ComConnectionManager _connectionManager;
        private readonly VbaModuleInjector _injector;
        private readonly PrerequisiteValidator _validator;
        
        private dynamic? _app;
        private dynamic? _workbook;

        public ScriptEngine()
        {
            _connectionManager = new ComConnectionManager();
            _injector = new VbaModuleInjector();
            _validator = new PrerequisiteValidator(Logger);
        }

        /// <summary>
        /// Connects to the active Excel/WPS instance.
        /// </summary>
        public bool Connect(int targetProcessId = 0)
        {
            Dispose(); // Clean up previous session

            if (_connectionManager.TryGetApplication(targetProcessId, out _app))
            {
                Logger?.LogDebug("[ScriptEngine] Connected to Application.");
                
                // Try to get ActiveWorkbook with retry
                return TryGetActiveWorkbook();
            }

            Logger?.LogDebug("[ScriptEngine] Failed to connect to any instance.");
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
                        Logger?.LogDebug("[ScriptEngine] Active workbook: {Name}", (string)_workbook.Name);
                        return true;
                    }
                    return false;
                }, "Get ActiveWorkbook");
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "[ScriptEngine] Failed to get ActiveWorkbook");
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
                Logger?.LogDebug(ex, "[ScriptEngine] Error getting sheets");
            }

            return names;
        }

        /// <summary>
        /// Validate prerequisites against active workbook
        /// </summary>
        /// <param name="requirements">List of requirement strings (e.g., "Sheet=SheetName")</param>
        /// <returns>Validation result with details</returns>
        public PrerequisiteResult ValidatePrerequisites(List<string> requirements)
        {
            if (_workbook == null)
            {
                return PrerequisiteResult.Failure(
                    new List<string> { "No active workbook" });
            }
            
            return _validator.Validate(_workbook, requirements);
        }
        
        /// <summary>
        /// Get filtered sheet names based on filter pattern
        /// </summary>
        /// <param name="filter">Filter pattern (e.g., "exclude:_Config_*" or "include:Data*")</param>
        /// <returns>Filtered list of visible sheet names</returns>
        public List<string> GetFilteredSheetNames(string? filter = null)
        {
            var allSheets = GetVisibleSheetNames();
            
            if (string.IsNullOrWhiteSpace(filter))
                return allSheets;
            
            try
            {
                // Parse filter: "exclude:pattern" or "include:pattern"
                var parts = filter.Split(':', 2);
                if (parts.Length != 2) return allSheets;
                
                string mode = parts[0].Trim().ToLowerInvariant();
                string pattern = parts[1].Trim();
                
                // Convert wildcard pattern to regex
                string regexPattern = "^" + 
                    Regex.Escape(pattern)
                        .Replace("\\*", ".*")
                        .Replace("\\?", ".") + 
                    "$";
                
                var regex = new Regex(regexPattern, RegexOptions.IgnoreCase);
                
                return mode switch
                {
                    "exclude" => allSheets.Where(s => !regex.IsMatch(s)).ToList(),
                    "include" => allSheets.Where(s => regex.IsMatch(s)).ToList(),
                    _ => allSheets
                };
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "[ScriptEngine] Error applying sheet filter");
                return allSheets;
            }
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
