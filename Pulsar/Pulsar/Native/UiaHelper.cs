using System;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Windows.Win32;
using Windows.Win32.Foundation;
using Windows.Win32.System.Com;
using Windows.Win32.UI.Accessibility;

namespace Pulsar.Native
{
    public static class UiaHelper
    {
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
                    Debug.WriteLine($"[UiaHelper] Failed to create IUIAutomation: {hr}");
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
                    Debug.WriteLine($"[UiaHelper] Failed to get focused element: {ex.Message}");
                    return false;
                }

                if (focusedElement == null)
                {
                    Debug.WriteLine("[UiaHelper] No focused element found.");
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
                        
                        Debug.WriteLine("[UiaHelper] Successfully set text via ValuePattern.");
                        return true;
                    }
                    catch (Exception ex)
                    {
                        Debug.WriteLine($"[UiaHelper] Failed to set value: {ex.Message}");
                        return false;
                    }
                }
                
                Debug.WriteLine("[UiaHelper] Focused element does not support ValuePattern.");
                return false;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[UiaHelper] Unexpected error: {ex.Message}");
                return false;
            }
        }
    }
}
