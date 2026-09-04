// [Path]: Pulsar/Pulsar.E2E/Occlusion/OcclusionAnalyzer.cs

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;
using Pulsar.E2E.AI;
using Pulsar.E2E.Driver;

namespace Pulsar.E2E.Occlusion
{
    /// <summary>One interactive element projected onto the screenshot overlay.</summary>
    public sealed class OverlayElement
    {
        [JsonPropertyName("id")] public string AutomationId { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("type")] public string ControlType { get; set; } = string.Empty;
        [JsonPropertyName("bounds")] public UiElementBounds Bounds { get; set; }
    }

    /// <summary>The overlay JSON handed to the vision model (design D5).</summary>
    public sealed class OverlaySpec
    {
        [JsonPropertyName("imageSize")] public UiElementBounds ImageSize { get; set; }
        [JsonPropertyName("interactiveElements")] public List<OverlayElement> InteractiveElements { get; set; } = new();
    }

    /// <summary>One structured occlusion defect found by the vision model.</summary>
    public sealed class OcclusionDefect
    {
        [JsonPropertyName("type")] public string Type { get; set; } = "interactive-overlap";
        [JsonPropertyName("overlaidId")] public string OverlaidId { get; set; } = string.Empty;
        [JsonPropertyName("occluderId")] public string OccluderId { get; set; } = string.Empty;
        [JsonPropertyName("area")] public double Area { get; set; }
        [JsonPropertyName("suggestion")] public string Suggestion { get; set; } = string.Empty;
    }

    public sealed class OcclusionReport
    {
        [JsonPropertyName("view")] public string View { get; set; } = string.Empty;
        [JsonPropertyName("defects")] public List<OcclusionDefect> Defects { get; set; } = new();
        [JsonPropertyName("allowListed")] public List<string> AllowListed { get; set; } = new();
    }

    /// <summary>
    /// Visual occlusion detection (design D5): stable screenshot (post-animation
    /// settle) + UIA bounding-box overlay projection, consumed by a vision model
    /// that outputs the structured occlusion report. Purely geometric intersection
    /// is deliberately NOT the detector — the vision model applies semantics, so
    /// transparent/rounded/cropped visuals do not flood false positives. The
    /// driver only supplies geometry and filters the model output.
    /// </summary>
    public sealed class OcclusionAnalyzer
    {
        /// <summary>
        /// Expected overlays that never count as defects: menus, tooltips, the
        /// radial menu over whatever is underneath (it is an overlay surface by
        /// design), badges anchored to their slot.
        /// </summary>
        private static readonly string[] AllowListedIdPrefixes =
        {
            "Pulsar.RadialMenuWindow",
            "Pulsar.MenuCanvas",
            "Pulsar.Tray.",
            "Pulsar.Overlay.",
            "Pulsar.Tooltip."
        };

        private readonly UiaDriver _uia;
        private readonly LlmConfig? _llmConfig;
        private readonly Action<string> _log;

        public OcclusionAnalyzer(UiaDriver uia, LlmConfig? llmConfig, Action<string>? log = null)
        {
            _uia = uia;
            _llmConfig = llmConfig;
            _log = log ?? (msg => Console.WriteLine(msg));
        }

        /// <summary>
        /// Captures a stable screenshot and projects the current UIA bounds of all
        /// interactive elements onto it. Returns the overlay spec and image path.
        /// </summary>
        public (OverlaySpec Spec, string ImagePath) CaptureStableOverlay(string imagePath, string[] targetAutomationIds)
        {
            // Post-animation settle: allow in-flight WPF animations to finish so
            // bounds and pixels are consistent.
            System.Threading.Thread.Sleep(700);

            var captured = Capture.CaptureScreenToBitmap(out var bitmap);
            try
            {
                bitmap.Save(imagePath, System.Drawing.Imaging.ImageFormat.Png);
            }
            finally
            {
                bitmap.Dispose();
            }

            var spec = new OverlaySpec
            {
                ImageSize = new UiElementBounds(captured.X, captured.Y, captured.Width, captured.Height)
            };

            foreach (var id in targetAutomationIds)
            {
                var info = _uia.FindElement(id);
                if (info == null)
                {
                    continue;
                }

                spec.InteractiveElements.Add(new OverlayElement
                {
                    AutomationId = info.AutomationId,
                    Name = info.Name,
                    ControlType = info.ControlType,
                    Bounds = info.Bounds
                });
            }

            return (spec, Path.GetFullPath(imagePath));
        }

        /// <summary>
        /// Runs the vision-model occlusion analysis over a captured overlay and
        /// post-filters the model's report: allow-listed overlays and purely
        /// decorative overlaps are dropped; only interactive-region overlaps remain.
        /// </summary>
        public async Task<OcclusionReport> AnalyzeAsync(OverlaySpec spec, string screenshotPath, string view)
        {
            var defects = await RunVisionModelAsync(spec, screenshotPath).ConfigureAwait(false);

            var report = new OcclusionReport { View = view };
            foreach (var defect in defects)
            {
                if (IsAllowListed(defect.OccluderId) || IsAllowListed(defect.OverlaidId))
                {
                    report.AllowListed.Add($"{defect.OverlaidId}<-{defect.OccluderId}");
                    continue;
                }

                // Both participants must be interactive elements we projected;
                // overlaps against unknown/decorative pixels are ignored.
                var overlaid = spec.InteractiveElements.FirstOrDefault(e => e.AutomationId == defect.OverlaidId);
                var occluder = spec.InteractiveElements.FirstOrDefault(e => e.AutomationId == defect.OccluderId);
                if (overlaid == null || occluder == null)
                {
                    continue;
                }

                report.Defects.Add(defect);
            }

            return report;
        }

        private bool IsAllowListed(string automationId)
        {
            return AllowListedIdPrefixes.Any(prefix =>
                automationId.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        }

        private async Task<List<OcclusionDefect>> RunVisionModelAsync(OverlaySpec spec, string screenshotPath)
        {
            if (_llmConfig == null || !_llmConfig.IsValid)
            {
                _log("[occlusion] Vision model not configured; running geometric fallback (intersection over projected bounds).");
                return GeometricFallback(spec);
            }

            var client = new LlmClient(_llmConfig);
            var overlayJson = JsonSerializer.Serialize(spec, new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping
            });

            const string systemPrompt =
                "You are a visual QA analyzer. You receive a screenshot plus the UIA bounding boxes of all " +
                "interactive elements (JSON overlay). Identify ONLY cases where two INTERACTIVE elements' " +
                "visible content overlaps such that one is hard to click or read. Purely decorative overlap " +
                "(backgrounds, glows, shadows, badges on their own slot) is NOT a defect. " +
                "Reply with ONLY JSON: {\"defects\": [{\"type\": \"interactive-overlap\", \"overlaidId\": \"...\", " +
                "\"occluderId\": \"...\", \"area\": <overlapped pixel area>, \"suggestion\": \"...\"}]}";

            try
            {
                var reply = await client.CompleteAsync(systemPrompt, overlayJson, new[] { screenshotPath }).ConfigureAwait(false);
                var json = reply.Trim();
                var brace = json.IndexOf('{');
                if (brace > 0)
                {
                    json = json[brace..];
                }

                using var doc = JsonDocument.Parse(json);
                var defects = new List<OcclusionDefect>();
                if (doc.RootElement.TryGetProperty("defects", out var arr) && arr.ValueKind == JsonValueKind.Array)
                {
                    foreach (var d in arr.EnumerateArray())
                    {
                        defects.Add(new OcclusionDefect
                        {
                            Type = d.TryGetProperty("type", out var t) ? t.GetString() ?? "interactive-overlap" : "interactive-overlap",
                            OverlaidId = d.TryGetProperty("overlaidId", out var o) ? o.GetString() ?? string.Empty : string.Empty,
                            OccluderId = d.TryGetProperty("occluderId", out var c) ? c.GetString() ?? string.Empty : string.Empty,
                            Area = d.TryGetProperty("area", out var a) && a.TryGetDouble(out var area) ? area : 0,
                            Suggestion = d.TryGetProperty("suggestion", out var s) ? s.GetString() ?? string.Empty : string.Empty
                        });
                    }
                }
                return defects;
            }
            catch (Exception ex)
            {
                _log($"[occlusion] Vision model analysis failed: {ex.Message}. Falling back to geometric check.");
                return GeometricFallback(spec);
            }
        }

        /// <summary>
        /// Deterministic fallback used when no vision model is configured: reports
        /// pairwise rectangle intersections between projected interactive bounds.
        /// More conservative than the model (rectangles overround) but deterministic.
        /// </summary>
        private static List<OcclusionDefect> GeometricFallback(OverlaySpec spec)
        {
            var defects = new List<OcclusionDefect>();
            var elements = spec.InteractiveElements;
            for (int i = 0; i < elements.Count; i++)
            {
                for (int j = i + 1; j < elements.Count; j++)
                {
                    var a = elements[i];
                    var b = elements[j];
                    var ax = Math.Max(a.Bounds.X, b.Bounds.X);
                    var ay = Math.Max(a.Bounds.Y, b.Bounds.Y);
                    var bx = Math.Min(a.Bounds.X + a.Bounds.Width, b.Bounds.X + b.Bounds.Width);
                    var by = Math.Min(a.Bounds.Y + a.Bounds.Height, b.Bounds.Y + b.Bounds.Height);
                    if (bx > ax && by > ay)
                    {
                        defects.Add(new OcclusionDefect
                        {
                            Type = "interactive-overlap",
                            OverlaidId = a.AutomationId,
                            OccluderId = b.AutomationId,
                            Area = (bx - ax) * (by - ay),
                            Suggestion = "Geometric intersection of interactive bounds; verify visually."
                        });
                    }
                }
            }
            return defects;
        }

        /// <summary>Diffs a report against a stored baseline; clean = acceptance gate passed.</summary>
        public static bool DiffAgainstBaseline(OcclusionReport report, string baselinePath, Action<string> log)
        {
            if (!File.Exists(baselinePath))
            {
                // First run establishes the baseline.
                File.WriteAllText(baselinePath, JsonSerializer.Serialize(report, new JsonSerializerOptions { WriteIndented = true }));
                log($"[occlusion] Baseline established at {baselinePath}");
                return report.Defects.Count == 0;
            }

            var baseline = JsonSerializer.Deserialize<OcclusionReport>(File.ReadAllText(baselinePath));
            var baselineKeys = (baseline?.Defects ?? new List<OcclusionDefect>())
                .Select(d => $"{d.OverlaidId}|{d.OccluderId}")
                .OrderBy(k => k).ToList();
            var currentKeys = report.Defects
                .Select(d => $"{d.OverlaidId}|{d.OccluderId}")
                .OrderBy(k => k).ToList();

            var identical = baselineKeys.SequenceEqual(currentKeys);
            if (!identical)
            {
                log("[occlusion] Layout changed relative to baseline: " +
                    $"baseline={string.Join(", ", baselineKeys)} current={string.Join(", ", currentKeys)}");
            }

            // Acceptance gate: a clean report is required to pass visual regression;
            // an unchanged clean view diffs identically.
            return identical && report.Defects.Count == 0;
        }
    }
}
