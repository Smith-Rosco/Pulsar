// [Path]: Pulsar.Tests/Services/ThemeServiceTests.cs

using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Pulsar.Models;
using Pulsar.Services;
using Wpf.Ui.Appearance;
using Wpf.Ui.Markup;
using Xunit;

namespace Pulsar.Tests.Services
{
    public class ThemeServiceTests
    {
        [Fact]
        public void DefaultTheme_ShouldMatchFirstLaunchConfiguration()
        {
            var service = new ThemeService(NullLogger<ThemeService>.Instance);

            service.CurrentTheme.Should().Be(AppTheme.Light);
        }

        [Fact]
        public void Initialize_ShouldSetPersistedTheme()
        {
            var service = new ThemeService(NullLogger<ThemeService>.Instance);

            service.Initialize(AppTheme.Dark);

            service.CurrentTheme.Should().Be(AppTheme.Dark);
        }

        [Fact]
        public void Initialize_ShouldRaiseThemeChanged_WhenThemeChanges()
        {
            var service = new ThemeService(NullLogger<ThemeService>.Instance);
            AppTheme? received = null;
            service.ThemeChanged += (_, theme) => received = theme;

            service.Initialize(AppTheme.Dark);

            received.Should().Be(AppTheme.Dark);
        }

        [Fact]
        public void SetGlobalTheme_ShouldUpdateRuntimeTheme_AndRaiseOnce()
        {
            var service = new ThemeService(NullLogger<ThemeService>.Instance);
            var raisedCount = 0;
            service.ThemeChanged += (_, _) => raisedCount++;

            service.SetGlobalTheme(AppTheme.Dark);
            service.SetGlobalTheme(AppTheme.Dark);

            service.CurrentTheme.Should().Be(AppTheme.Dark);
            raisedCount.Should().Be(1);
        }

        [Fact]
        public void ApplyTheme_ShouldNotChangeRuntimeTheme_OrRaiseThemeChanged()
        {
            RunInSta(() =>
            {
                var service = new ThemeService(NullLogger<ThemeService>.Instance);
                var raisedCount = 0;
                service.ThemeChanged += (_, _) => raisedCount++;

                service.ApplyTheme(new FrameworkElement(), AppTheme.Dark);

                service.CurrentTheme.Should().Be(AppTheme.Light);
                raisedCount.Should().Be(0);
            });
        }

        [Fact]
        public void ApplyContextMenuTheme_ShouldInjectAndUpdateWpfUiDictionaries()
        {
            RunInSta(() =>
            {
                var service = new ThemeService(NullLogger<ThemeService>.Instance);
                service.Initialize(AppTheme.Light);

                var menu = new ContextMenu();
                service.ApplyContextMenuTheme(menu, AppTheme.Light);

                var themeDictionary = menu.Resources.MergedDictionaries.OfType<ThemesDictionary>().Should().ContainSingle().Which;
                themeDictionary.Should().NotBeNull("ThemesDictionary is write-only, so verify injection structurally");
                menu.Resources.MergedDictionaries.OfType<ControlsDictionary>().Should().ContainSingle();

                service.ApplyContextMenuTheme(menu, AppTheme.Dark);

                menu.Resources.MergedDictionaries.OfType<ThemesDictionary>().Should().ContainSingle("theme dictionary should be updated in place");
                menu.Resources.MergedDictionaries.OfType<ControlsDictionary>().Should().ContainSingle("controls dictionary should not be duplicated");
            });
        }

        [Fact]
        public void Initialize_ShouldMakeAccentBrushesResolvableAtApplicationLevel()
        {
            RunInSta(() =>
            {
                // WPF allows only one Application per AppDomain; reuse one if a sibling test
                // already created it (same guard as SettingsSaveSessionTests / DirtyStateTests).
                if (Application.Current == null)
                {
                    _ = new Application();
                }

                var app = Application.Current!;
                var service = new ThemeService(NullLogger<ThemeService>.Instance);

                service.Initialize(AppTheme.Light);

                // Wpf.Ui's accent manager injects these into UiApplication.Current.Resources, which
                // for a plain System.Windows.Application (no "wpf.ui;" dictionary merged at App level)
                // is a detached dictionary never reached by resource lookup. ThemeService must bridge
                // them into Application.Current.Resources so every {DynamicResource Accent*} reference
                // (button fills, hover states, nav indicator, plugin card borders) resolves.
                app.Resources["AccentFillColorDefaultBrush"].Should().NotBeNull(
                    "primary button fills resolve from AccentFillColorDefaultBrush");
                app.Resources["AccentFillColorSecondaryBrush"].Should().NotBeNull(
                    "hover fills resolve from AccentFillColorSecondaryBrush");
                app.Resources["SystemAccentColor"].Should().NotBeNull(
                    "the accent colour itself must be available at Application level");

                var accentFill = app.Resources["AccentFillColorDefaultBrush"] as SolidColorBrush;
                accentFill.Should().NotBeNull();
                accentFill!.Color.Should().NotBe(Colors.Transparent,
                    "the accent fill must be a real colour, not a silently-missing fallback");
            });
        }

        private static void RunInSta(Action action) => StaTestRunner.RunInSta(action);
    }
}
