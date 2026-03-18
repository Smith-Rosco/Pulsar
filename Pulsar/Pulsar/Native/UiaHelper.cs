using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;

namespace Pulsar.Native
{
    /// <summary>
    /// UI Automation helper for interacting with focused UI elements
    /// </summary>
    public static class UiaHelper
    {
        private static ILogger _logger = NullLogger.Instance;
        
        /// <summary>
        /// Initialize the logger for UiaHelper. Should be called once during application startup.
        /// </summary>
        /// <param name="loggerFactory">Logger factory from DI container</param>
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger("UiaHelper") ?? NullLogger.Instance;
        }

        /// <summary>
        /// Attempts to set the text of the currently focused UI element using UI Automation.
        /// This is much faster and cleaner than SendInput simulation or Clipboard pasting.
        /// </summary>
        /// <param name="text">The text to inject (e.g., javascript:...).</param>
        /// <returns>True if successful, False otherwise.</returns>
        public static bool TrySetFocusedElementText(string text)
        {
            try
            {
                // Initialize COM usage for this thread if needed (STA/MTA)
                // UIA is usually best on MTA but works on STA too.
                
                // Create CUIAutomation instance
                // CLSID_CUIAutomation = {ff48dba4-60ef-4201-aa87-54103eef594e}
                Guid clsid_CUIAutomation = new Guid("ff48dba4-60ef-4201-aa87-54103eef594e");
                
                // Create the automation object
                int hr = PInvoke.CoCreateInstance(
                    clsid_CUIAutomation,
                    null,
                    CLSCTX.CLSCTX_INPROC_SERVER,
                    out IUIAutomation automation);

                if (hr != 0 || automation == null)
                {
                    _logger.LogDebug("Failed to create IUIAutomation: {HResult}", hr);
                    return false;
                }

                // Get Focused Element
                IUIAutomationElement focusedElement;
                try
                {
                    focusedElement = automation.GetFocusedElement();
                }
                catch (COMException ex)
                {
                    _logger.LogDebug(ex, "Failed to get focused element");
                    return false;
                }

                if (focusedElement == null)
                {
                    _logger.LogDebug("No focused element found");
                    return false;
                }

                // Check for ValuePattern (for Edit controls like Address Bar)
                // UIA_ValuePatternId = 10002
                int valuePatternId = 10002;
                
                // Note: CsWin32 generates GetCurrentPattern taking an int or enum depending on config.
                // We cast to ensure compatibility if it's strictly typed.
                object patternObj = focusedElement.GetCurrentPattern((UIA_PATTERN_ID)valuePatternId);
                
                if (patternObj != null && patternObj is IUIAutomationValuePattern valuePattern)
                {
                    try
                    {
                        // Set the value directly!
                        // Convert string to BSTR safely using SysAllocString
                        unsafe
                        {
                            fixed (char* pText = text)
                            {
                                // SysAllocString returns BSTR which we must free
                                BSTR bstr = PInvoke.SysAllocString((PCWSTR)pText);
                                try
                                {
                                    valuePattern.SetValue(bstr);
                                }
                                finally
                                {
                                    PInvoke.SysFreeString(bstr);
                                }
                            }
                        }
                        
                        _logger.LogDebug("Successfully set text via ValuePattern");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug(ex, "Failed to set value via ValuePattern");
                        return false;
                    }
                }
                
                _logger.LogDebug("Focused element does not support ValuePattern");
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogDebug(ex, "Unexpected error in UIA text injection");
                return false;
            }
        }
    }
}
