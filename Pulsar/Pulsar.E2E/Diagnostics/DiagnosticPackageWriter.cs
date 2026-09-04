// [Path]: Pulsar/Pulsar.E2E/Diagnostics/DiagnosticPackageWriter.cs

using System;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;

namespace Pulsar.E2E.Diagnostics
{
    /// <summary>
    /// The diagnostic package is the ONLY input the visual AI iteration loop
    /// consumes (design D3). Layout, per run, per failing step:
    /// <code>
    /// artifacts/&lt;run-id&gt;/&lt;step-id&gt;/
    ///   failure.json     — failed step, assertion message, timeout data
    ///   uia-tree.txt     — UIA automation tree dump (id / name / bounds / enabled)
    ///   screenshot.png   — screen-level capture at failure moment
    ///   video.mp4        — recording clip (when recording was active)
    ///   logs/excerpt.log — relevant log slice from the debug instance
    /// </code>
    /// </summary>
    public sealed class FailureReport
    {
        [JsonPropertyName("runId")] public string RunId { get; set; } = string.Empty;
        [JsonPropertyName("workflow")] public string Workflow { get; set; } = string.Empty;
        [JsonPropertyName("stepId")] public string StepId { get; set; } = string.Empty;
        [JsonPropertyName("stepType")] public string StepType { get; set; } = string.Empty;
        [JsonPropertyName("message")] public string Message { get; set; } = string.Empty;
        [JsonPropertyName("failedAtUtc")] public DateTime FailedAtUtc { get; set; }
        [JsonPropertyName("details")] public JsonElement? Details { get; set; }
    }

    public static class DiagnosticPackageWriter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
        };

        public static string GetStepDirectory(string artifactsRoot, string runId, string stepId)
        {
            return Path.Combine(artifactsRoot, runId, stepId);
        }

        public static void WriteFailureJson(string stepDir, FailureReport report)
        {
            Directory.CreateDirectory(stepDir);
            File.WriteAllText(Path.Combine(stepDir, "failure.json"), JsonSerializer.Serialize(report, JsonOptions));
        }

        public static void WriteUiaTree(string stepDir, string treeDump)
        {
            Directory.CreateDirectory(stepDir);
            File.WriteAllText(Path.Combine(stepDir, "uia-tree.txt"), treeDump);
        }

        public static void WriteScreenshot(string stepDir, string sourcePng)
        {
            if (!File.Exists(sourcePng))
            {
                return;
            }
            Directory.CreateDirectory(stepDir);
            var target = Path.Combine(stepDir, "screenshot.png");
            File.Copy(sourcePng, target, overwrite: true);
        }

        public static void WriteVideo(string stepDir, string sourceMp4)
        {
            if (!File.Exists(sourceMp4))
            {
                return;
            }
            Directory.CreateDirectory(stepDir);
            var target = Path.Combine(stepDir, "video.mp4");
            CopyWithRetry(sourceMp4, target, attempts: 20, delayMs: 250);
        }

        /// <summary>
        /// Copies with retry: ScreenRecorderLib finalizes the MP4 asynchronously and
        /// the file lock (finalizer or antivirus scan) can outlive OnRecordingComplete.
        /// </summary>
        private static void CopyWithRetry(string source, string target, int attempts, int delayMs)
        {
            for (int attempt = 0; ; attempt++)
            {
                try
                {
                    File.Copy(source, target, overwrite: true);
                    return;
                }
                catch (Exception ex) when (attempt < attempts - 1
                    && (ex is IOException || ex is UnauthorizedAccessException))
                {
                    Thread.Sleep(delayMs);
                }
            }
        }

        /// <summary>
        /// Writes a log excerpt: the tail of the newest debug-instance log file
        /// (bounded so the package stays reviewable by the AI loop).
        /// </summary>
        public static void WriteLogExcerpt(string stepDir, string debugConfigDir, int maxLines = 400)
        {
            try
            {
                var logsDir = Path.Combine(debugConfigDir, "Logs");
                if (!Directory.Exists(logsDir))
                {
                    return;
                }

                var newest = new DirectoryInfo(logsDir)
                    .EnumerateFiles("*.log", SearchOption.TopDirectoryOnly)
                    .OrderByDescending(f => f.LastWriteTimeUtc)
                    .FirstOrDefault();
                if (newest == null)
                {
                    return;
                }

                var allLines = File.ReadAllLines(newest.FullName);
                var excerpt = allLines.Length <= maxLines
                    ? allLines
                    : allLines[^maxLines..];

                Directory.CreateDirectory(Path.Combine(stepDir, "logs"));
                File.WriteAllLines(Path.Combine(stepDir, "logs", "excerpt.log"), excerpt);
            }
            catch (Exception)
            {
                // A missing log excerpt must never mask the real failure.
            }
        }
    }
}
