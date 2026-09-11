// [Path]: Pulsar/Pulsar.Tests/Dialogs/DialogCatalogTests.cs

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Resources;
using System.Windows;
using FluentAssertions;
using Pulsar.Services;
using Pulsar.ViewModels.Base;
using Xunit;

namespace Pulsar.Tests.Dialogs
{
    /// <summary>
    /// Guards the dialog catalog (ADR-033): the single registration surface that
    /// replaced per-call-site title and size choices. Three legs must stay coherent
    /// for every row — the title key (exists in both resx), the size/button/theme
    /// preset, and the content type (has an implicit DataTemplate). A row that drifts
    /// from any of them would otherwise fail at runtime with no compile error: a
    /// missing resx key renders the key itself, a missing template renders the type
    /// name.
    /// </summary>
    public class DialogCatalogTests
    {
        /// <summary>
        /// Content types that legitimately have a DataTemplate but no catalog row,
        /// because they are shown through a dedicated API that owns its own title —
        /// they are not <c>ShowCustomAsync(dialogId, …)</c> dialogs.
        /// </summary>
        private static readonly HashSet<Type> NonCatalogContentTypes =
        [
            typeof(string),                                         // ShowMessageAsync / ShowConfirmationAsync body
            typeof(Pulsar.ViewModels.Dialogs.ColorPickerViewModel),  // ShowColorPickerAsync
            typeof(Pulsar.ViewModels.Dialogs.InputDialogViewModel),  // ShowInputAsync
        ];

        [Fact]
        public void Catalog_ShouldNotBeEmpty()
        {
            DialogCatalog.Registrations.Should().NotBeEmpty(
                "the catalog is the only way a dialog is shown; an empty catalog means no dialog is registered");
        }

        [Fact]
        public void DialogIds_ShouldBeUniqueAndNonEmpty()
        {
            var ids = DialogCatalog.Registrations.Select(registration => registration.Id.Value).ToList();

            ids.Should().OnlyHaveUniqueItems("a duplicate id would silently resolve to whichever row was added first");
            ids.Should().AllSatisfy(id => id.Should().NotBeNullOrWhiteSpace());
        }

        [Fact]
        public void EveryRegistration_ShouldBeWellFormed()
        {
            foreach (var registration in DialogCatalog.Registrations)
            {
                registration.TitleKey.Should().NotBeNullOrWhiteSpace($"{registration.Id} must name a title resource key");
                registration.SizeConstraints.Should().NotBeNull($"{registration.Id} must carry a size preset");
                typeof(IDialogViewModel).IsAssignableFrom(registration.ContentType).Should().BeTrue(
                    $"{registration.Id}'s content type {registration.ContentType.Name} must be a dialog view model");
            }
        }

        [Fact]
        public void Lookup_ShouldResolveEveryRegistration_AndRejectUnknownIds()
        {
            foreach (var registration in DialogCatalog.Registrations)
            {
                DialogCatalog.TryGet(registration.Id, out var found).Should().BeTrue();
                found.Should().BeSameAs(registration);
                DialogCatalog.GetRequired(registration.Id).Should().BeSameAs(registration);
            }

            var unknown = new DialogId("NotRegistered");
            DialogCatalog.TryGet(unknown, out _).Should().BeFalse();

            var act = () => DialogCatalog.GetRequired(unknown);
            act.Should().Throw<InvalidOperationException>().WithMessage("*NotRegistered*");
        }

        [Fact]
        public void RegisteredTitleKeys_ShouldExistInBothResx()
        {
            var missing = new List<string>();

            foreach (var culture in new[] { "en", "zh-CN" })
            {
                var keys = KeysIn(culture);
                missing.AddRange(DialogCatalog.Registrations
                    .Where(registration => !keys.Contains(registration.TitleKey))
                    .Select(registration => $"{culture}: {registration.Id} -> {registration.TitleKey}"));
            }

            missing.Should().BeEmpty(
                "every catalog title key must resolve in Strings.resx and Strings.zh-CN.resx; " +
                "a missing key renders the key itself as the dialog title");
        }

        [Fact]
        public void TitleFormatFlag_ShouldMatchTheResxValue()
        {
            var manager = new ResourceManager("Pulsar.Resources.Strings", typeof(Pulsar.Models.ProfilesConfig).Assembly);
            var mismatches = new List<string>();

            foreach (var culture in new[] { "en", "zh-CN" })
            {
                var info = CultureInfo.GetCultureInfo(culture);

                foreach (var registration in DialogCatalog.Registrations)
                {
                    var value = manager.GetString(registration.TitleKey, info);
                    if (value == null)
                    {
                        continue; // reported by RegisteredTitleKeys_ShouldExistInBothResx
                    }

                    var hasPlaceholder = value.Contains("{0}", StringComparison.Ordinal);
                    if (hasPlaceholder != registration.TitleIsFormat)
                    {
                        mismatches.Add(
                            $"{culture}: {registration.Id} (TitleIsFormat={registration.TitleIsFormat}) " +
                            $"-> \"{value}\"");
                    }
                }
            }

            mismatches.Should().BeEmpty(
                "a format flag that disagrees with the resx value either drops the argument silently " +
                "(no {0} in the value) or renders a literal '{0}' (flag false while the value has one)");
        }

        [Fact]
        public void EveryRegisteredContentType_ShouldHaveADialogTemplate()
        {
            var templates = LoadDialogTemplateTypes();

            var missing = DialogCatalog.Registrations
                .Where(registration => !templates.Contains(registration.ContentType))
                .Select(registration => $"{registration.Id} -> {registration.ContentType.Name}")
                .ToList();

            missing.Should().BeEmpty(
                "every catalog content type must have an implicit DataTemplate in Themes/DialogTemplates.xaml; " +
                "otherwise the dialog fails at runtime with 'No DataTemplate registered'");
        }

        [Fact]
        public void EveryDialogTemplate_ShouldBeRegisteredInTheCatalog()
        {
            var templates = LoadDialogTemplateTypes();
            var catalogTypes = DialogCatalog.Registrations.Select(registration => registration.ContentType).ToHashSet();

            var unregistered = templates
                .Where(type => !NonCatalogContentTypes.Contains(type))
                .Where(type => !catalogTypes.Contains(type))
                .Select(type => type.Name)
                .ToList();

            unregistered.Should().BeEmpty(
                "a dialog template with neither a catalog row nor an exemption is a dialog whose title and size " +
                "are being chosen at the call site again — add a row in Services/DialogCatalog.cs, or an exemption here " +
                "if it is shown through a dedicated API");
        }

        private static HashSet<Type> LoadDialogTemplateTypes()
        {
            HashSet<Type>? registered = null;

            // Loading a ResourceDictionary goes through the WPF XAML reader; run it on
            // an STA thread for the same reason DialogMessageOverflowGuardTests does.
            StaTestRunner.RunInSta(() =>
            {
                var templates = (ResourceDictionary)Application.LoadComponent(
                    new Uri("Pulsar;component/Themes/DialogTemplates.xaml", UriKind.Relative));

                registered = templates.Values.OfType<DataTemplate>()
                    .Select(template => template.DataType)
                    .OfType<Type>()
                    .ToHashSet();
            });

            return registered!;
        }

        private static HashSet<string> KeysIn(string culture)
        {
            var manager = new ResourceManager("Pulsar.Resources.Strings", typeof(Pulsar.Models.ProfilesConfig).Assembly);

            using var set = manager.GetResourceSet(CultureInfo.GetCultureInfo(culture), true, true)!;
            return set.Cast<System.Collections.DictionaryEntry>()
                .Select(entry => (string)entry.Key)
                .ToHashSet(StringComparer.Ordinal);
        }
    }
}
