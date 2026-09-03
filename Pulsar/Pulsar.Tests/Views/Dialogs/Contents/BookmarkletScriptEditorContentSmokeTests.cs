using System;
using System.Windows;
using System.Windows.Markup;
using FluentAssertions;
using Pulsar.Views.Dialogs.Contents;
using Xunit;

namespace Pulsar.Tests.Views.Dialogs.Contents
{
    public class BookmarkletScriptEditorContentSmokeTests
    {
        [Fact]
        public void InitializeComponent_ResolvesPulsarButtonStyles_WithoutXamlParseException()
        {
            // Regression: importing an example opened the in-app script editor and the
            // app crashed with "cannot find resource PulsarSecondaryButtonStyle".
            // BookmarkletScriptEditorContent.xaml referenced PulsarSecondaryButtonStyle
            // via StaticResource, but it did NOT merge ButtonStyles.xaml into its own
            // resources. A UserControl's StaticResource is resolved during
            // InitializeComponent (before the control is attached to the dialog
            // window's tree), so the lookup only sees the control's own resources +
            // Application resources — and App.xaml deliberately loads no
            // ButtonStyles.xaml. Every other dialog content control (IconPicker,
            // WindowInspector, SecretPicker, ...) merges ButtonStyles.xaml locally.
            // Constructing the control, under the app's real Application resources,
            // must not throw XamlParseException.
            XamlParseException? resourceError = null;
            StaTestRunner.RunInSta(() =>
            {
                // Reproduce the app's global resource environment: App.xaml merges
                // Tokens.xaml (which provides Pulsar.Gap.* / Pulsar.Type.*) but NOT
                // ButtonStyles.xaml. With Tokens resolvable, the parse reaches the
                // actual crash site: PulsarSecondaryButtonStyle.
                if (Application.Current == null)
                {
                    var app = new Application();
                    var global = new ResourceDictionary();
                    global.MergedDictionaries.Add(new ResourceDictionary
                    {
                        Source = new Uri("pack://application:,,,/Pulsar;component/Styles/Tokens.xaml", UriKind.Absolute)
                    });
                    app.Resources = global;
                }

                try
                {
                    _ = new BookmarkletScriptEditorContent();
                }
                catch (XamlParseException ex)
                {
                    resourceError = ex;
                }
                catch (Exception ex) when (ex is InvalidCastException or NullReferenceException)
                {
                    // InitializeComponent succeeded (resources resolved); the ctor then
                    // reads App.Current.Services, which requires a running Pulsar.App
                    // (same guard as EmptyStateSmokeTests).
                }
            });

            resourceError.Should().BeNull(
                "BookmarkletScriptEditorContent must self-merge ButtonStyles.xaml so PulsarSecondaryButtonStyle resolves at construction");
        }
    }
}
