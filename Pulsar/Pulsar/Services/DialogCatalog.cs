// [Path]: Pulsar/Pulsar/Services/DialogCatalog.cs

using System;
using System.Collections.Generic;
using System.Linq;
using Pulsar.Models;
using Pulsar.Models.Enums;
using Pulsar.ViewModels.Dialogs;
using Wpf.Ui.Appearance;

namespace Pulsar.Services
{
    /// <summary>
    /// Identity of one dialog registration in <see cref="DialogCatalog"/>.
    /// A distinct type rather than a raw <see cref="string"/> so a dialog id can
    /// never be passed where a display title is expected: the <c>string</c>-keyed
    /// and <see cref="DialogId"/>-keyed <c>ShowCustomAsync</c> overloads can never
    /// be ambiguous, and the compiler rejects a swap.
    /// </summary>
    public readonly record struct DialogId(string Value)
    {
        public override string ToString() => Value;
    }

    /// <summary>
    /// The single source of dialog ids. Every dialog shown through
    /// <c>IDialogService.ShowCustomAsync(dialogId, …)</c> is named here; call sites
    /// reference these constants instead of hand-writing ids at each site.
    /// </summary>
    public static class DialogIds
    {
        // --- Settings: slot, secret and profile editing ---

        /// <summary>"Secret Configuration" — the quick-secrets editor for a new slot.</summary>
        public static readonly DialogId AddSecretToSlot = new("AddSecretToSlot");

        /// <summary>"Edit Secret" — the quick-secrets editor for an existing slot.</summary>
        public static readonly DialogId EditSecretInSlot = new("EditSecretInSlot");

        /// <summary>"Select Secret" — the secret picker (existing or new).</summary>
        public static readonly DialogId PickSecret = new("PickSecret");

        /// <summary>"New Profile" — process-profile editor for a new profile.</summary>
        public static readonly DialogId AddProfile = new("AddProfile");

        /// <summary>"Edit Profile" — process-profile editor for an existing profile.</summary>
        public static readonly DialogId EditProfile = new("EditProfile");

        /// <summary>
        /// "Select Application" — the process picker, shared by the Settings slot list
        /// and the profile editors (both show it resizable / maximizable).
        /// </summary>
        public static readonly DialogId PickProcess = new("PickProcess");

        /// <summary>"Select Icon" — the icon picker (shared by the slot list and both profile editors).</summary>
        public static readonly DialogId PickIcon = new("PickIcon");

        // --- Secret picker: nested secret create / edit ---

        /// <summary>"Add Secret" — quick-secrets editor opened from inside the secret picker.</summary>
        public static readonly DialogId AddSecret = new("AddSecret");

        /// <summary>"Edit Secret" — quick-secrets editor opened from inside the secret picker.</summary>
        public static readonly DialogId EditSecret = new("EditSecret");

        // --- Plugin surfaces ---

        /// <summary>"Plugin Logs: {0}" — the plugin log viewer (Settings plugin list and analytics drill-down).</summary>
        public static readonly DialogId PluginLogs = new("PluginLogs");

        /// <summary>"Plugin Details" — the analytics drill-down for one plugin.</summary>
        public static readonly DialogId PluginAnalyticsDetails = new("PluginAnalyticsDetails");

        /// <summary>"Example Library" — the built-in bookmarklet example browser.</summary>
        public static readonly DialogId ExampleLibrary = new("ExampleLibrary");

        /// <summary>"Script Editor" — the in-app bookmarklet script editor.</summary>
        public static readonly DialogId ScriptEditor = new("ScriptEditor");

        /// <summary>"Process Blacklist" — the custom configuration dialog for the window switcher.</summary>
        public static readonly DialogId ProcessBlacklist = new("ProcessBlacklist");

        /// <summary>"Configure {0}" — the schema-driven plugin settings dialog.</summary>
        public static readonly DialogId PluginSettings = new("PluginSettings");

        /// <summary>"Window Inspector" — the window-discovery inspector opened from plugin settings.</summary>
        public static readonly DialogId WindowInspector = new("WindowInspector");

        // --- About: configuration backup ---

        /// <summary>"Export Options" — export options for a configuration backup.</summary>
        public static readonly DialogId ExportOptions = new("ExportOptions");

        /// <summary>"Backup Password" — the password prompt when importing a protected backup.</summary>
        public static readonly DialogId BackupPassword = new("BackupPassword");

        // --- Startup ---

        /// <summary>"Welcome to Pulsar" — the first-run setup wizard (light theme, forced).</summary>
        public static readonly DialogId FirstLaunchSetup = new("FirstLaunchSetup");
    }

    /// <summary>
    /// One row of the dialog catalog: everything a dialog needs except its content
    /// instance. The row — not the call site — owns the title key, the size preset,
    /// the button set and the theme override, so no two call sites can drift apart
    /// on the same dialog, and a new dialog cannot be added without a row.
    /// </summary>
    /// <param name="Id">Catalog key; see <see cref="DialogIds"/>.</param>
    /// <param name="TitleKey">Resource key of the dialog title.</param>
    /// <param name="ContentType">The dialog view-model type, which must have an implicit DataTemplate in <c>Themes/DialogTemplates.xaml</c>.</param>
    /// <param name="SizeConstraints">
    /// Size template applied to the dialog window. Treated as read-only: the window
    /// copies the values (<c>DialogService.ApplySizeConstraints</c>) and never
    /// mutates this instance.
    /// </param>
    /// <param name="Buttons">Button set the dialog is shown with.</param>
    /// <param name="ThemeOverride">Forced theme, or <c>null</c> to infer from context.</param>
    public sealed record DialogRegistration(
        DialogId Id,
        string TitleKey,
        Type ContentType,
        DialogSizeConstraints SizeConstraints,
        DialogButtons Buttons = DialogButtons.OkCancel,
        AppTheme? ThemeOverride = null)
    {
        /// <summary>
        /// True when <see cref="TitleKey"/> resolves to a composite format string that
        /// consumes the caller's title arguments (e.g. the plugin name); false when the
        /// localized value is used verbatim and any supplied arguments are ignored.
        /// This drives <c>DialogService</c>'s title resolution, and is pinned against the
        /// actual resx value by
        /// <c>DialogCatalogTests.TitleFormatFlag_ShouldMatchTheResxValue</c> — so a
        /// translator dropping a <c>{0}</c> placeholder fails the build instead of
        /// silently dropping the argument from the dialog title.
        /// </summary>
        public bool TitleIsFormat { get; init; }
    }

    /// <summary>
    /// The registration surface for modal dialog content. Replaces the previous
    /// arrangement in which each of ~20 call sites independently chose a title key
    /// and a size preset, with no guard tying either to the content type.
    ///
    /// <para>
    /// Scope: dialogs shown through <see cref="Interfaces.IDialogService.ShowCustomAsync{TViewModel}(DialogId, TViewModel, object[])"/>
    /// — i.e. view-model content. The message / confirmation / input / colour dialogs
    /// have dedicated, title-less-by-design APIs (<c>ShowMessageAsync</c>,
    /// <c>ShowConfirmationAsync</c>, <c>ShowInputAsync</c>, <c>ShowColorPickerAsync</c>)
    /// and deliberately carry no row here.
    /// </para>
    ///
    /// <para>
    /// Static by design: the set is compile-time data with no runtime registration, in
    /// contrast with <see cref="SettingsPageCatalog"/>, which must accept transient pages
    /// at runtime.
    /// </para>
    /// </summary>
    public static class DialogCatalog
    {
        private static readonly IReadOnlyList<DialogRegistration> _registrations =
        [
            // --- Settings: slot, secret and profile editing ---
            new(DialogIds.AddSecretToSlot, "Notification.SecretConfiguration", typeof(QuickSecretsViewModel), DialogSizeConstraints.Medium),
            new(DialogIds.EditSecretInSlot, "Notification.EditSecret", typeof(QuickSecretsViewModel), DialogSizeConstraints.Medium),
            new(DialogIds.PickSecret, "Notification.SelectSecret", typeof(SecretPickerViewModel), DialogSizeConstraints.Medium, DialogButtons.None),
            new(DialogIds.AddProfile, "Notification.NewProfile", typeof(InputProfileViewModel), DialogSizeConstraints.Medium),
            new(DialogIds.EditProfile, "Notification.EditProfile", typeof(EditProfileViewModel), DialogSizeConstraints.Medium),
            new(DialogIds.PickProcess, "Notification.SelectApplication", typeof(ProcessPickerViewModel), DialogSizeConstraints.LargeResizable),
            new(DialogIds.PickIcon, "Notification.SelectIcon", typeof(IconPickerViewModel), DialogSizeConstraints.LargeResizable),

            // --- Secret picker: nested secret create / edit ---
            new(DialogIds.AddSecret, "Dialog.SecretPicker.AddSecret", typeof(QuickSecretsViewModel), DialogSizeConstraints.Medium),
            new(DialogIds.EditSecret, "Dialog.SecretPicker.EditSecret", typeof(QuickSecretsViewModel), DialogSizeConstraints.Medium),

            // --- Plugin surfaces ---
            new(DialogIds.PluginLogs, "Notification.PluginLogsTitleFormat", typeof(PluginLogViewerViewModel), DialogSizeConstraints.Large, DialogButtons.Ok)
            {
                TitleIsFormat = true
            },
            new(DialogIds.PluginAnalyticsDetails, "Dialog.PluginAnalyticsDetail.Title", typeof(PluginAnalyticsDetailViewModel), DialogSizeConstraints.Large, DialogButtons.Ok),
            new(DialogIds.ExampleLibrary, "ExampleLibrary.Title", typeof(ExampleLibraryViewModel),
                new DialogSizeConstraints
                {
                    Width = 620, Height = 480, MinWidth = 520, MinHeight = 400, MaxWidth = 900, MaxHeight = 700,
                    AllowResize = true, ShowMaximizeButton = true
                }, DialogButtons.None),
            new(DialogIds.ScriptEditor, "Bookmarklet.ScriptEditor.Title", typeof(BookmarkletScriptEditorViewModel),
                new DialogSizeConstraints
                {
                    Width = 720, Height = 560, MinWidth = 560, MinHeight = 420, MaxWidth = 1200, MaxHeight = 900,
                    AllowResize = true, ShowMaximizeButton = true
                }, DialogButtons.None),
            new(DialogIds.ProcessBlacklist, "Notification.ProcessBlacklistTitle", typeof(ProcessBlacklistViewModel), DialogSizeConstraints.Medium),
            new(DialogIds.PluginSettings, "Notification.ConfigureTitleFormat", typeof(PluginSettingsDialogViewModel),
                new DialogSizeConstraints { Width = 550, Height = 500, MinWidth = 400, MinHeight = 300 }, DialogButtons.None)
            {
                TitleIsFormat = true
            },
            new(DialogIds.WindowInspector, "Inspector.Title", typeof(WindowInspectorViewModel),
                new DialogSizeConstraints { Width = 780, Height = 560, MinWidth = 600, MinHeight = 400 }, DialogButtons.None),

            // --- About: configuration backup ---
            new(DialogIds.ExportOptions, "Settings.About.ExportOptionsTitle", typeof(ConfigBackupOptionsViewModel), DialogSizeConstraints.Small),
            new(DialogIds.BackupPassword, "Settings.About.BackupPasswordTitle", typeof(ConfigBackupOptionsViewModel), DialogSizeConstraints.Small),

            // --- Startup ---
            new(DialogIds.FirstLaunchSetup, "FirstLaunch.SetupTitle", typeof(FirstLaunchSetupWizardViewModel), DialogSizeConstraints.LargeResizable, DialogButtons.None, AppTheme.Light)
        ];

        private static readonly IReadOnlyDictionary<DialogId, DialogRegistration> _byId =
            _registrations.ToDictionary(registration => registration.Id);

        /// <summary>Every registration, in declaration order.</summary>
        public static IReadOnlyList<DialogRegistration> Registrations => _registrations;

        public static bool TryGet(DialogId id, out DialogRegistration registration)
            => _byId.TryGetValue(id, out registration!);

        /// <summary>
        /// The registration for <paramref name="id"/>, or an
        /// <see cref="InvalidOperationException"/> naming the missing id — the same
        /// fail-loud stance <c>DialogService</c> takes for a missing DataTemplate.
        /// </summary>
        public static DialogRegistration GetRequired(DialogId id)
            => _byId.TryGetValue(id, out var registration)
                ? registration
                : throw new InvalidOperationException(
                    $"Dialog id '{id}' is not registered in DialogCatalog. " +
                    "Add a row in Services/DialogCatalog.cs.");
    }
}
