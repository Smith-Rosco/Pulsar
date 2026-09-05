// [Path]: Pulsar/Pulsar.E2E/Driver/UiaDriver.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using FlaUI.Core.AutomationElements;
using FlaUI.Core.Definitions;
using FlaUI.UIA3;

namespace Pulsar.E2E.Driver
{
    /// <summary>Screen-space rectangle of a UIA element (physical pixels).</summary>
    public readonly record struct UiElementBounds(double X, double Y, double Width, double Height);

    /// <summary>An element found by AutomationId with its key UIA properties.</summary>
    public sealed class UiElementInfo
    {
        public string AutomationId { get; init; } = string.Empty;
        public string Name { get; init; } = string.Empty;
        public string ControlType { get; init; } = string.Empty;
        public bool IsEnabled { get; init; }
        public bool IsOffscreen { get; init; }
        public UiElementBounds Bounds { get; init; }
    }

    /// <summary>
    /// UIA3-based lookup, assertion and clicking over the debug instance's windows.
    ///
    /// Element identity is ALWAYS the AutomationId — localized display text is
    /// never used for lookup because Pulsar is bilingual (EN/ZH).
    /// </summary>
    public sealed class UiaDriver : IDisposable
    {
        private readonly UIA3Automation _automation = new();

        /// <summary>Remembered for diagnostics; element search is desktop-wide.</summary>
        public int AttachedProcessId { get; private set; }

        public void Attach(int processId)
        {
            AttachedProcessId = processId;
        }

        private AutomationElement GetDesktop()
        {
            return _automation.GetDesktop();
        }

        /// <summary>Finds the radial menu window by its stable AutomationId.</summary>
        public AutomationElement? FindWindowByAutomationId(string automationId, TimeSpan timeout)
        {
            var desktop = GetDesktop();
            var deadline = DateTime.UtcNow + timeout;
            while (DateTime.UtcNow < deadline)
            {
                var window = desktop.FindFirstChild(cf => cf.ByAutomationId(automationId));
                if (window != null)
                {
                    return window;
                }
                Thread.Sleep(100);
            }
            return null;
        }

        /// <summary>
        /// Waits for an element with the given AutomationId anywhere in the UIA
        /// tree, retrying until the timeout. UIA trees for WPF windows update
        /// asynchronously, so direct-poll is more reliable than a single find.
        /// </summary>
        public UiElementInfo? WaitForElement(string automationId, TimeSpan timeout)
        {
            var element = WaitForElementRaw(automationId, timeout);
            return element == null ? null : ToInfo(element, automationId);
        }

        private AutomationElement? WaitForElementRaw(string automationId, TimeSpan timeout)
        {
            var desktop = GetDesktop();
            var deadline = DateTime.UtcNow + timeout;

            while (true)
            {
                var element = desktop.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
                if (element != null)
                {
                    return element;
                }

                if (DateTime.UtcNow >= deadline)
                {
                    return null;
                }

                Thread.Sleep(120);
            }
        }

        public UiElementInfo? FindElement(string automationId)
        {
            var desktop = GetDesktop();
            var element = desktop.FindFirstDescendant(cf => cf.ByAutomationId(automationId));
            return element == null ? null : ToInfo(element, automationId);
        }

        /// <summary>
        /// Clicks the center of the element's bounding rectangle via FlaUI Mouse
        /// (real SendInput). Coordinates come from UIA bounds, never hardcoded
        /// pixels, which keeps clicks DPI-safe.
        /// </summary>
        public void ClickElement(string automationId, TimeSpan timeout)
        {
            var info = WaitForElement(automationId, timeout)
                ?? throw new UiDriverException($"Click failed: element '{automationId}' not found within {timeout.TotalSeconds:F1}s.");

            FlaUI.Core.Input.Mouse.MoveTo(
                (int)Math.Round(info.Bounds.X + info.Bounds.Width / 2),
                (int)Math.Round(info.Bounds.Y + info.Bounds.Height / 2));
            FlaUI.Core.Input.Mouse.Click();
        }

        /// <summary>
        /// Scrolls the element with <paramref name="automationId"/> (or its first
        /// scrollable descendant) to the end/start via UIA ScrollPattern. WPF
        /// ScrollViewers expose the pattern on their own peer, but the lookup walks
        /// descendants too so a composite host works as the anchor.
        /// </summary>
        public void Scroll(string automationId, bool toBottom, TimeSpan timeout)
        {
            var element = WaitForElementRaw(automationId, timeout)
                ?? throw new UiDriverException($"Scroll failed: element '{automationId}' not found within {timeout.TotalSeconds:F1}s.");

            var scrollable = element.Patterns.Scroll.IsSupported
                ? element
                : element.FindAllDescendants().FirstOrDefault(d => d.Patterns.Scroll.IsSupported);

            if (scrollable == null)
            {
                throw new UiDriverException(
                    $"Scroll failed: element '{automationId}' (type {element.ControlType}) exposes no UIA ScrollPattern.");
            }

            var pattern = scrollable.Patterns.Scroll.Pattern;
            var vertical = toBottom ? 100.0 : 0.0;
            try
            {
                // -1 = "leave this axis unchanged"; some providers reject it, so
                // fall back to the axis' actual current percent.
                pattern.SetScrollPercent(-1, vertical);
            }
            catch (Exception)
            {
                var horizontal = pattern.HorizontallyScrollable.ValueOrDefault
                    ? pattern.HorizontalScrollPercent.ValueOrDefault
                    : -1;
                pattern.SetScrollPercent(horizontal, vertical);
            }

            // Give WPF a moment to re-layout before the next step captures.
            Thread.Sleep(250);
        }

        /// <summary>Dumps the UIA control-view tree (id / name / type / bounds / enabled).</summary>
        public string DumpTree(int maxDepth = 12)
        {
            var sb = new StringBuilder();
            var desktop = GetDesktop();
            var root = desktop.FindAllChildren();

            foreach (var child in root)
            {
                DumpElement(child, 0, maxDepth, sb);
            }
            return sb.ToString();
        }

        private void DumpElement(AutomationElement element, int depth, int maxDepth, StringBuilder sb)
        {
            if (depth > maxDepth)
            {
                return;
            }

            try
            {
                var indent = new string(' ', depth * 2);
                var bounds = element.BoundingRectangle;
                sb.AppendLine(
                    $"{indent}id='{element.AutomationId}' name='{Truncate(element.Name, 60)}' type={element.ControlType} " +
                    $"enabled={element.IsEnabled} bounds=({bounds.X:F0},{bounds.Y:F0},{bounds.Width:F0}x{bounds.Height:F0})");

                foreach (var child in element.FindAllChildren())
                {
                    DumpElement(child, depth + 1, maxDepth, sb);
                }
            }
            catch (Exception ex)
            {
                sb.AppendLine(new string(' ', depth * 2) + $"[error dumping element: {ex.Message}]");
            }
        }

        private static UiElementInfo ToInfo(AutomationElement element, string automationId)
        {
            var bounds = element.BoundingRectangle;
            return new UiElementInfo
            {
                AutomationId = element.AutomationId ?? automationId,
                Name = element.Name ?? string.Empty,
                ControlType = element.ControlType.ToString(),
                IsEnabled = element.IsEnabled,
                IsOffscreen = element.Properties.IsOffscreen.ValueOrDefault,
                Bounds = new UiElementBounds(bounds.X, bounds.Y, bounds.Width, bounds.Height)
            };
        }

        private static string Truncate(string? text, int max)
        {
            if (string.IsNullOrEmpty(text))
            {
                return string.Empty;
            }
            return text.Length <= max ? text : text[..max] + "…";
        }

        public void Dispose()
        {
            _automation.Dispose();
        }
    }

    /// <summary>UIA driver failure with a diagnostic message.</summary>
    public sealed class UiDriverException : Exception
    {
        public UiDriverException(string message) : base(message) { }
    }
}
