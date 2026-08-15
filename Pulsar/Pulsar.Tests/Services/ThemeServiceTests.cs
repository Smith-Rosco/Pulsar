// [Path]: Pulsar.Tests/Services/ThemeServiceTests.cs

using System;
using System.Linq;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Controls;
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

        private static void RunInSta(Action action)
        {
            Exception? capturedException = null;
            using var completed = new ManualResetEventSlim(false);

            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completed.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            completed.Wait();
            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
