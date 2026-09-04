// [Path]: Pulsar/Pulsar.Tests/E2E/OcclusionAnalyzerTests.cs

using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.E2E.Occlusion;
using Xunit;

namespace Pulsar.Tests.E2E
{
    /// <summary>
    /// Unit tests for the occlusion detection pipeline (task 8.5): the geometric
    /// fallback detector (used when no vision model is configured), the
    /// allow-list / unknown-participant post-filter, and the baseline acceptance
    /// gate. No UIA or screen capture needed — pure geometry and filtering.
    /// </summary>
    public class OcclusionAnalyzerTests : IDisposable
    {
        private readonly string _tempDir;

        public OcclusionAnalyzerTests()
        {
            _tempDir = Path.Combine(Path.GetTempPath(), "pulsar-occlusion-tests-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(_tempDir);
        }

        [Fact]
        public async Task Analyze_OverlappingInteractiveElements_ProduceInteractiveOverlapDefect()
        {
            var analyzer = new OcclusionAnalyzer(uia: null!, llmConfig: null);
            var spec = MakeSpec(
                ("Pulsar.Settings.SaveChangesButton", 0, 0, 100, 40),
                ("Pulsar.Settings.CancelButton", 50, 0, 100, 40));

            var report = await analyzer.AnalyzeAsync(spec, "unused.png", "settings-view");

            report.Defects.Should().HaveCount(1);
            var defect = report.Defects[0];
            defect.Type.Should().Be("interactive-overlap");
            new[] { defect.OverlaidId, defect.OccluderId }.Should().BeEquivalentTo(
                "Pulsar.Settings.SaveChangesButton", "Pulsar.Settings.CancelButton");
            defect.Area.Should().Be(50 * 40);
        }

        [Fact]
        public async Task Analyze_NonOverlappingElements_ProduceNoDefects()
        {
            var analyzer = new OcclusionAnalyzer(uia: null!, llmConfig: null);
            var spec = MakeSpec(
                ("Pulsar.Settings.SaveChangesButton", 0, 0, 100, 40),
                ("Pulsar.Settings.CancelButton", 200, 0, 100, 40));

            var report = await analyzer.AnalyzeAsync(spec, "unused.png", "settings-view");

            report.Defects.Should().BeEmpty();
        }

        [Fact]
        public async Task Analyze_AllowListedOverlay_IsNotADefect()
        {
            var analyzer = new OcclusionAnalyzer(uia: null!, llmConfig: null);
            // The radial menu window is an overlay surface by design: overlapping it
            // must never be reported as an occlusion defect.
            var spec = MakeSpec(
                ("Pulsar.Settings.SaveChangesButton", 0, 0, 100, 40),
                ("Pulsar.RadialMenuWindow", 50, 0, 100, 40));

            var report = await analyzer.AnalyzeAsync(spec, "unused.png", "settings-view");

            report.Defects.Should().BeEmpty();
            report.AllowListed.Should().ContainSingle();
        }

        [Fact]
        public void DiffAgainstBaseline_FirstCleanRun_EstablishesBaselineAndPasses()
        {
            var baseline = Path.Combine(_tempDir, "settings-view.json");
            var report = new OcclusionReport { View = "settings-view" };

            var passed = OcclusionAnalyzer.DiffAgainstBaseline(
                report, baseline, _ => { });

            passed.Should().BeTrue();
            File.Exists(baseline).Should().BeTrue();
        }

        [Fact]
        public void DiffAgainstBaseline_UnchangedCleanView_Passes()
        {
            var baseline = Path.Combine(_tempDir, "settings-view.json");
            OcclusionAnalyzer.DiffAgainstBaseline(
                new OcclusionReport { View = "settings-view" }, baseline, _ => { });

            var passed = OcclusionAnalyzer.DiffAgainstBaseline(
                new OcclusionReport { View = "settings-view" }, baseline, _ => { });

            passed.Should().BeTrue();
        }

        [Fact]
        public void DiffAgainstBaseline_ChangedLayout_FailsGate()
        {
            var baseline = Path.Combine(_tempDir, "settings-view.json");
            var dirty = new OcclusionReport { View = "settings-view" };
            dirty.Defects.Add(new OcclusionDefect
            {
                OverlaidId = "A",
                OccluderId = "B",
                Area = 100
            });
            OcclusionAnalyzer.DiffAgainstBaseline(dirty, baseline, _ => { });

            var newDefects = new OcclusionReport { View = "settings-view" };
            newDefects.Defects.Add(new OcclusionDefect
            {
                OverlaidId = "C",
                OccluderId = "D",
                Area = 100
            });

            var passed = OcclusionAnalyzer.DiffAgainstBaseline(newDefects, baseline, _ => { });

            passed.Should().BeFalse();
        }

        private static OverlaySpec MakeSpec(params (string Id, double X, double Y, double W, double H)[] elements)
        {
            var spec = new OverlaySpec
            {
                ImageSize = new Pulsar.E2E.Driver.UiElementBounds(0, 0, 1920, 1080)
            };
            foreach (var (id, x, y, w, h) in elements)
            {
                spec.InteractiveElements.Add(new OverlayElement
                {
                    AutomationId = id,
                    Name = id,
                    ControlType = "Button",
                    Bounds = new Pulsar.E2E.Driver.UiElementBounds(x, y, w, h)
                });
            }

            return spec;
        }

        public void Dispose()
        {
            try { Directory.Delete(_tempDir, recursive: true); } catch { /* best effort */ }
        }
    }
}
