using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Pulsar.Models;
using Pulsar.Services;
using Pulsar.Features.Tutorial.Services;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    /// <summary>
    /// Tests for the tutorial write-side API added to IOnboardingStateService
    /// (architecture review candidate C5, 2026-09-09): TutorialOrchestrator used to
    /// write these fields directly via ConfigEditSession; the writes now travel
    /// through the state service so the field semantics have one owner.
    /// GetStateAsync's read mapping (incl. the self-healing rule) is pinned in
    /// OnboardingVerificationTests.
    /// </summary>
    public class OnboardingStateWriteTests : IDisposable
    {
        private readonly string _configPath;
        private readonly ConfigService _configService;
        private readonly OnboardingStateService _service;

        public OnboardingStateWriteTests()
        {
            _configPath = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(), $"{Guid.NewGuid():N}-onboard-write.json");
            _configService = new ConfigService(
                NullLogger<ConfigService>.Instance,
                metadataRegistry: null,
                backgroundWorkScheduler: null,
                configPath: _configPath);
            _service = new OnboardingStateService(_configService);
        }

        public void Dispose()
        {
            if (System.IO.File.Exists(_configPath))
            {
                System.IO.File.Delete(_configPath);
            }
        }

        [Fact]
        public async Task MarkTutorialStartedAsync_ClearsLastTutorialStep()
        {
            await _service.MarkTutorialStepReachedAsync("step2_switch_mode_intro");
            await _service.MarkTutorialStartedAsync();

            var config = await _configService.LoadSnapshotAsync(forceReload: true);
            config.Settings.LastTutorialStep.Should().BeNull();
            config.Settings.HasCompletedTutorial.Should().BeFalse();
        }

        [Fact]
        public async Task MarkTutorialStepReachedAsync_PersistsStepId()
        {
            await _service.MarkSetupCompletedAsync();
            await _service.MarkTutorialStepReachedAsync("step2_switch_mode_intro");

            var config = await _configService.LoadSnapshotAsync(forceReload: true);
            config.Settings.LastTutorialStep.Should().Be("step2_switch_mode_intro");
        }

        [Fact]
        public async Task MarkTutorialCrashedAsync_WritesCrashMarker_KeepsLastStepByDefault()
        {
            await _service.MarkSetupCompletedAsync();
            await _service.MarkTutorialStepReachedAsync("step2_switch_mode_intro");
            await _service.MarkTutorialCrashedAsync("step2_switch_mode_intro");

            var config = await _configService.LoadSnapshotAsync(forceReload: true);
            config.Settings.TutorialCrashedAt.Should().Be("step2_switch_mode_intro");
            config.Settings.LastTutorialStep.Should().Be("step2_switch_mode_intro",
                "the force-cleanup path only writes the crash marker");
            config.Settings.HasCompletedTutorial.Should().BeFalse();
        }

        [Fact]
        public async Task MarkTutorialCrashedAsync_WithClearLastStep_ResetsLastStep()
        {
            await _service.MarkSetupCompletedAsync();
            await _service.MarkTutorialStepReachedAsync("step2_switch_mode_intro");
            await _service.MarkTutorialCrashedAsync("step2_switch_mode_intro", clearLastTutorialStep: true);

            var config = await _configService.LoadSnapshotAsync(forceReload: true);
            config.Settings.TutorialCrashedAt.Should().Be("step2_switch_mode_intro");
            config.Settings.LastTutorialStep.Should().BeNull();
        }

        [Fact]
        public async Task MarkTutorialCompletedAsync_ClearsCrashMarker()
        {
            await _service.MarkSetupCompletedAsync();
            await _service.MarkTutorialCrashedAsync("step2_switch_mode_intro", clearLastTutorialStep: true);
            await _service.MarkTutorialCompletedAsync();

            var config = await _configService.LoadSnapshotAsync(forceReload: true);
            config.Settings.HasCompletedTutorial.Should().BeTrue();
            config.Settings.OnboardingState.Should().Be("Complete");
            config.Settings.TutorialCrashedAt.Should().BeNull();
            config.Settings.LastTutorialStep.Should().BeNull();
        }

        [Fact]
        public async Task MarkTutorialSkippedAsync_WritesSkipMarker()
        {
            // Honesty note (C5): this pins the CURRENT service behaviour — the skip
            // marker does NOT touch HasCompletedTutorial (it is already false in every
            // legal state from which a tutorial can run). The Orchestrator's former
            // redundant HasCompletedTutorial=false write was dropped when routing
            // through this service; the field convergence is a NEXT candidate.
            await _service.MarkTutorialSkippedAsync();

            var config = await _configService.LoadSnapshotAsync(forceReload: true);
            config.Settings.LastTutorialStep.Should().Be("Skipped");
            config.Settings.HasCompletedTutorial.Should().BeFalse();
        }
    }
}
