// [Path]: Pulsar/Pulsar/Core/Plugin/Metadata/SlotParameterSpecs.cs

using System.Collections.Generic;

namespace Pulsar.Core.Plugin.Metadata
{
    /// <summary>
    /// Canonical specs for the slot-parameter shapes that more than one built-in plugin
    /// declares. The <em>structural</em> fields of a shape — Type, Group, SummaryMode and
    /// its summary texts, PresentationHint, QuickEditPriority, PickerIntent, validators —
    /// are decided once here; only the prose a user reads (Label, Description, Placeholder,
    /// Example, InputHint, ValidationHint) is passed in. Two plugins declaring the same
    /// shape therefore cannot disagree about what the slot editor does with it.
    ///
    /// [Architecture review 2026-09-11, candidate #2] Previously WinSwitcher's canonical
    /// spec factories were <c>private static</c> inside that plugin, so the same shapes were
    /// hand-written again in Command / VbaRunner / Bookmarklet — 14 blocks in total — and the
    /// anti-drift guard only covered WinSwitcher. Only shapes with two or more real consumers
    /// live here: WinSwitcher's app / fallback-path / executable-path variants stay private
    /// to that plugin, because extracting a single-consumer spec would add a seam that
    /// nothing varies across.
    ///
    /// Localization note: <c>Label</c> drives the <c>SlotParam.{AlphaNumOnly(Label)}</c>
    /// resx convention key, so rewording a default Label here changes which translation is
    /// looked up. Read <see cref="FilePathParameter"/> / <see cref="ArgumentsParameter"/>
    /// before overriding one.
    /// </summary>
    public static class SlotParameterSpecs
    {
        /// <summary>The parameter key shared by the file-path shape.</summary>
        public const string ScriptPathKey = "scriptPath";

        /// <summary>The parameter key shared by the command-line arguments shape.</summary>
        public const string ArgumentsKey = "arguments";

        /// <summary>
        /// A required quick-edit file path (<c>scriptPath</c>). Consumers: VbaRunner, Bookmarklet.
        /// </summary>
        public static SlotParameterMetadata FilePathParameter(
            string description,
            string placeholder,
            string example,
            string inputHint,
            string validationHint) => new()
        {
            Key = ScriptPathKey,
            Type = "string",
            Label = "Script File",
            Description = description,
            IsRequired = true,
            Group = SlotParameterGroup.Required,
            SummaryLabel = "Script",
            SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
            ConfiguredSummaryText = "file ready",
            MissingSummaryText = "file missing",
            PresentationHint = SlotParameterPresentationHint.QuickEdit,
            QuickEditPriority = 100,
            Placeholder = placeholder,
            Example = example,
            InputHint = inputHint,
            ValidationHint = validationHint,
            PickerIntent = SlotPickerIntent.File,
            Validators = new List<ValidationRule> { new RequiredValidator() }
        };

        /// <summary>
        /// An optional command-line arguments string (<c>arguments</c>). Consumers: WinSwitcher
        /// switch + launch, Command run. <paramref name="hint"/> feeds both InputHint and
        /// ValidationHint, which is how WinSwitcher already used it; pass null for no hints.
        /// </summary>
        public static SlotParameterMetadata ArgumentsParameter(
            SlotParameterGroup group,
            string description,
            string placeholder,
            string label = "Launch Arguments",
            string example = "--new-window https://example.com",
            string? hint = null) => new()
        {
            Key = ArgumentsKey,
            Type = "string",
            Label = label,
            Description = description,
            IsRequired = false,
            Group = group,
            SummaryLabel = "Args",
            SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
            ConfiguredSummaryText = "args set",
            MissingSummaryText = "no args",
            PresentationHint = SlotParameterPresentationHint.DialogOnly,
            Placeholder = placeholder,
            Example = example,
            InputHint = hint,
            ValidationHint = hint
        };
    }
}
