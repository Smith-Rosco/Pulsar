// [Path]: Pulsar/Pulsar.E2E/Workflow/WorkflowParser.cs

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;

namespace Pulsar.E2E.Workflow
{
    /// <summary>
    /// Parses workflow JSON into a <see cref="WorkflowDefinition"/>. All failure
    /// modes produce a <see cref="WorkflowParseException"/> whose message names the
    /// offending location and what was wrong — malformed JSON, a missing/unknown
    /// step type, or a step without an id — so both humans and the AI iteration
    /// loop get actionable errors.
    /// </summary>
    public static class WorkflowParser
    {
        private static readonly Dictionary<string, StepType> KnownStepTypes = new(StringComparer.OrdinalIgnoreCase)
        {
            ["launch"] = StepType.Launch,
            ["wait"] = StepType.Wait,
            ["waitforstate"] = StepType.WaitForState,
            ["hotkey"] = StepType.Hotkey,
            ["menu-open"] = StepType.MenuOpen,
            ["menu-close"] = StepType.MenuClose,
            ["click"] = StepType.Click,
            ["assert"] = StepType.Assert,
            ["screenshot"] = StepType.Screenshot,
            ["record"] = StepType.Record,
            ["exit"] = StepType.Exit
        };

        public static WorkflowDefinition ParseFile(string path)
        {
            if (!System.IO.File.Exists(path))
            {
                throw new WorkflowParseException($"Workflow file not found: '{path}'");
            }

            var json = System.IO.File.ReadAllText(path);
            return Parse(json);
        }

        public static WorkflowDefinition Parse(string json)
        {
            JsonDocument document;
            try
            {
                document = JsonDocument.Parse(json);
            }
            catch (JsonException ex)
            {
                throw new WorkflowParseException($"Malformed workflow JSON: {ex.Message}");
            }

            using (document)
            {
                var root = document.RootElement;
                if (root.ValueKind != JsonValueKind.Object)
                {
                    throw new WorkflowParseException("Workflow JSON root must be an object.");
                }

                var definition = new WorkflowDefinition();

                if (root.TryGetProperty("name", out var nameElement) && nameElement.ValueKind == JsonValueKind.String)
                {
                    definition.Name = nameElement.GetString() ?? string.Empty;
                }

                if (root.TryGetProperty("app", out var appElement) && appElement.ValueKind == JsonValueKind.Object)
                {
                    definition.App = ParseAppConfig(appElement);
                }

                if (!root.TryGetProperty("steps", out var stepsElement) || stepsElement.ValueKind != JsonValueKind.Array)
                {
                    throw new WorkflowParseException("Workflow JSON must contain a 'steps' array.");
                }

                int index = 0;
                foreach (var stepElement in stepsElement.EnumerateArray())
                {
                    definition.Steps.Add(ParseStep(stepElement, index));
                    index++;
                }

                if (definition.Steps.Count == 0)
                {
                    throw new WorkflowParseException("Workflow must contain at least one step.");
                }

                Validate(definition);
                return definition;
            }
        }

        private static AppLaunchConfig ParseAppConfig(JsonElement element)
        {
            var config = new AppLaunchConfig();
            if (element.TryGetProperty("exePath", out var exe) && exe.ValueKind == JsonValueKind.String)
            {
                config.ExePath = exe.GetString() ?? string.Empty;
            }
            if (element.TryGetProperty("fixture", out var fixture) && fixture.ValueKind == JsonValueKind.String)
            {
                config.Fixture = fixture.GetString();
            }
            if (element.TryGetProperty("arguments", out var args) && args.ValueKind == JsonValueKind.String)
            {
                config.Arguments = args.GetString() ?? string.Empty;
            }
            return config;
        }

        private static WorkflowStep ParseStep(JsonElement element, int index)
        {
            if (element.ValueKind != JsonValueKind.Object)
            {
                throw new WorkflowParseException($"steps[{index}] must be an object.");
            }

            if (!element.TryGetProperty("type", out var typeElement) || typeElement.ValueKind != JsonValueKind.String)
            {
                throw new WorkflowParseException($"steps[{index}] is missing the required 'type' field.");
            }

            var typeRaw = typeElement.GetString() ?? string.Empty;
            if (!KnownStepTypes.TryGetValue(typeRaw, out var type))
            {
                var known = string.Join(", ", KnownStepTypes.Keys.OrderBy(k => k, StringComparer.OrdinalIgnoreCase));
                throw new WorkflowParseException($"steps[{index}] has unknown step type '{typeRaw}'. Known types: {known}.");
            }

            var step = new WorkflowStep { Type = type, TypeRaw = typeRaw };

            if (element.TryGetProperty("id", out var idElement) && idElement.ValueKind == JsonValueKind.String)
            {
                step.Id = idElement.GetString() ?? string.Empty;
            }

            if (string.IsNullOrWhiteSpace(step.Id))
            {
                step.Id = $"{typeRaw.ToLowerInvariant()}-{index}";
            }

            if (element.TryGetProperty("durationMs", out var duration) && duration.TryGetInt32(out var d))
            {
                step.DurationMs = d;
            }
            if (element.TryGetProperty("timeoutMs", out var timeout) && timeout.TryGetInt32(out var t))
            {
                step.TimeoutMs = t;
            }
            if (element.TryGetProperty("automationId", out var automationId) && automationId.ValueKind == JsonValueKind.String)
            {
                step.AutomationId = automationId.GetString() ?? string.Empty;
            }
            if (element.TryGetProperty("event", out var evt) && evt.ValueKind == JsonValueKind.String)
            {
                step.Event = evt.GetString() ?? string.Empty;
            }
            if (element.TryGetProperty("keys", out var keys) && keys.ValueKind == JsonValueKind.String)
            {
                step.Keys = keys.GetString() ?? string.Empty;
            }
            if (element.TryGetProperty("mode", out var mode) && mode.ValueKind == JsonValueKind.String)
            {
                step.Mode = mode.GetString() ?? string.Empty;
            }
            if (element.TryGetProperty("expected", out var expected) && expected.ValueKind == JsonValueKind.String)
            {
                step.Expected = expected.GetString() ?? "exists";
            }
            if (element.TryGetProperty("file", out var file) && file.ValueKind == JsonValueKind.String)
            {
                step.File = file.GetString() ?? string.Empty;
            }
            if (element.TryGetProperty("action", out var action) && action.ValueKind == JsonValueKind.String)
            {
                step.Action = action.GetString() ?? string.Empty;
            }

            return step;
        }

        /// <summary>
        /// Semantic validation: required fields per step type. Split from parsing so
        /// error messages name the step id, not just the array index.
        /// </summary>
        private static void Validate(WorkflowDefinition definition)
        {
            var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var step in definition.Steps)
            {
                if (!seenIds.Add(step.Id))
                {
                    throw new WorkflowParseException($"Duplicate step id '{step.Id}'.");
                }

                switch (step.Type)
                {
                    case StepType.WaitForState:
                        Require(step, s => !string.IsNullOrWhiteSpace(s.Event), "requires an 'event' field (e.g. 'menu-opened').");
                        break;
                    case StepType.Hotkey:
                        Require(step, s => !string.IsNullOrWhiteSpace(s.Keys), "requires a 'keys' field (e.g. 'Ctrl+Space').");
                        break;
                    case StepType.MenuOpen:
                        Require(step, s =>
                            string.IsNullOrWhiteSpace(s.Mode)
                            || s.Mode.Equals("action", StringComparison.OrdinalIgnoreCase)
                            || s.Mode.Equals("task", StringComparison.OrdinalIgnoreCase),
                            "requires 'mode' of 'action' or 'task' (defaults to 'action').");
                        break;
                    case StepType.Click:
                    case StepType.Assert:
                        Require(step, s => !string.IsNullOrWhiteSpace(s.AutomationId), "requires an 'automationId' field.");
                        break;
                    case StepType.Wait:
                        Require(step, s => s.DurationMs >= 0, "requires a non-negative 'durationMs'.");
                        break;
                    case StepType.Record:
                        Require(step, s =>
                            string.IsNullOrWhiteSpace(s.Action)
                            || s.Action.Equals("start", StringComparison.OrdinalIgnoreCase)
                            || s.Action.Equals("stop", StringComparison.OrdinalIgnoreCase),
                            "requires 'action' of 'start' or 'stop'.");
                        break;
                }
            }
        }

        private static void Require(WorkflowStep step, Func<WorkflowStep, bool> condition, string message)
        {
            if (!condition(step))
            {
                throw new WorkflowParseException($"Step '{step.Id}' ({step.TypeRaw}) {message}");
            }
        }
    }

    /// <summary>Thrown for any invalid workflow definition.</summary>
    public sealed class WorkflowParseException : Exception
    {
        public WorkflowParseException(string message) : base(message) { }
    }
}
