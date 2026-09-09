using System;
using System.Collections.Generic;
using FluentAssertions;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services;
using Pulsar.Features.Tutorial.Views;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    /// <summary>
    /// Unit tests for TutorialFlowPolicy — the pure skip/focus decision logic
    /// extracted from TutorialOrchestrator (architecture review candidate C5,
    /// 2026-09-09). No WPF, no mocks: total functions on their inputs.
    /// </summary>
    public class TutorialFlowPolicyTests
    {
        private static List<TutorialStep> Steps(params TutorialStep[] steps) => new(steps);

        private static TutorialStep Step(string id, TutorialStepType type) => new() { Id = id, Type = type };

        // ---- ShouldSkipConfirmationStep ----

        [Fact]
        public void ShouldSkipConfirmationStep_Step3Instruction_ReturnsTrue()
        {
            var steps = Steps(
                Step("step1_onboarding_welcome", TutorialStepType.Instruction),
                Step("step2_switch_mode_intro", TutorialStepType.WaitForAction),
                Step("step3_switch_mode_success", TutorialStepType.Instruction),
                Step("step4_command_mode_intro", TutorialStepType.WaitForAction));

            TutorialFlowPolicy.ShouldSkipConfirmationStep(steps, 2).Should().BeTrue();
        }

        [Fact]
        public void ShouldSkipConfirmationStep_SameIdButWaitForAction_ReturnsFalse()
        {
            var steps = Steps(Step("step3_switch_mode_success", TutorialStepType.WaitForAction));

            TutorialFlowPolicy.ShouldSkipConfirmationStep(steps, 0).Should().BeFalse();
        }

        [Fact]
        public void ShouldSkipConfirmationStep_SameTypeButOtherId_ReturnsFalse()
        {
            var steps = Steps(Step("step5_something_else", TutorialStepType.Instruction));

            TutorialFlowPolicy.ShouldSkipConfirmationStep(steps, 0).Should().BeFalse();
        }

        [Theory]
        [InlineData(-1)]
        [InlineData(99)]
        public void ShouldSkipConfirmationStep_OutOfRangeIndex_ReturnsFalse(int index)
        {
            var steps = Steps(Step("step3_switch_mode_success", TutorialStepType.Instruction));

            TutorialFlowPolicy.ShouldSkipConfirmationStep(steps, index).Should().BeFalse();
        }

        [Fact]
        public void ShouldSkipConfirmationStep_NullSteps_ReturnsFalse()
        {
            TutorialFlowPolicy.ShouldSkipConfirmationStep(null!, 0).Should().BeFalse();
        }

        [Fact]
        public void ShouldSkipConfirmationStep_EmptySteps_ReturnsFalse()
        {
            TutorialFlowPolicy.ShouldSkipConfirmationStep(Steps(), 0).Should().BeFalse();
        }

        // ---- DetermineFocusState ----

        [Fact]
        public void DetermineFocusState_AlwaysFocused_ReturnsFocused()
        {
            var step = new TutorialStep { FocusMode = TutorialFocusMode.AlwaysFocused, Type = TutorialStepType.WaitForAction };

            TutorialFlowPolicy.DetermineFocusState(step).Should().Be(OverlayState.Focused);
        }

        [Fact]
        public void DetermineFocusState_AlwaysObserving_ReturnsObserving()
        {
            var step = new TutorialStep { FocusMode = TutorialFocusMode.AlwaysObserving, Type = TutorialStepType.Instruction };

            TutorialFlowPolicy.DetermineFocusState(step).Should().Be(OverlayState.Observing);
        }

        [Fact]
        public void DetermineFocusState_Auto_Instruction_ReturnsFocused()
        {
            var step = new TutorialStep { FocusMode = TutorialFocusMode.Auto, Type = TutorialStepType.Instruction };

            TutorialFlowPolicy.DetermineFocusState(step).Should().Be(OverlayState.Focused);
        }

        [Fact]
        public void DetermineFocusState_Auto_WaitForAction_ReturnsObserving()
        {
            var step = new TutorialStep { FocusMode = TutorialFocusMode.Auto, Type = TutorialStepType.WaitForAction };

            TutorialFlowPolicy.DetermineFocusState(step).Should().Be(OverlayState.Observing);
        }

        [Fact]
        public void DetermineFocusState_UnknownMode_FallsBackToFocused()
        {
            // Honesty note: this pins the current default-case behaviour —
            // an unrecognised FocusMode value yields Focused, not an error.
            var step = new TutorialStep { FocusMode = (TutorialFocusMode)999 };

            TutorialFlowPolicy.DetermineFocusState(step).Should().Be(OverlayState.Focused);
        }
    }
}
