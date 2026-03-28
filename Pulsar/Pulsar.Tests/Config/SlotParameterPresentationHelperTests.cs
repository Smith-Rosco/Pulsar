using System.Collections.Generic;
using System.Linq;
using System;
using FluentAssertions;
using Pulsar.Core.Plugin.Metadata;
using Pulsar.Helpers;
using Pulsar.Plugins.Core.Pki.Models;
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

        [Fact]
        public void SecretSelector_ShouldExposeResolvedDisplayMetadata()
        {
            var slot = CreateSlot();
            var secretId = Guid.NewGuid();
            slot.SetArgument("secretId", secretId.ToString());

            var field = new SlotParameterEditorField(
                slot,
                new SlotParameterMetadata
                {
                    Key = "secretId",
                    Type = "guid",
                    Label = "Secret",
                    IsRequired = true,
                    PickerIntent = SlotPickerIntent.Secret,
                    SummaryMode = SlotParameterSummaryMode.SafeStateOnly,
                    MissingSummaryText = "not selected",
                    ConfiguredSummaryText = "selected"
                },
                _ => new SecretDisplayMetadata(secretId, "Payroll Vault", "ops@example.com"));

            field.IsReadOnlySelector.Should().BeTrue();
            field.DisplayValue.Should().Be("Payroll Vault");
            field.SecondaryDisplayValue.Should().Be("ops@example.com");
            field.SelectorActionLabel.Should().Be("Change");
        }

        [Fact]
        public void SecretMetadataResolver_ShouldPreferPendingSecretsAndFallbackLegacyLabel()
        {
            var secretId = Guid.NewGuid();
            var persisted = new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = "Old Label", Account = "saved@example.com", EncryptedData = "one" }
            };
            var pending = new Dictionary<Guid, SecretPayload>
            {
                [secretId] = new() { Label = string.Empty, Account = "draft@example.com", EncryptedData = "two" }
            };
            var legacyLabels = new Dictionary<Guid, string>
            {
                [secretId] = "Legacy Slot Label"
            };

            var display = SecretMetadataResolver.Resolve(secretId, persisted, pending, legacyLabels);

            display.Should().NotBeNull();
            display!.Label.Should().Be("Legacy Slot Label");
            display.Account.Should().Be("draft@example.com");
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
