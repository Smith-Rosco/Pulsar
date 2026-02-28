using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;

namespace Pulsar.Native
{
    public static class UiaHelper
    {
        public static ILogger? Logger { get; set; }

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
                    Logger?.LogDebug("[UiaHelper] Failed to create IUIAutomation: {HResult}", hr);
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
                    Logger?.LogDebug(ex, "[UiaHelper] Failed to get focused element");
                    return false;
                }

                if (focusedElement == null)
                {
                    Logger?.LogDebug("[UiaHelper] No focused element found.");
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
                        
                        Logger?.LogDebug("[UiaHelper] Successfully set text via ValuePattern.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Logger?.LogDebug(ex, "[UiaHelper] Failed to set value");
                        return false;
                    }
                }
                
                Logger?.LogDebug("[UiaHelper] Focused element does not support ValuePattern.");
                return false;
            }
            catch (Exception ex)
            {
                Logger?.LogDebug(ex, "[UiaHelper] Unexpected error");
                return false;
            }
        }
    }
}
