// [Path]: Pulsar/Pulsar.E2E/AI/IterationLoop.cs

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Pulsar.E2E.Runner;
using Pulsar.E2E.Workflow;

namespace Pulsar.E2E.AI
{
    /// <summary>CLI options for the `iterate` command.</summary>
    public sealed class IterateOptions
    {
        public string WorkflowPath { get; set; } = string.Empty;
        public int MaxIterations { get; set; } = 3;
        public LlmConfig Llm { get; set; } = new();
        public RunOptions Run { get; set; } = new();

        /// <summary>
        /// Workspace root against which AI-proposed file paths are resolved and
        /// `dotnet build` runs (defaults to the current directory).
        /// </summary>
        public string WorkspaceRoot { get; set; } = Directory.GetCurrentDirectory();

        /// <summary>Optional pre-populated LLM config from env vars.</summary>
        public static LlmConfig FromEnvironment()
        {
            return new LlmConfig
            {
                BaseUrl = Environment.GetEnvironmentVariable("PULSAR_E2E_LLM_BASE_URL") ?? string.Empty,
                ApiKey = Environment.GetEnvironmentVariable("PULSAR_E2E_LLM_API_KEY") ?? string.Empty,
                Model = Environment.GetEnvironmentVariable("PULSAR_E2E_LLM_MODEL") ?? string.Empty
            };
        }
    }

    /// <summary>
    /// The visual AI iteration loop (design D7): run the workflow → on failure emit
    /// the diagnostic package → hand it to the configured LLM (image+text) → apply
    /// the proposed patch → rebuild → re-run the SAME workflow. Convergence is
    /// judged solely by the workflow result; the AI never self-certifies.
    ///
    /// Safety rails: max-iteration cap, and every iteration's diagnostics are
    /// preserved under its own run-id so a hallucinated patch is reviewable by a
    /// human offline.
    /// </summary>
    public sealed class IterationLoop
    {
        private readonly IterateOptions _options;
        private readonly Action<string> _log;

        public IterationLoop(IterateOptions options, Action<string>? log = null)
        {
            _options = options;
            _log = log ?? (msg => Console.WriteLine(msg));
        }

        public async Task<bool> RunAsync()
        {
            var workflow = WorkflowParser.ParseFile(_options.WorkflowPath);
            var runner = new WorkflowRunner(_log);

            for (int iteration = 1; iteration <= _options.MaxIterations; iteration++)
            {
                _options.Run.RunId = $"iter{iteration:D3}-{DateTime.UtcNow:yyyyMMdd-HHmmss}";
                _log($"=== Iteration {iteration}/{_options.MaxIterations} (run {_options.Run.RunId}) ===");

                var result = runner.Execute(workflow, _options.WorkflowPath, _options.Run);
                if (result.Success)
                {
                    _log($"=== Workflow PASSED on iteration {iteration}. ===");
                    return true;
                }

                _log($"=== Iteration {iteration} FAILED at step '{result.FailedStepId}': {result.FailureMessage} ===");

                if (iteration == _options.MaxIterations)
                {
                    _log("=== Max iterations reached; giving up. ===");
                    _log($"Last diagnostic package preserved at: {result.DiagnosticPackageDir}");
                    return false;
                }

                // Ask the AI for a fix based ONLY on the diagnostic package.
                var packageDir = result.DiagnosticPackageDir;
                var proposal = await RequestFixAsync(packageDir).ConfigureAwait(false);
                if (proposal == null)
                {
                    _log("AI could not produce a fix proposal; stopping.");
                    _log($"Last diagnostic package preserved at: {packageDir}");
                    return false;
                }

                ApplyProposal(proposal);
                Rebuild();
            }

            return false;
        }

        private async Task<FixProposal?> RequestFixAsync(string packageDir)
        {
            if (!_options.Llm.IsValid)
            {
                _log("LLM provider is not configured. Set PULSAR_E2E_LLM_BASE_URL, PULSAR_E2E_LLM_MODEL (and optionally PULSAR_E2E_LLM_API_KEY).");
                return null;
            }

            var failureJson = Path.Combine(packageDir, "failure.json");
            var uiaTree = Path.Combine(packageDir, "uia-tree.txt");
            var logExcerpt = Path.Combine(packageDir, "logs", "excerpt.log");
            var screenshot = Path.Combine(packageDir, "screenshot.png");

            if (!File.Exists(failureJson))
            {
                _log($"No failure.json in diagnostic package '{packageDir}'; nothing to analyze.");
                return null;
            }

            var userText = new System.Text.StringBuilder();
            userText.AppendLine("A Pulsar E2E workflow step failed. Diagnose the root cause from the diagnostic package and propose a fix.");
            userText.AppendLine("Reply with ONLY a JSON object: {\"reasoning\": \"...\", \"patches\": [{\"file\": \"relative/path\", \"content\": \"full new file content\"}]}");
            userText.AppendLine();
            userText.AppendLine("=== failure.json ===");
            userText.AppendLine(File.ReadAllText(failureJson));

            if (File.Exists(uiaTree))
            {
                userText.AppendLine();
                userText.AppendLine("=== uia-tree.txt (UIA tree at failure) ===");
                userText.AppendLine(File.ReadAllText(uiaTree));
            }

            if (File.Exists(logExcerpt))
            {
                userText.AppendLine();
                userText.AppendLine("=== logs/excerpt.log ===");
                userText.AppendLine(File.ReadAllText(logExcerpt));
            }

            var images = File.Exists(screenshot) ? new[] { screenshot } : Array.Empty<string>();

            const string systemPrompt =
                "You are a senior WPF engineer fixing UI regressions in the Pulsar radial menu application. " +
                "You receive E2E diagnostic packages (failure report, UIA tree, log excerpt, screenshot) and must " +
                "propose minimal patches to XAML or C# files. Respect project rules: never hardcode user-facing " +
                "strings (use localization resources), use stable AutomationProperties.AutomationId for element " +
                "identity, do not use Appearance=\"Primary\" on buttons. Reply with only the requested JSON.";

            var client = new LlmClient(_options.Llm);
            var reply = await client.CompleteAsync(systemPrompt, userText.ToString(), images).ConfigureAwait(false);

            try
            {
                var proposal = FixProposalParser.Parse(reply);
                _log($"[ai] Reasoning: {proposal.Reasoning}");
                _log($"[ai] Proposed patches: {string.Join(", ", proposal.FilePatches.Keys)}");
                return proposal;
            }
            catch (Exception ex)
            {
                _log($"[ai] Failed to parse AI proposal: {ex.Message}");
                return null;
            }
        }

        /// <summary>Applies AI-proposed full-file replacements (paths resolved inside the workspace).</summary>
        private void ApplyProposal(FixProposal proposal)
        {
            foreach (var (relativePath, content) in proposal.FilePatches)
            {
                var fullPath = Path.GetFullPath(Path.Combine(_options.WorkspaceRoot, relativePath));
                // Path-safety: the AI must not write outside the workspace.
                if (!fullPath.StartsWith(Path.GetFullPath(_options.WorkspaceRoot), StringComparison.OrdinalIgnoreCase))
                {
                    _log($"[ai] Skipping patch outside workspace: {relativePath}");
                    continue;
                }

                Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
                File.WriteAllText(fullPath, content);
                _log($"[ai] Patched {fullPath}");
            }
        }

        private void Rebuild()
        {
            _log("[build] Running dotnet build ...");
            var psi = new ProcessStartInfo
            {
                FileName = "dotnet",
                Arguments = "build --nologo -v q",
                WorkingDirectory = _options.WorkspaceRoot,
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                UseShellExecute = false
            };

            using var process = Process.Start(psi)!;
            var output = process.StandardOutput.ReadToEnd() + process.StandardError.ReadToEnd();
            process.WaitForExit();

            if (process.ExitCode != 0)
            {
                _log("[build] FAILED:\n" + output);
                throw new InvalidOperationException("Rebuild failed after applying the AI patch; aborting the loop. Fix manually or adjust the model.");
            }

            _log("[build] OK");
        }
    }
}
