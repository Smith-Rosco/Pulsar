using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Focus;
using Pulsar.Models;
using Pulsar.Native;
using Pulsar.Services;
using Pulsar.Services.Interfaces;
using Pulsar.Services.WindowSwitching;

namespace Pulsar.Tests.Services
{
    /// <summary>
    /// Orchestration tests that construct <see cref="WindowService"/> directly with mocked
    /// collaborators, exercising the module's real wiring (selection, activation gates,
    /// MRU writes, cache invalidation) that the mock-IWindowService tests cannot reach.
    /// </summary>
    public class WindowServiceOrchestrationTests
    {
        [Fact]
        public async Task SwitchToProcessAsync_WhenInventoryReturnsWindow_ShouldActivateAndReturnTrue()
        {
            var (service, evaluator, inventory, focusManager, _, _, _) = CreateService(isWindow: _ => true);
            try
            {
                var handle = new IntPtr(0x1234);
                var window = new ProcessWindowInfo
                {
                    Handle = handle,
                    Title = "Test Window",
                    ProcessName = "testapp",
                    RealActivationTime = DateTime.Now
                };
                inventory
                    .Setup(i => i.GetProcessWindowsAsync(
                        "testapp",
                        null,
                        It.IsAny<Func<IntPtr, WindowTrackingSnapshot>>(),
                        It.IsAny<Func<string, ImageSource?>>()))
                    .ReturnsAsync(new List<ProcessWindowInfo> { window });
                SetupEligible(evaluator);
                focusManager
                    .Setup(f => f.ActivateWindowAsync(handle, It.IsAny<FocusActivationOptions?>()))
                    .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });

                var selection = service.SelectTargetWindowOrDefault(
                    new List<ProcessWindowInfo> { window },
                    new WindowSelectionRequest
                    {
                        Intent = WindowSelectionIntent.ProcessActivation,
                        SkipMode = WindowSelectionSkipMode.None,
                        CurrentForegroundHandle = PulsarNative.GetForegroundWindow(),
                        PreviousWindowHandle = IntPtr.Zero
                    });

                var selectionForSwitch = service.SelectTargetWindowOrDefault(
                    new List<ProcessWindowInfo> { window },
                    new WindowSelectionRequest
                    {
                        Intent = WindowSelectionIntent.ProcessActivation,
                        SkipMode = WindowSelectionSkipMode.SkipCurrentForeground,
                        CurrentForegroundHandle = PulsarNative.GetForegroundWindow(),
                        PreviousWindowHandle = IntPtr.Zero
                    });

                var result = await service.SwitchToProcessAsync("testapp");

                selection.Should().NotBeNull();
                selectionForSwitch.Should().NotBeNull("selection with SkipCurrentForeground must still resolve");
                result.Should().BeTrue();
                focusManager.Verify(f => f.ActivateWindowAsync(handle, It.IsAny<FocusActivationOptions?>()), Times.AtLeastOnce);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public async Task SwitchToProcessAsync_WhenInventoryReturnsNoWindows_ShouldReturnFalse()
        {
            var (service, _, inventory, _, _, _, _) = CreateService();
            try
            {
                inventory
                    .Setup(i => i.GetProcessWindowsAsync(
                        "ghost",
                        null,
                        It.IsAny<Func<IntPtr, WindowTrackingSnapshot>>(),
                        It.IsAny<Func<string, ImageSource?>>()))
                    .ReturnsAsync(new List<ProcessWindowInfo>());

                var result = await service.SwitchToProcessAsync("ghost");

                result.Should().BeFalse();
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void RecordWindowActivation_WhenEligible_ShouldWriteQuickSwitchHistory()
        {
            var hwnd = new IntPtr(0x1001);
            var (service, evaluator, _, _, _, quickSwitch, _) = CreateService();
            try
            {
                SetupEligible(evaluator);

                service.RecordWindowActivation(hwnd);

                quickSwitch.SnapshotHistory().Should().Contain(hwnd);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void RecordWindowActivation_WhenIneligible_ShouldNotWriteQuickSwitchHistory()
        {
            var hwnd = new IntPtr(0x1002);
            var (service, evaluator, _, _, _, quickSwitch, _) = CreateService();
            try
            {
                SetupEligible(evaluator, included: false, verdict: WindowEligibilityVerdict.ExcludedByRule);

                service.RecordWindowActivation(hwnd);

                quickSwitch.SnapshotHistory().Should().NotContain(hwnd);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public async Task SwitchToPreviousWindow_WhenFirstActivationFails_ShouldFallThroughToNextHistoryWindow()
        {
            using var win1 = new TestWindow("First");
            using var win2 = new TestWindow("Second");
            var (service, evaluator, _, focusManager, _, quickSwitch, _) = CreateService();
            try
            {
                SetupEligible(evaluator);

                // Seed history so the MRU (top) is win1, then win2.
                quickSwitch.RecordWindowActivation(win2.Handle, 10);
                quickSwitch.RecordWindowActivation(win1.Handle, 10);

                focusManager
                    .Setup(f => f.ActivateWindowAsync(win1.Handle, It.IsAny<FocusActivationOptions?>()))
                    .ReturnsAsync(new FocusActivationResult
                    {
                        Success = false,
                        FailureReason = FocusActivationFailureReason.ForegroundSwitchFailed
                    });
                focusManager
                    .Setup(f => f.ActivateWindowAsync(win2.Handle, It.IsAny<FocusActivationOptions?>()))
                    .ReturnsAsync(new FocusActivationResult { Success = true, VerificationPassed = true });

                var result = await service.SwitchToPreviousWindow();

                result.Should().BeTrue();
                focusManager.Verify(f => f.ActivateWindowAsync(win1.Handle, It.IsAny<FocusActivationOptions?>()), Times.Once);
                focusManager.Verify(f => f.ActivateWindowAsync(win2.Handle, It.IsAny<FocusActivationOptions?>()), Times.Once);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void UpdateBlacklist_ShouldForwardEntriesToEvaluator()
        {
            var (service, evaluator, _, _, _, _, _) = CreateService();
            try
            {
                service.UpdateBlacklist(new[] { "myapp" });

                evaluator.Verify(e => e.UpdateBlacklist(
                    It.Is<IEnumerable<string>>(entries => entries.Contains("myapp"))), Times.Once);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void UpdateEligibilityRules_ShouldPropagateToEvaluator()
        {
            var rules = new List<WindowEligibilityRule> { new(false, null, "GhostClass", null) };
            var (service, evaluator, _, _, _, _, _) = CreateService();
            try
            {
                evaluator.SetupGet(e => e.Rules).Returns(rules);

                service.UpdateEligibilityRules(rules);

                evaluator.Verify(e => e.UpdateRules(rules), Times.Once);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void OnWindowActivated_SameProcessHwnd_ShouldNotInvalidateInventory()
        {
            using var testWindow = new TestWindow("OwnProcess");
            var (service, _, _, _, cache, _, _) = CreateService();
            try
            {
                var snapshot = new List<ProcessWindowInfo>
                {
                    new ProcessWindowInfo { Handle = testWindow.Handle, Title = "Cached", ProcessName = "x" }
                };
                cache.Store(snapshot);

                InvokeOnWindowActivated(service, testWindow.Handle);

                cache.TryGet(out var cached).Should().BeTrue();
                cached.Should().BeEquivalentTo(snapshot);
            }
            finally
            {
                service.Dispose();
            }
        }

        [Fact]
        public void OnWindowActivated_NonPulsarSwitch_ShouldInvalidateAndTriggerBackgroundRefresh()
        {
            var (service, _, inventory, _, cache, _, _) = CreateService();
            try
            {
                var snapshot = new List<ProcessWindowInfo>
                {
                    new ProcessWindowInfo { Handle = new IntPtr(0x4001), Title = "Cached", ProcessName = "x" }
                };
                cache.Store(snapshot);

                var pendingRefresh = new TaskCompletionSource<List<ProcessWindowInfo>>(
                    TaskCreationOptions.RunContinuationsAsynchronously);
                inventory
                    .Setup(i => i.GetActiveWindowsAsync(
                        It.IsAny<Func<string, bool>>(),
                        It.IsAny<Func<IntPtr, WindowTrackingSnapshot>>(),
                        It.IsAny<Func<string, ImageSource?>>(),
                        It.IsAny<IProcessRegistryService?>()))
                    .Returns(pendingRefresh.Task);

                InvokeOnWindowActivated(service, new IntPtr(0x5001));

                // The invalidation happens synchronously before the single-flight
                // background refresh re-populates the cache, so it must be observed
                // as a miss while the refresh is still pending.
                cache.TryGet(out _).Should().BeFalse();

                pendingRefresh.SetResult(new List<ProcessWindowInfo>());
            }
            finally
            {
                service.Dispose();
            }
        }

        private static void InvokeOnWindowActivated(WindowService service, IntPtr hwnd)
        {
            var method = typeof(WindowService).GetMethod(
                "OnWindowActivated",
                BindingFlags.NonPublic | BindingFlags.Instance)!;
            method.Invoke(service, new object[] { hwnd });
        }

        private static (WindowService Service,
            Mock<IWindowEligibilityEvaluator> Evaluator,
            Mock<IWindowInventoryService> Inventory,
            Mock<IFocusManager> FocusManager,
            WindowInventoryCache Cache,
            QuickSwitchEngine QuickSwitch,
            WindowTrackingService Tracking) CreateService(
            Func<IntPtr, bool>? isWindow = null)
        {
            var evaluator = new Mock<IWindowEligibilityEvaluator>();
            var inventory = new Mock<IWindowInventoryService>();
            var focusManager = new Mock<IFocusManager>();
            var logger = new Mock<ILogger<WindowService>>();
            var cache = new WindowInventoryCache();
            var quickSwitch = new QuickSwitchEngine();
            var tracking = new WindowTrackingService();

            var service = new WindowService(
                logger.Object,
                focusManager.Object,
                evaluator.Object,
                inventory.Object,
                cache,
                quickSwitch,
                tracking,
                Mock.Of<IWindowCaptureService>(),
                isWindow: isWindow);

            return (service, evaluator, inventory, focusManager, cache, quickSwitch, tracking);
        }

        /// <summary>
        /// Configures the mocked evaluator so every window is judged eligible (or with
        /// the given verdict), regardless of HWND or scope. Eligibility now flows
        /// through the <see cref="IWindowEligibilityEvaluator"/> seam.
        /// </summary>
        private static void SetupEligible(
            Mock<IWindowEligibilityEvaluator> evaluator,
            bool included = true,
            WindowEligibilityVerdict verdict = WindowEligibilityVerdict.Eligible)
        {
            evaluator
                .Setup(e => e.EvaluateWithSnapshot(It.IsAny<IntPtr>(), It.IsAny<EligibilityScope>()))
                .Returns((new EligibilityResult(included, verdict), new WindowEligibilitySnapshot()));
        }

        /// <summary>A real hidden top-level window so native IsWindow/GetWindowRect gates pass.</summary>
        private sealed class TestWindow : IDisposable
        {
            public IntPtr Handle { get; }

            public TestWindow(string title = "PulsarTestWindow")
            {
                Handle = PulsarNative.CreateWindowEx(
                    0,
                    "STATIC",
                    title,
                    0,
                    0,
                    0,
                    200,
                    100,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero,
                    IntPtr.Zero);

                if (Handle == IntPtr.Zero)
                {
                    throw new InvalidOperationException("Failed to create test window");
                }
            }

            public void Dispose()
            {
                PulsarNative.DestroyWindow(Handle);
            }
        }
    }
}
