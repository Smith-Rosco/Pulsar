using System.Collections.Generic;
using System.Linq;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Models;

namespace Pulsar.Helpers
{
    public static class SlotParameterPresentationHelper
    {
        public static IReadOnlyList<SlotParameterEditorField> BuildQuickEditParameters(IReadOnlyList<SlotParameterEditorField> parameters)
        {
            var explicitQuickEdit = parameters
                .Where(parameter => parameter.PreferQuickEdit)
                .OrderByDescending(parameter => parameter.QuickEditPriority)
                .ThenBy(parameter => parameter.Label)
                .ToList();

            if (explicitQuickEdit.Count > 0)
            {
                return explicitQuickEdit;
            }

            return parameters
                .Where(parameter => !parameter.IsDialogOnly && parameter.Metadata.Group != SlotParameterGroup.Advanced)
                .OrderByDescending(parameter => parameter.IsRequired)
                .ThenByDescending(parameter => parameter.HasPicker)
                .ThenByDescending(parameter => parameter.QuickEditPriority)
                .ThenBy(parameter => parameter.Label)
                .Take(2)
                .ToList();
        }

        public static IReadOnlyList<string> BuildSummaryTokens(IReadOnlyList<SlotParameterEditorField> parameters, string validationSummary)
        {
            var tokens = new List<string>();

            var warningToken = BuildWarningSummaryToken(parameters, validationSummary);
            if (!string.IsNullOrWhiteSpace(warningToken))
            {
                tokens.Add(warningToken);
            }

            tokens.AddRange(parameters
                .Where(parameter => parameter.IsRequired || parameter.HasValue)
                .OrderByDescending(parameter => parameter.IsRequired)
                .ThenByDescending(parameter => parameter.QuickEditPriority)
                .ThenBy(parameter => parameter.Label)
                .Select(parameter => parameter.SummaryToken)
                .Where(token => !string.IsNullOrWhiteSpace(token))
                .Take(3));

            return tokens;
        }

        public static string BuildWarningSummaryToken(IReadOnlyList<SlotParameterEditorField> parameters, string validationSummary)
        {
            if (!string.IsNullOrWhiteSpace(validationSummary))
            {
                return "Warning: incomplete";
            }

            int missingRequired = parameters.Count(parameter => parameter.IsRequired && !parameter.HasValue);
            if (missingRequired > 0)
            {
                return missingRequired == 1
                    ? "1 required field missing"
                    : $"{missingRequired} required fields missing";
            }

            return string.Empty;
        }
    }
}
