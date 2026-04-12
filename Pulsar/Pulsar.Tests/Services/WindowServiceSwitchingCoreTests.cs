using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Services.Interfaces;

namespace Pulsar.Tests.Services
{
    public class WindowServiceSwitchingCoreTests
    {
        [Fact]
        public void SelectTargetWindow_ShouldPreferRealActivationRecency()
        {
            var older = CreateWindow(new IntPtr(11), realActivationTime: new DateTime(2026, 1, 1, 10, 0, 0), lastActivationTime: new DateTime(2026, 1, 1, 9, 0, 0));
            var newer = CreateWindow(new IntPtr(22), realActivationTime: new DateTime(2026, 1, 1, 11, 0, 0), lastActivationTime: new DateTime(2026, 1, 1, 8, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { older, newer },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.ProcessActivation,
                    SkipMode = WindowSelectionSkipMode.None
                },
                _ => true);

            result.SelectedWindow.Should().BeSameAs(newer);
        }

        [Fact]
        public void SelectTargetWindow_ShouldTreatUntrackedWindowsAsFallback()
        {
            var tracked = CreateWindow(new IntPtr(11), realActivationTime: new DateTime(2026, 1, 1, 10, 0, 0), firstSeenTime: new DateTime(2026, 1, 1, 9, 0, 0));
            var untracked = CreateWindow(new IntPtr(22), realActivationTime: DateTime.MinValue, lastActivationTime: DateTime.MaxValue, firstSeenTime: new DateTime(2026, 1, 1, 8, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { untracked, tracked },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.ProcessActivation,
                    SkipMode = WindowSelectionSkipMode.None
                },
                _ => true);

            result.SelectedWindow.Should().BeSameAs(tracked);
        }

        [Fact]
        public void SelectTargetWindow_ShouldSkipCurrentForeground_WhenRequested()
        {
            var current = CreateWindow(new IntPtr(11), realActivationTime: new DateTime(2026, 1, 1, 11, 0, 0));
            var next = CreateWindow(new IntPtr(22), realActivationTime: new DateTime(2026, 1, 1, 10, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { current, next },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.ProcessActivation,
                    SkipMode = WindowSelectionSkipMode.SkipCurrentForeground,
                    CurrentForegroundHandle = current.Handle
                },
                _ => true);

            result.SelectedWindow.Should().BeSameAs(next);
        }

        [Fact]
        public void SelectTargetWindow_ShouldSkipPreviousWindow_WhenRequested()
        {
            var previous = CreateWindow(new IntPtr(11), realActivationTime: new DateTime(2026, 1, 1, 11, 0, 0));
            var next = CreateWindow(new IntPtr(22), realActivationTime: new DateTime(2026, 1, 1, 10, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { previous, next },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.GroupedSwitch,
                    SkipMode = WindowSelectionSkipMode.SkipPreviousWindow,
                    PreviousWindowHandle = previous.Handle
                },
                _ => true);

            result.SelectedWindow.Should().BeSameAs(next);
        }

        [Fact]
        public void SelectTargetWindow_ShouldFallbackToBestCandidate_WhenAllCandidatesAreSkipped()
        {
            var onlyCandidate = CreateWindow(new IntPtr(11), realActivationTime: new DateTime(2026, 1, 1, 11, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { onlyCandidate },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.ProcessActivation,
                    SkipMode = WindowSelectionSkipMode.SkipCurrentForeground,
                    CurrentForegroundHandle = onlyCandidate.Handle
                },
                _ => true);

            result.SelectedWindow.Should().BeSameAs(onlyCandidate);
        }

        [Fact]
        public void SelectTargetWindow_ShouldUseStableOrder_WhenCandidatesLackRecency()
        {
            var laterDisplay = CreateWindow(new IntPtr(11), firstSeenTime: new DateTime(2026, 1, 1, 10, 0, 0));
            var earlierDisplay = CreateWindow(new IntPtr(22), firstSeenTime: new DateTime(2026, 1, 1, 9, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { laterDisplay, earlierDisplay },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.SubMenuDefault,
                    SkipMode = WindowSelectionSkipMode.None
                },
                _ => true);

            result.SelectedWindow.Should().BeSameAs(earlierDisplay);
            result.DecisionReason.Should().Contain("stable display order");
        }

        [Fact]
        public void SelectTargetWindow_ShouldReturnDecisionMetadata()
        {
            var current = CreateWindow(new IntPtr(11), realActivationTime: new DateTime(2026, 1, 1, 11, 0, 0));
            var next = CreateWindow(new IntPtr(22), realActivationTime: new DateTime(2026, 1, 1, 10, 0, 0));

            var result = WindowService.SelectTargetWindow(
                new[] { current, next },
                new WindowSelectionRequest
                {
                    Intent = WindowSelectionIntent.ProcessActivation,
                    SkipMode = WindowSelectionSkipMode.SkipCurrentForeground,
                    CurrentForegroundHandle = current.Handle
                },
                _ => true);

            result.HasSelection.Should().BeTrue();
            result.SkippedHandle.Should().Be(current.Handle);
            result.RankedHandles.Should().ContainInOrder(current.Handle, next.Handle);
            result.DecisionReason.Should().Contain(nameof(WindowSelectionSkipMode.SkipCurrentForeground));
        }

        [Fact]
        public void ActivateWindow_ShouldFailForInvalidHandle()
        {
            var result = WindowService.ActivateWindow(
                CreateWindow(new IntPtr(33)),
                _ => false);

            result.Success.Should().BeFalse();
        }

        private static ProcessWindowInfo CreateWindow(
            IntPtr handle,
            DateTime? realActivationTime = null,
            DateTime? lastActivationTime = null,
            DateTime? firstSeenTime = null)
        {
            return new ProcessWindowInfo
            {
                Handle = handle,
                Title = $"Window-{handle}",
                ProcessName = "testapp",
                RealActivationTime = realActivationTime ?? DateTime.MinValue,
                LastActivationTime = lastActivationTime ?? realActivationTime ?? DateTime.MinValue,
                FirstSeenTime = firstSeenTime ?? DateTime.MinValue
            };
        }
    }
}
