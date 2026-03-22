using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Models;
using Xunit;

namespace Pulsar.Tests.Config
{
    public class SlotParameterPresentationHelperTests
    {
        [Fact]
        public void BuildQuickEditParameters_ShouldPreferExplicitQuickEditHints()
        {
            var slot = CreateSlot();
            var fields = new List<SlotParameterEditorField>
            {
                CreateField(slot, "path", SlotParameterPresentationHint.QuickEdit, 100),
                CreateField(slot, "delay", SlotParameterPresentationHint.QuickEdit, 20),
                CreateField(slot, "arguments", SlotParameterPresentationHint.DialogOnly, 0)
            };

            var quickEdit = SlotParameterPresentationHelper.BuildQuickEditParameters(fields);

            quickEdit.Select(field => field.Key).Should().Equal("path", "delay");
        }

        [Fact]
        public void BuildQuickEditParameters_ShouldFallbackToTopNonAdvancedFields()
        {
            var slot = CreateSlot();
            var fields = new List<SlotParameterEditorField>
            {
                CreateField(slot, "requiredA", SlotParameterPresentationHint.Auto, 0, isRequired: true),
                CreateField(slot, "optionalA", SlotParameterPresentationHint.Auto, 0),
                CreateField(slot, "advancedA", SlotParameterPresentationHint.Auto, 0, group: SlotParameterGroup.Advanced)
            };

            var quickEdit = SlotParameterPresentationHelper.BuildQuickEditParameters(fields);

            quickEdit.Select(field => field.Key).Should().Equal("requiredA", "optionalA");
        }

        [Fact]
        public void BuildSummaryTokens_ShouldSurfaceWarningAndSafeFallbackForSensitiveFields()
        {
            var slot = CreateSlot();
            var sensitiveField = new SlotParameterEditorField(slot, new SlotParameterMetadata
            {
                Key = "secretId",
                Type = "guid",
                Label = "Secret",
                IsRequired = true,
                IsSensitive = true,
                SummaryLabel = "Secret",
                ConfiguredSummaryText = "selected",
                MissingSummaryText = "not selected"
            });

            var tokens = SlotParameterPresentationHelper.BuildSummaryTokens(new[] { sensitiveField }, "");

            tokens.Should().Contain("1 required field missing");
            tokens.Should().Contain("Secret: not selected");
        }

        private static PluginSlot CreateSlot()
        {
            return new PluginSlot
            {
                Slot = 1,
                PluginId = "com.test.plugin",
                Action = "run",
                Label = "Test Slot",
                Args = new Dictionary<string, string>()
            };
        }

        private static SlotParameterEditorField CreateField(
            PluginSlot slot,
            string key,
            SlotParameterPresentationHint presentationHint,
            int quickEditPriority,
            bool isRequired = false,
            SlotParameterGroup group = SlotParameterGroup.Optional)
        {
            return new SlotParameterEditorField(slot, new SlotParameterMetadata
            {
                Key = key,
                Type = "string",
                Label = key,
                IsRequired = isRequired,
                Group = group,
                PresentationHint = presentationHint,
                QuickEditPriority = quickEditPriority,
                SummaryLabel = key,
                SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                ConfiguredSummaryText = "configured",
                MissingSummaryText = "missing"
            });
        }
    }
}
