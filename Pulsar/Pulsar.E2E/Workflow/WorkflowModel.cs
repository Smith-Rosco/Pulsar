// [Path]: Pulsar/Pulsar.E2E/Workflow/WorkflowModel.cs

using System;
using System.Collections.Generic;

namespace Pulsar.E2E.Workflow
{
    /// <summary>All supported workflow step types.</summary>
    public enum StepType
    {
        Launch,
        Wait,
        WaitForState,
        Hotkey,
        MenuOpen,
        MenuClose,
        Click,
        Assert,
        Screenshot,
        Record,
        Exit
    }

    /// <summary>
    /// The application-under-test launch configuration embedded in a workflow.
    /// </summary>
    public sealed class AppLaunchConfig
    {
        /// <summary>Path to Pulsar.exe. May be empty when supplied via CLI.</summary>
        public string ExePath { get; set; } = string.Empty;

        /// <summary>
        /// Optional fixture Profiles.json copied into the debug config directory
        /// before launch, so the debug instance starts from a predetermined state.
        /// </summary>
        public string? Fixture { get; set; }

        /// <summary>Extra arguments appended after --ui-debug.</summary>
        public string Arguments { get; set; } = string.Empty;
    }

    /// <summary>One workflow step. Fields are interpreted per <see cref="StepType"/>.</summary>
    public sealed class WorkflowStep
    {
        public StepType Type { get; set; }
        public string TypeRaw { get; set; } = string.Empty;

        /// <summary>Stable step id; used for artifact directories and diagnostics.</summary>
        public string Id { get; set; } = string.Empty;

        /// <summary>wait: sleep duration; waitForState/assert/click/hotkey: timeout.</summary>
        public int DurationMs { get; set; }
        public int TimeoutMs { get; set; } = 5000;

        /// <summary>assert/click: AutomationId of the target element.</summary>
        public string AutomationId { get; set; } = string.Empty;

        /// <summary>waitForState: pipe event name (menu-opened, slot-activated, ...).</summary>
        public string Event { get; set; } = string.Empty;

        /// <summary>hotkey: chord like "Ctrl+Space" or "Alt+P".</summary>
        public string Keys { get; set; } = string.Empty;

        /// <summary>menu-open: "action" (default) or "task".</summary>
        public string Mode { get; set; } = string.Empty;

        /// <summary>assert: expected state (exists / visible).</summary>
        public string Expected { get; set; } = "exists";

        /// <summary>screenshot: output file name (relative to the artifacts dir).</summary>
        public string File { get; set; } = string.Empty;

        /// <summary>record: whether the step starts or stops recording.</summary>
        public string Action { get; set; } = string.Empty;
    }

    /// <summary>A parsed JSON workflow definition.</summary>
    public sealed class WorkflowDefinition
    {
        public string Name { get; set; } = string.Empty;
        public AppLaunchConfig App { get; set; } = new();
        public List<WorkflowStep> Steps { get; set; } = new();
    }
}
