using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    public class VbaModuleInjector
    {
        public static ILogger? Logger { get; set; }
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
                    
                    // Create new module
                    component = vbProject.VBComponents.Add(vbext_ct_StdModule);
                    
                    // Rename
                    try { component.Name = moduleName; } catch { /* Name collision unlikely but possible */ }
                    
                    // Inject Code
                    component.CodeModule.AddFromString(content);
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
                Logger?.LogDebug(ex, "[VbaInjector] Failed to clean up module");
            }
        }

    }
}
