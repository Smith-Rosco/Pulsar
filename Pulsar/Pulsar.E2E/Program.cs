// [Path]: Pulsar/Pulsar.E2E/Program.cs

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.E2E.AI;
using Pulsar.E2E.Occlusion;
using Pulsar.E2E.Runner;
using Pulsar.E2E.Workflow;

namespace Pulsar.E2E
{
    /// <summary>
    /// Pulsar.E2E — external driver for Pulsar debug instances.
    ///
    /// Commands:
    ///   run --workflow &lt;path&gt; [--app &lt;exe&gt;] [--fixture &lt;json&gt;]
    ///            [--artifacts &lt;dir&gt;] [--run-id &lt;id&gt;]
    ///   iterate --workflow &lt;path&gt; --max-iterations &lt;N&gt; --base-url &lt;url&gt; --model &lt;id&gt; [--api-key &lt;k&gt;]
    ///   occlusion --workflow &lt;path&gt; [--view &lt;name&gt;] [--baseline &lt;path&gt;] [--app &lt;exe&gt;] [--fixture &lt;json&gt;]
    /// </summary>
    public static class Program
    {
        public static int Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            Console.WriteLine("Pulsar.E2E — deterministic UIA driver for Pulsar debug instances");

            try
            {
                if (args.Length == 0)
                {
                    PrintUsage();
                    return 2;
                }

                var command = args[0].ToLowerInvariant();
                var rest = args.Skip(1).ToArray();

                return command switch
                {
                    "run" => RunCommand(rest),
                    "iterate" => IterateCommandAsync(rest).GetAwaiter().GetResult(),
                    "occlusion" => OcclusionCommandAsync(rest).GetAwaiter().GetResult(),
                    "list-steps" => ListSteps(),
                    "rec-test" => RunRecorderSelfTest(rest.Length > 0 ? rest[0] : null),
                    "--help" or "-h" or "help" => PrintUsage() * 0,
                    _ => UnknownCommand(command)
                };
            }
            catch (WorkflowParseException ex)
            {
                Console.Error.WriteLine($"[workflow] {ex.Message}");
                return 2;
            }
            catch (Exception ex)
            {
                Console.Error.WriteLine($"[fatal] {ex}");
                return 1;
            }
        }

        private static int RunCommand(string[] args)
        {
            var options = new RunOptions();
            string? workflowPath = null;

            for (int i = 0; i < args.Length; i += 2)
            {
                switch (GetOption(args, i, "--workflow", "--app", "--fixture", "--artifacts", "--run-id", "--app-args"))
                {
                    case "--workflow": workflowPath = args[i + 1]; break;
                    case "--app": options.AppExePath = args[i + 1]; break;
                    case "--fixture": options.FixturePath = args[i + 1]; break;
                    case "--artifacts": options.ArtifactsRoot = args[i + 1]; break;
                    case "--run-id": options.RunId = args[i + 1]; break;
                    case "--app-args": options.AppArguments = args[i + 1]; break;
                }
            }

            ValidateRequired(workflowPath, "--workflow");
            ValidateFile(workflowPath!, "workflow");

            var workflow = WorkflowParser.ParseFile(workflowPath!);
            Console.WriteLine($"Workflow '{workflow.Name}' with {workflow.Steps.Count} steps.");

            var runner = new WorkflowRunner();
            var result = runner.Execute(workflow, workflowPath!, options);

            Console.WriteLine(result.Success
                ? $"PASS (run {result.RunId}, {result.Duration.TotalSeconds:F1}s)"
                : $"FAIL at step '{result.FailedStepId}' (run {result.RunId}): {result.FailureMessage}");

            return result.Success ? 0 : 1;
        }

        private static async Task<int> IterateCommandAsync(string[] args)
        {
            var options = new IterateOptions
            {
                Run = new RunOptions(),
                Llm = IterateOptions.FromEnvironment()
            };
            string? workflowPath = null;

            for (int i = 0; i < args.Length; i += 2)
            {
                switch (GetOption(args, i, "--workflow", "--max-iterations", "--base-url", "--model",
                    "--api-key", "--workspace", "--artifacts", "--app", "--fixture"))
                {
                    case "--workflow": workflowPath = args[i + 1]; break;
                    case "--max-iterations": options.MaxIterations = ParsePositiveInt(args[i + 1], "--max-iterations"); break;
                    case "--base-url": options.Llm.BaseUrl = args[i + 1]; break;
                    case "--model": options.Llm.Model = args[i + 1]; break;
                    case "--api-key": options.Llm.ApiKey = args[i + 1]; break;
                    case "--workspace": options.WorkspaceRoot = Path.GetFullPath(args[i + 1]); break;
                    case "--artifacts": options.Run.ArtifactsRoot = args[i + 1]; break;
                    case "--app": options.Run.AppExePath = args[i + 1]; break;
                    case "--fixture": options.Run.FixturePath = args[i + 1]; break;
                }
            }

            ValidateRequired(workflowPath, "--workflow");
            ValidateFile(workflowPath!, "workflow");
            options.WorkflowPath = workflowPath!;

            var loop = new IterationLoop(options);
            var success = await loop.RunAsync().ConfigureAwait(false);
            return success ? 0 : 1;
        }

        private static async Task<int> OcclusionCommandAsync(string[] args)
        {
            string? workflowPath = null, appPath = null, fixturePath = null, view = "default", baseline = null;
            var llm = IterateOptions.FromEnvironment();

            for (int i = 0; i < args.Length; i += 2)
            {
                switch (GetOption(args, i, "--workflow", "--app", "--fixture", "--view", "--baseline", "--base-url", "--model", "--api-key"))
                {
                    case "--workflow": workflowPath = args[i + 1]; break;
                    case "--app": appPath = args[i + 1]; break;
                    case "--fixture": fixturePath = args[i + 1]; break;
                    case "--view": view = args[i + 1]; break;
                    case "--baseline": baseline = args[i + 1]; break;
                    case "--base-url": llm.BaseUrl = args[i + 1]; break;
                    case "--model": llm.Model = args[i + 1]; break;
                    case "--api-key": llm.ApiKey = args[i + 1]; break;
                }
            }

            ValidateRequired(workflowPath, "--workflow");
            ValidateFile(workflowPath!, "workflow");

            var workflow = WorkflowParser.ParseFile(workflowPath!);
            var runOptions = new RunOptions { AppExePath = appPath, FixturePath = fixturePath };

            // Minimal launch+settle sequence, then capture the overlay.
            using var uia = new Driver.UiaDriver();
            using var stateClient = new Driver.StateClient();
            var launched = new Driver.AppLauncher().Launch(
                FirstNonEmpty(appPath, workflow.App.ExePath),
                FirstNonEmpty(fixturePath, workflow.App.Fixture),
                workflow.App.Arguments,
                msg => Console.WriteLine(msg));
            stateClient.Start(launched.Process.Id);
            uia.Attach(launched.Process.Id);

            try
            {
                var artifactsRoot = runOptions.ArtifactsRoot;
                var runId = runOptions.RunId;
                Directory.CreateDirectory(Path.Combine(artifactsRoot, runId, "occlusion"));

                var analyzer = new OcclusionAnalyzer(uia, llm.IsValid ? llm : null);
                var (spec, imagePath) = analyzer.CaptureStableOverlay(
                    Path.Combine(artifactsRoot, runId, "occlusion", "overlay.png"),
                    new[] { "Pulsar.RadialMenuWindow", "Pulsar.Slot.0", "Pulsar.Slot.1", "Pulsar.Slot.2", "Pulsar.Slot.3" });

                File.WriteAllText(
                    Path.Combine(artifactsRoot, runId, "occlusion", "overlay.json"),
                    JsonSerializer.Serialize(spec, new JsonSerializerOptions { WriteIndented = true }));

                var report = await analyzer.AnalyzeAsync(spec, imagePath, view).ConfigureAwait(false);
                var reportPath = Path.Combine(artifactsRoot, runId, "occlusion", "report.json");
                File.WriteAllText(reportPath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                Console.WriteLine($"Occlusion report: {reportPath}");

                var baselinePath = baseline ?? Path.Combine(artifactsRoot, "baselines", $"{view}.occlusion.json");
                var passed = OcclusionAnalyzer.DiffAgainstBaseline(report, baselinePath, msg => Console.WriteLine(msg));
                Console.WriteLine(passed ? "Occlusion check PASS" : "Occlusion check FAIL (visual regression gate)");
                return passed ? 0 : 1;
            }
            finally
            {
                Driver.AppLauncher.Stop(launched, msg => Console.WriteLine(msg));
            }
        }

        private static int ListSteps()
        {
            Console.WriteLine("Supported workflow step types:");
            foreach (var name in new[] { "launch", "wait", "waitForState", "hotkey", "menu-open", "menu-close", "click", "assert", "screenshot", "record", "exit" })
            {
                Console.WriteLine($"  {name}");
            }
            return 0;
        }

        /// <summary>
        /// Isolates the ScreenRecorderLib pipeline: record 3s, stop, report which
        /// events fired and whether the MP4 got any bytes. Used to diagnose
        /// 'recording never finalizes / 0-byte file' environments.
        /// </summary>
        private static int RunRecorderSelfTest(string? outputPath)
        {
            var path = string.IsNullOrWhiteSpace(outputPath)
                ? Path.Combine(Path.GetTempPath(), "pulsar-rec-test.mp4")
                : Path.GetFullPath(outputPath);
            var parent = Path.GetDirectoryName(path);
            if (!string.IsNullOrWhiteSpace(parent))
            {
                Directory.CreateDirectory(parent);
            }
            if (File.Exists(path))
            {
                File.Delete(path);
            }
            var outcome = new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);
            var recorder = ScreenRecorderLib.Recorder.CreateRecorder(new ScreenRecorderLib.RecorderOptions
            {
                OutputOptions = new ScreenRecorderLib.OutputOptions { RecorderMode = ScreenRecorderLib.RecorderMode.Video },
                VideoEncoderOptions = new ScreenRecorderLib.VideoEncoderOptions
                {
                    Framerate = 15,
                    Encoder = new ScreenRecorderLib.H264VideoEncoder
                    {
                        BitrateMode = ScreenRecorderLib.H264BitrateControlMode.Quality
                    }
                }
            });
            recorder.OnRecordingComplete += (s, e) =>
            {
                Console.WriteLine($"[rec-test] OnRecordingComplete: {e.FilePath}");
                outcome.TrySetResult("complete");
            };
            recorder.OnRecordingFailed += (s, e) =>
            {
                Console.WriteLine($"[rec-test] OnRecordingFailed: {e.Error}");
                outcome.TrySetResult("failed");
            };

            recorder.Record(path);
            Console.WriteLine($"[rec-test] recording to {path} for 3s...");
            Thread.Sleep(3000);
            Console.WriteLine("[rec-test] calling Stop()");
            recorder.Stop();

            Task.WhenAny(outcome.Task, Task.Delay(15000)).Wait();
            var size = File.Exists(path) ? new FileInfo(path).Length : -1;
            Console.WriteLine($"[rec-test] event='{(outcome.Task.IsCompleted ? outcome.Task.Result : "NONE within 15s")}', fileSize={size}");
            recorder.Dispose();

            return size > 0 && outcome.Task.Status == TaskStatus.RanToCompletion ? 0 : 1;
        }

        private static int UnknownCommand(string command)
        {
            Console.Error.WriteLine($"Unknown command '{command}'.");
            PrintUsage();
            return 2;
        }

        private static string GetOption(string[] args, int index, params string[] names)
        {
            var value = args[index];
            if (!names.Contains(value))
            {
                return string.Empty;
            }

            if (index + 1 >= args.Length)
            {
                throw new ArgumentException($"Option '{value}' requires a value.");
            }

            return value;
        }

        private static void ValidateRequired(string? value, string optionName)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                throw new ArgumentException($"Missing required option {optionName}.");
            }
        }

        private static void ValidateFile(string path, string description)
        {
            if (!File.Exists(path))
            {
                throw new FileNotFoundException($"{description} file not found: '{path}'");
            }
        }

        private static int ParsePositiveInt(string value, string optionName)
        {
            if (!int.TryParse(value, out var parsed) || parsed <= 0)
            {
                throw new ArgumentException($"Option {optionName} must be a positive integer, got '{value}'.");
            }
            return parsed;
        }

        private static string FirstNonEmpty(params string?[] values)
        {
            return values.FirstOrDefault(v => !string.IsNullOrWhiteSpace(v)) ?? string.Empty;
        }

        private static int PrintUsage()
        {
            Console.WriteLine(@"
Usage:
  Pulsar.E2E run --workflow <path> [--app <exe>] [--fixture <json>] [--artifacts <dir>] [--run-id <id>] [--app-args <args>]
  Pulsar.E2E iterate --workflow <path> --max-iterations <N> --base-url <url> --model <id> [--api-key <key>] [--workspace <dir>]
  Pulsar.E2E occlusion --workflow <path> [--view <name>] [--baseline <path>] [--app <exe>] [--fixture <json>]
  Pulsar.E2E list-steps

LLM env vars (used by iterate/occlusion when flags absent):
  PULSAR_E2E_LLM_BASE_URL, PULSAR_E2E_LLM_MODEL, PULSAR_E2E_LLM_API_KEY
");
            return 0;
        }
    }
}
