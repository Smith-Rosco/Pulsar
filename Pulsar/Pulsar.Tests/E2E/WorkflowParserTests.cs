// [Path]: Pulsar/Pulsar.Tests/E2E/WorkflowParserTests.cs

using System;
using System.Linq;
using FluentAssertions;
using Pulsar.E2E.Workflow;
using Xunit;

namespace Pulsar.Tests.E2E
{
    /// <summary>
    /// Unit tests for the E2E JSON workflow parser: happy path plus every
    /// failure mode must produce a clear, actionable error (task 4.5).
    /// </summary>
    public class WorkflowParserTests
    {
        [Fact]
        public void Parse_MinimalWorkflow_ReturnsSteps()
        {
            var json = """
            {
              "name": "launch-and-exit",
              "steps": [
                { "type": "launch", "id": "launch" },
                { "type": "exit", "id": "exit" }
              ]
            }
            """;

            var workflow = WorkflowParser.Parse(json);

            workflow.Name.Should().Be("launch-and-exit");
            workflow.Steps.Should().HaveCount(2);
            workflow.Steps[0].Type.Should().Be(StepType.Launch);
            workflow.Steps[1].Type.Should().Be(StepType.Exit);
        }

        [Fact]
        public void Parse_AllStepTypes_MapsTypesCorrectly()
        {
            var json = """
            {
              "name": "all",
              "app": { "exePath": "C:/x/Pulsar.exe", "fixture": "fix.json", "arguments": "--v" },
              "steps": [
                { "type": "launch", "id": "l" },
                { "type": "wait", "id": "w", "durationMs": 250 },
                { "type": "waitForState", "id": "wfs", "event": "menu-opened", "timeoutMs": 3000 },
                { "type": "hotkey", "id": "hk", "keys": "Ctrl+Space" },
                { "type": "command", "id": "cmd", "command": "open-settings" },
                { "type": "click", "id": "c", "automationId": "Pulsar.Slot.1" },
                { "type": "assert", "id": "a", "automationId": "Pulsar.Slot.1", "expected": "visible" },
                { "type": "screenshot", "id": "s", "file": "shot.png" },
                { "type": "scroll", "id": "sc", "automationId": "Pulsar.Settings.Analytics.ScrollViewer", "direction": "down" },
                { "type": "dump", "id": "d", "file": "tree.txt" },
                { "type": "record", "id": "r", "action": "start" },
                { "type": "exit", "id": "e" }
              ]
            }
            """;

            var workflow = WorkflowParser.Parse(json);

            workflow.Steps.Select(s => s.Type).Should().Equal(
                StepType.Launch, StepType.Wait, StepType.WaitForState, StepType.Hotkey,
                StepType.Command, StepType.Click, StepType.Assert, StepType.Screenshot, StepType.Scroll, StepType.Dump, StepType.Record, StepType.Exit);
            workflow.Steps[1].DurationMs.Should().Be(250);
            workflow.Steps[2].Event.Should().Be("menu-opened");
            workflow.Steps[2].TimeoutMs.Should().Be(3000);
            workflow.Steps[3].Keys.Should().Be("Ctrl+Space");
            workflow.Steps[4].Command.Should().Be("open-settings");
            workflow.Steps[5].AutomationId.Should().Be("Pulsar.Slot.1");
            workflow.Steps[6].Expected.Should().Be("visible");
            workflow.Steps[7].File.Should().Be("shot.png");
            workflow.Steps[8].AutomationId.Should().Be("Pulsar.Settings.Analytics.ScrollViewer");
            workflow.Steps[8].Direction.Should().Be("down");
            workflow.App.ExePath.Should().Be("C:/x/Pulsar.exe");
            workflow.App.Fixture.Should().Be("fix.json");
        }

        [Fact]
        public void Parse_MalformedJson_ThrowsWithMessage()
        {
            var act = () => WorkflowParser.Parse("{ not json ]");

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*Malformed workflow JSON*");
        }

        [Fact]
        public void Parse_MissingStepsArray_Throws()
        {
            var act = () => WorkflowParser.Parse("""{ "name": "x" }""");

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'steps' array*");
        }

        [Fact]
        public void Parse_MissingStepType_ThrowsNamingIndex()
        {
            var json = """
            { "steps": [ { "id": "one" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*steps[0]*missing the required 'type'*");
        }

        [Fact]
        public void Parse_UnknownStepType_ThrowsWithKnownTypes()
        {
            var json = """
            { "steps": [ { "type": "explode", "id": "boom" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*unknown step type 'explode'*launch*");
        }

        [Fact]
        public void Parse_WaitForStateWithoutEvent_ThrowsNamingStepId()
        {
            var json = """
            { "steps": [ { "type": "waitForState", "id": "waiter" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'waiter'*requires an 'event' field*");
        }

        [Fact]
        public void Parse_CommandWithoutCommandName_Throws()
        {
            var json = """
            { "steps": [ { "type": "command", "id": "cmd" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'cmd'*requires a 'command' field*");
        }

        [Fact]
        public void Parse_HotkeyWithoutKeys_Throws()
        {
            var json = """
            { "steps": [ { "type": "hotkey", "id": "hk" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'hk'*requires a 'keys' field*");
        }

        [Fact]
        public void Parse_ClickWithoutAutomationId_Throws()
        {
            var json = """
            { "steps": [ { "type": "click", "id": "c" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'c'*requires an 'automationId' field*");
        }

        [Fact]
        public void Parse_ScrollWithoutAutomationId_Throws()
        {
            var json = """
            { "steps": [ { "type": "scroll", "id": "sc" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'sc'*requires an 'automationId' field*");
        }

        [Fact]
        public void Parse_ScrollInvalidDirection_Throws()
        {
            var json = """
            { "steps": [ { "type": "scroll", "id": "sc", "automationId": "Pulsar.Settings.Analytics.ScrollViewer", "direction": "sideways" } ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*'sc'*'direction' of 'down' or 'up'*");
        }

        [Fact]
        public void Parse_DuplicateStepId_Throws()
        {
            var json = """
            { "steps": [
                { "type": "launch", "id": "same" },
                { "type": "exit", "id": "same" }
            ] }
            """;

            var act = () => WorkflowParser.Parse(json);

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*Duplicate step id 'same'*");
        }

        [Fact]
        public void Parse_EmptySteps_Throws()
        {
            var act = () => WorkflowParser.Parse("""{ "steps": [] }""");

            act.Should().Throw<WorkflowParseException>()
                .WithMessage("*at least one step*");
        }

        [Fact]
        public void Parse_MissingId_AutoGeneratesStableId()
        {
            var json = """
            { "steps": [ { "type": "launch" }, { "type": "exit" } ] }
            """;

            var workflow = WorkflowParser.Parse(json);

            workflow.Steps[0].Id.Should().Be("launch-0");
            workflow.Steps[1].Id.Should().Be("exit-1");
        }
    }
}
