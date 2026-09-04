// [Path]: Pulsar/Pulsar.E2E/Runner/WorkflowRunner.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Pulsar.E2E.Diagnostics;
using Pulsar.E2E.Driver;
using Pulsar.E2E.Preflight;
using Pulsar.E2E.Workflow;

namespace Pulsar.E2E.Runner
{
    /// <summary>The result of one workflow execution.</summary>
    public sealed class RunResult
    {
        public bool Success { get; init; }
        public string? FailedStepId { get; init; }
        public string? FailureMessage { get; init; }
        public string DiagnosticPackageDir { get; init; } = string.Empty;
        public string RunId { get; init; } = string.Empty;
        public TimeSpan Duration { get; init; }
        public List<string> ExecutedStepIds { get; } = new();
    }

    /// <summary>
    /// Executes a workflow against a Pulsar debug instance. On any step failure the
    /// run aborts and emits the standard diagnostic package — the single contract
    /// between the deterministic framework and the visual AI iteration loop.
    /// </summary>
    public sealed class WorkflowRunner
    {
        private readonly Action<string> _log;

        public WorkflowRunner(Action<string>? log = null)
        {
            _log = log ?? (msg => Console.WriteLine(msg));
        }

        public RunResult Execute(
            WorkflowDefinition workflow,
            string workflowPath,
            RunOptions options)
        {
            // Pre-flight: real SendInput needs an interactive desktop. Abort cleanly
            // with a diagnostic instead of confusing per-step timeouts.
            var preflight = InteractiveDesktopCheck.Verify();
            if (!preflight.Ok)
            {
                _log("[pre-flight] " + preflight.Diagnostic);
                return new RunResult
                {
                    Success = false,
                    FailureMessage = preflight.Diagnostic,
                    RunId = options.RunId
                };
            }

            var runId = options.RunId;
            var artifactsRoot = options.ArtifactsRoot;
            Directory.CreateDirectory(artifactsRoot);

            using var stateClient = new StateClient();
            using var uia = new UiaDriver();
            using var recorder = new ScreenRecorder();

            var stopwatch = Stopwatch.StartNew();
            string? failedStepId = null;
            string? failureMessage = null;

            try
            {
                foreach (var step in workflow.Steps)
                {
                    _log($"[step:{step.Id}] {step.TypeRaw}");
                    ExecuteStep(step, workflow, options, stateClient, uia, recorder, artifactsRoot, runId);
                }
            }
            catch (StepFailureException ex)
            {
                failedStepId = ex.StepId;
                failureMessage = ex.Message;
                EmitDiagnosticPackage(ex.StepId, ex.StepType, ex.Message, ex.Details, workflow, workflowPath,
                    runId, artifactsRoot, stateClient, uia, recorder, options);
            }
            catch (Exception ex)
            {
                failedStepId = "unknown";
                failureMessage = ex.ToString();
                EmitDiagnosticPackage("unknown", "unknown", ex.ToString(), null, workflow, workflowPath,
                    runId, artifactsRoot, stateClient, uia, recorder, options);
            }
            finally
            {
                TryStopRecording(recorder, artifactsRoot, runId, failedStepId);
                AppLauncher.Stop(_launched, _log);
                stopwatch.Stop();
            }

            return new RunResult
            {
                Success = failedStepId == null,
                FailedStepId = failedStepId,
                FailureMessage = failureMessage,
                RunId = runId,
                DiagnosticPackageDir = failedStepId == null
                    ? string.Empty
                    : DiagnosticPackageWriter.GetStepDirectory(artifactsRoot, runId, failedStepId),
                Duration = stopwatch.Elapsed
            };
        }

        private AppLauncher.LaunchedApp? _launched;

        private void ExecuteStep(
            WorkflowStep step,
            WorkflowDefinition workflow,
            RunOptions options,
            StateClient stateClient,
            UiaDriver uia,
            ScreenRecorder recorder,
            string artifactsRoot,
            string runId)
        {
            switch (step.Type)
            {
                case StepType.Launch:
                {
                    var exePath = FirstNonEmpty(options.AppExePath, workflow.App.ExePath);
                    var fixture = FirstNonEmpty(options.FixturePath, workflow.App.Fixture);
                    var args = FirstNonEmpty(options.AppArguments, workflow.App.Arguments);

                    _launched = new AppLauncher().Launch(exePath, fixture, args, _log);
                    stateClient.Start(_launched.Process.Id);
                    uia.Attach(_launched.Process.Id);
                    break;
                }

                case StepType.Wait:
                    Task.Delay(TimeSpan.FromMilliseconds(step.DurationMs)).Wait();
                    break;

                case StepType.WaitForState:
                {
                    var wait = stateClient.WaitForEventAsync(step.Event, TimeSpan.FromMilliseconds(step.TimeoutMs))
                        .GetAwaiter().GetResult();
                    if (!wait.Success)
                    {
                        var observed = JsonSerializer.Serialize(wait.Observed.Select(e => e.Event).ToArray());
                        throw new StepFailureException(step.Id, step.TypeRaw,
                            $"waitForState timed out after {step.TimeoutMs}ms waiting for '{step.Event}'. Events observed: {observed}",
                            JsonSerializer.SerializeToElement(new { timeoutMs = step.TimeoutMs, expectedEvent = step.Event, observedEvents = wait.Observed.Select(e => new { e.Event, e.TimestampUtc }) }));
                    }
                    break;
                }

                case StepType.Hotkey:
                    InputDriver.SendHotkey(step.Keys);
                    break;

                case StepType.MenuOpen:
                case StepType.MenuClose:
                {
                    if (_launched == null)
                    {
                        throw new StepFailureException(step.Id, step.TypeRaw,
                            $"{step.TypeRaw} step requires a prior 'launch' step (no debug process is attached).", null);
                    }

                    try
                    {
                        if (step.Type == StepType.MenuOpen)
                        {
                            var mode = step.Mode.Equals("task", StringComparison.OrdinalIgnoreCase) ? "task" : "action";
                            CommandClient.Send(_launched.Process.Id, "menu-open", mode);
                        }
                        else
                        {
                            CommandClient.Send(_launched.Process.Id, "menu-close");
                        }
                    }
                    catch (Exception ex)
                    {
                        throw new StepFailureException(step.Id, step.TypeRaw,
                            $"Failed to send '{step.TypeRaw}' command to pipe Pulsar.Debug.{_launched.Process.Id}.cmd: {ex.Message}", null);
                    }
                    break;
                }

                case StepType.Click:
                    try
                    {
                        uia.ClickElement(step.AutomationId, TimeSpan.FromMilliseconds(step.TimeoutMs));
                    }
                    catch (UiDriverException ex)
                    {
                        throw new StepFailureException(step.Id, step.TypeRaw, ex.Message, null);
                    }
                    break;

                case StepType.Assert:
                {
                    var element = uia.WaitForElement(step.AutomationId, TimeSpan.FromMilliseconds(step.TimeoutMs));
                    if (element == null)
                    {
                        throw new StepFailureException(step.Id, step.TypeRaw,
                            $"assert failed: element with AutomationId '{step.AutomationId}' not found within {step.TimeoutMs}ms.",
                            JsonSerializer.SerializeToElement(new { automationId = step.AutomationId, expected = step.Expected }));
                    }

                    if (step.Expected.Equals("visible", StringComparison.OrdinalIgnoreCase) && element.IsOffscreen)
                    {
                        throw new StepFailureException(step.Id, step.TypeRaw,
                            $"assert failed: element '{step.AutomationId}' found but reported offscreen.",
                            JsonSerializer.SerializeToElement(element));
                    }
                    break;
                }

                case StepType.Screenshot:
                {
                    var fileName = string.IsNullOrWhiteSpace(step.File)
                        ? $"{step.Id}.png"
                        : step.File;
                    var path = Path.Combine(artifactsRoot, runId, fileName);
                    Capture.CaptureScreen(path);
                    _log($"[step:{step.Id}] screenshot -> {path}");
                    break;
                }

                case StepType.Record:
                    if (step.Action.Equals("stop", StringComparison.OrdinalIgnoreCase))
                    {
                        try
                        {
                            StopRecording(recorder, artifactsRoot, runId, step.Id);
                        }
                        catch (Exception ex) when (ex is InvalidOperationException or IOException)
                        {
                            throw new StepFailureException(step.Id, step.TypeRaw,
                                $"record stop failed: {ex.Message}", null);
                        }
                    }
                    else
                    {
                        recorder.Start(Path.Combine(artifactsRoot, runId, $"{runId}-recording.mp4"));
                    }
                    break;

                case StepType.Exit:
                    AppLauncher.Stop(_launched, _log);
                    _launched = null;
                    break;

                default:
                    throw new StepFailureException(step.Id, step.TypeRaw, $"Unsupported step type '{step.TypeRaw}'.", null);
            }
        }

        private void TryStopRecording(ScreenRecorder recorder, string artifactsRoot, string runId, string? stepId)
        {
            try
            {
                StopRecording(recorder, artifactsRoot, runId, stepId ?? "recording");
            }
            catch (InvalidOperationException)
            {
                // No active recording — nothing to do.
            }
        }

        private void StopRecording(ScreenRecorder recorder, string artifactsRoot, string runId, string stepId)
        {
            var path = recorder.Stop();
            _log($"[record:{stepId}] saved {path}");
            if (stepId != "recording")
            {
                // Attach the clip to this step's diagnostics when it fails later.
                DiagnosticPackageWriter.WriteVideo(
                    DiagnosticPackageWriter.GetStepDirectory(artifactsRoot, runId, stepId), path);
            }
        }

        private void EmitDiagnosticPackage(
            string stepId,
            string stepType,
            string message,
            JsonElement? details,
            WorkflowDefinition workflow,
            string workflowPath,
            string runId,
            string artifactsRoot,
            StateClient stateClient,
            UiaDriver uia,
            ScreenRecorder recorder,
            RunOptions options)
        {
            _log($"[diagnostics] Emitting diagnostic package for failed step '{stepId}'");
            try
            {
                var stepDir = DiagnosticPackageWriter.GetStepDirectory(artifactsRoot, runId, stepId);

                DiagnosticPackageWriter.WriteFailureJson(stepDir, new FailureReport
                {
                    RunId = runId,
                    Workflow = workflow.Name ?? Path.GetFileName(workflowPath),
                    StepId = stepId,
                    StepType = stepType,
                    Message = message,
                    FailedAtUtc = DateTime.UtcNow,
                    Details = details
                });

                // UIA tree dump — the structural ground truth at failure time.
                try
                {
                    DiagnosticPackageWriter.WriteUiaTree(stepDir, uia.DumpTree());
                }
                catch (Exception ex)
                {
                    _log($"[diagnostics] UIA tree dump failed: {ex.Message}");
                }

                // Screenshot at failure moment (screen-level, includes popups).
                try
                {
                    var shotPath = Path.Combine(artifactsRoot, runId, $"{stepId}-failure.png");
                    Capture.CaptureScreen(shotPath);
                    DiagnosticPackageWriter.WriteScreenshot(stepDir, shotPath);
                }
                catch (Exception ex)
                {
                    _log($"[diagnostics] Screenshot failed: {ex.Message}");
                }

                // Recording clip (best-effort; only when a recording was active).
                try
                {
                    var clip = recorder.Stop();
                    DiagnosticPackageWriter.WriteVideo(stepDir, clip);
                }
                catch
                {
                    // no active recording — fine.
                }

                // Log excerpt from the debug instance.
                var configDir = _launched?.ConfigDirectory;
                if (!string.IsNullOrEmpty(configDir))
                {
                    DiagnosticPackageWriter.WriteLogExcerpt(stepDir, configDir);
                }

                _log($"[diagnostics] Package written to {stepDir}");
            }
            catch (Exception ex)
            {
                _log($"[diagnostics] Failed to emit diagnostic package: {ex.Message}");
            }
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            foreach (var value in values)
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }
            return string.Empty;
        }
    }

    /// <summary>CLI/runtime options for one workflow execution.</summary>
    public sealed class RunOptions
    {
        public string RunId { get; set; } = DateTime.UtcNow.ToString("yyyyMMdd-HHmmss", System.Globalization.CultureInfo.InvariantCulture);
        public string ArtifactsRoot { get; set; } = "artifacts";
        public string? AppExePath { get; set; }
        public string? FixturePath { get; set; }
        public string? AppArguments { get; set; }
    }

    /// <summary>A step-level failure carrying its identity for diagnostics.</summary>
    internal sealed class StepFailureException : Exception
    {
        public string StepId { get; }
        public string StepType { get; }
        public JsonElement? Details { get; }

        public StepFailureException(string stepId, string stepType, string message, JsonElement? details)
            : base(message)
        {
            StepId = stepId;
            StepType = stepType;
            Details = details;
        }
    }
}
