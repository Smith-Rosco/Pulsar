using System;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    public class VbaModuleInjector
    {
        private static ILogger _logger = NullLogger.Instance;
        
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger("VbaRunner.VbaModuleInjector") ?? NullLogger.Instance;
        }
        private const int vbext_ct_StdModule = 1;
        
        // COM Error Codes
        private const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001);
        private const int VBA_E_IGNORE = unchecked((int)0x800A9C68); // Project access denied
        private const int MK_E_UNAVAILABLE = unchecked((int)0x800401E3); // Operation unavailable

        /// <summary>
        /// Injects VBA code into the workbook, executes the macro, and cleans up.
        /// </summary>
        public void Execute(dynamic workbook, string scriptContent, string macroName, object? argument)
        {
            if (workbook == null) throw new ArgumentNullException(nameof(workbook));

            dynamic? vbComponent = null;
            // Generate a safe, unique module name
            string moduleName = $"Pulsar_{Guid.NewGuid().ToString("N").Substring(0, 8)}";

            try
            {
                // 1. Inject Module (with Retry for Busy state)
                vbComponent = InjectModule(workbook, moduleName, scriptContent);

                // 2. Execute Macro (with Retry for Busy state)
                ExecuteMacro(workbook, moduleName, macroName, argument);
            }
            finally
            {
                // 3. Cleanup (Best Effort)
                if (vbComponent != null)
                {
                    CleanupModule(workbook, vbComponent);
                }
            }
        }

        private dynamic InjectModule(dynamic workbook, string moduleName, string content)
        {
            dynamic? component = null;

            ComRetryHelper.Execute(() =>
            {
                try
                {
                    // Access VBProject - this might fail if Trust Access is off
                    var vbProject = workbook.VBProject;

                    // WPS without the VBA component returns a *stub* VBProject:
                    // Name is empty, VBComponents.Count is 0 and Add() throws a
                    // NullReferenceException, which used to surface as "macro did
                    // nothing". Detect it up-front and fail with an actionable message.
                    EnsureVbaProjectIsUsable(vbProject);

                    // Create new module
                    component = vbProject.VBComponents.Add(vbext_ct_StdModule);
                    
                    // Rename
                    try { component.Name = moduleName; } catch { /* Name collision unlikely but possible */ }
                    
                    // Inject Code
                    // Strip VBE-export module attribute lines (e.g. "Attribute VB_Name = \"Module1\"").
                    // AddFromString does not process them, and their presence breaks full module
                    // compilation, making Application.Run fail with 0x800A03EC
                    // ("macro not available in this workbook or all macros are disabled").
                    string sanitized = Regex.Replace(
                        content,
                        @"(?m)^\s*Attribute\s+VB_\w+\s*=.*$",
                        string.Empty);
                    component.CodeModule.AddFromString(sanitized);
                }
                catch (COMException ex)
                {
                    // Cleanup partial injection if failed
                    if (component != null)
                    {
                        try { workbook.VBProject.VBComponents.Remove(component); } catch { }
                        component = null;
                    }

                    if (ex.ErrorCode == VBA_E_IGNORE)
                    {
                        throw new InvalidOperationException(
                            "VBA project access denied. Please enable 'Trust access to the VBA project object model' in Excel settings.", ex);
                    }
                    throw; // Rethrow to trigger retry if it's RPC_E_CALL_REJECTED
                }
            }, "Inject Module");

            return component!;
        }

        /// <summary>
        /// Detects the WPS "stub" VBProject: WPS without the VBA component hands out a
        /// shell object (empty Name, VBComponents.Count == 0, Add() throws NRE). A real
        /// Excel/WPS workbook always has at least one component (ThisWorkbook/Sheet).
        /// Conservative by design: any probing failure is treated as "usable" so the
        /// normal path is never blocked by the probe itself.
        /// </summary>
        private static void EnsureVbaProjectIsUsable(object? vbProject)
        {
            if (vbProject == null)
            {
                throw new InvalidOperationException(
                    "VBA project is unavailable (null VBProject). If you are using WPS, install the VBA component " +
                    "(vbeapi.dll ships as an interface stub only) and enable 'Trust access to the VBA project object model'.");
            }

            dynamic project = vbProject;
            object? components = null;
            try { components = project.VBComponents; }
            catch
            {
                // 探测失败（dynamic 绑定失败 / getter 抛出）→ 保守放行，
                // 由真实调用暴露错误；不得落入下方 null 抛错分支。
                return;
            }

            if (components == null)
            {
                throw new InvalidOperationException(
                    "VBA project exposes no components (VBComponents is null). If you are using WPS, install the VBA component " +
                    "(vbeapi.dll ships as an interface stub only) and enable 'Trust access to the VBA project object model'.");
            }

            int count = -1;
            try { count = (int)((dynamic)components).Count; }
            catch { /* non-COM / unexpected shape -> assume usable */ }

            if (count == 0)
            {
                throw new InvalidOperationException(
                    "VBA project contains no components (VBComponents.Count == 0). If you are using WPS, install the VBA component " +
                    "(vbeapi.dll ships as an interface stub only) and enable 'Trust access to the VBA project object model'.");
            }
        }

        private void ExecuteMacro(dynamic workbook, string moduleName, string macroName, object? argument)
        {
            string runMacro = $"{moduleName}.{macroName}";
            
            ComRetryHelper.Execute(() =>
            {
                try
                {
                    if (argument != null)
                    {
                        workbook.Application.Run(runMacro, argument);
                    }
                    else
                    {
                        workbook.Application.Run(runMacro);
                    }
                }
                catch (COMException ex)
                {
                    // 0x800A03EC : Name not found (Macro missing)
                    if (ex.ErrorCode == unchecked((int)0x800A03EC))
                    {
                        throw new InvalidOperationException($"Macro '{macroName}' not found in the injected module.", ex);
                    }
                    throw;
                }
            }, "Run Macro");
        }

        private void CleanupModule(dynamic workbook, dynamic component)
        {
            try
            {
                ComRetryHelper.Execute(() =>
                {
                    workbook.VBProject.VBComponents.Remove(component);
                }, "Cleanup Module", maxRetries: 3);
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "[VbaInjector] Failed to clean up module");
            }
        }

    }
}
