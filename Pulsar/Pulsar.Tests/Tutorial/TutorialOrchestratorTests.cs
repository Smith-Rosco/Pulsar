using System;
using System.Collections.Generic;
using System.Reflection;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Models;
using Pulsar.Models.Tutorial;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Tutorial;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class TutorialOrchestratorTests
    {
        private static TutorialOrchestrator CreateOrchestrator(IConfigService? configService = null)
        {
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l["Tutorial.SwitchedToApp"]).Returns("Switched to {0}!");
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);

            configService ??= Mock.Of<IConfigService>(c =>
                c.Current == new ProfilesConfig());

            var logger = Mock.Of<ILogger<TutorialOrchestrator>>();
            var stepLoaderLogger = Mock.Of<ILogger<TutorialStepLoader>>();
            var stepLoader = new TutorialStepLoader(stepLoaderLogger, loc.Object);
            var overlayManager = Mock.Of<IOverlayManager>();
            var triggerEngine = Mock.Of<ITutorialTriggerEngine>();
            var spotlightController = Mock.Of<ITutorialSpotlightController>();
            var waitStepHintTimeout = Mock.Of<IWaitStepHintTimeout>();

            return new TutorialOrchestrator(
                loc.Object,
                configService,
                logger,
                stepLoader,
                overlayManager,
                triggerEngine,
                spotlightController,
                waitStepHintTimeout);
        }

        [Fact]
        public void ShouldSkipStep_WithStep3Instruction_ShouldReturnTrue()
        {
            var orchestrator = CreateOrchestrator();
            var method = typeof(TutorialOrchestrator)
                .GetMethod("ShouldSkipStep", BindingFlags.NonPublic | BindingFlags.Instance);

            var steps = new List<TutorialStep>
            {
                new() { Id = "step1_onboarding_welcome", Type = TutorialStepType.Instruction },
                new() { Id = "step2_switch_mode_intro", Type = TutorialStepType.WaitForAction },
                new() { Id = "step3_switch_mode_success", Type = TutorialStepType.Instruction },
                new() { Id = "step4_command_mode_intro", Type = TutorialStepType.WaitForAction },
            };

            var stepsField = typeof(TutorialOrchestrator)
                .GetField("_steps", BindingFlags.NonPublic | BindingFlags.Instance);
            stepsField!.SetValue(orchestrator, steps);

            var result = (bool)method!.Invoke(orchestrator, new object[] { 2 })!;
            result.Should().BeTrue();
        }

        [Fact]
        public void ShouldSkipStep_WithNonStep3Index_ShouldReturnFalse()
        {
            var orchestrator = CreateOrchestrator();
            var method = typeof(TutorialOrchestrator)
                .GetMethod("ShouldSkipStep", BindingFlags.NonPublic | BindingFlags.Instance);

            var steps = new List<TutorialStep>
            {
                new() { Id = "step1_onboarding_welcome", Type = TutorialStepType.Instruction },
                new() { Id = "step2_switch_mode_intro", Type = TutorialStepType.WaitForAction },
                new() { Id = "step3_switch_mode_success", Type = TutorialStepType.Instruction },
            };

            var stepsField = typeof(TutorialOrchestrator)
                .GetField("_steps", BindingFlags.NonPublic | BindingFlags.Instance);
            stepsField!.SetValue(orchestrator, steps);

            var result = (bool)method!.Invoke(orchestrator, new object[] { 0 })!;
            result.Should().BeFalse();
        }

        [Fact]
        public void ShouldSkipStep_WithOutOfRangeIndex_ShouldReturnFalse()
        {
            var orchestrator = CreateOrchestrator();
            var method = typeof(TutorialOrchestrator)
                .GetMethod("ShouldSkipStep", BindingFlags.NonPublic | BindingFlags.Instance);

            var steps = new List<TutorialStep> { new() { Id = "step1", Type = TutorialStepType.Instruction } };
            var stepsField = typeof(TutorialOrchestrator)
                .GetField("_steps", BindingFlags.NonPublic | BindingFlags.Instance);
            stepsField!.SetValue(orchestrator, steps);

            var result = (bool)method!.Invoke(orchestrator, new object[] { 5 })!;
            result.Should().BeFalse();
        }

        [Fact]
        public void HasRequiredSlot_WithNoCommandSlots_ShouldReturnFalse()
        {
            var config = new ProfilesConfig
            {
                Profiles = new Dictionary<string, ProcessProfile>
                {
                    ["Global"] = new ProcessProfile()
                }
            };
            var configService = Mock.Of<IConfigService>(c => c.Current == config);

            var orchestrator = CreateOrchestrator(configService);

            var method = typeof(TutorialOrchestrator)
                .GetMethod("HasRequiredSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = (bool)method!.Invoke(orchestrator, null)!;
            result.Should().BeFalse();
        }

        [Fact]
        public void HasRequiredSlot_WithCommandSlots_ShouldReturnTrue()
        {
            var config = new ProfilesConfig
            {
                Profiles = new Dictionary<string, ProcessProfile>
                {
                    ["Global"] = new ProcessProfile
                    {
                        CommandMode = new List<PluginSlot>
                        {
                            new() { Slot = 1, PluginId = "com.pulsar.command", Action = "sendkeys" }
                        }
                    }
                }
            };
            var configService = Mock.Of<IConfigService>(c => c.Current == config);

            var orchestrator = CreateOrchestrator(configService);

            var method = typeof(TutorialOrchestrator)
                .GetMethod("HasRequiredSlot", BindingFlags.NonPublic | BindingFlags.Instance);
            var result = (bool)method!.Invoke(orchestrator, null)!;
            result.Should().BeTrue();
        }
    }
}
