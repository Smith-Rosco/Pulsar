using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows.Markup;
using FluentAssertions;
using Pulsar.Views.Controls;
using Xunit;

namespace Pulsar.Tests.Views.Controls
{
    public class SlotWheelEditorSmokeTests
    {
        [Fact]
        public void InitializeComponent_ResolvesSlotStyles_WithoutResourceErrors()
        {
            XamlParseException? resourceError = null;
            RunInSta(() =>
            {
                try
                {
                    _ = new SlotWheelEditor();
                }
                catch (XamlParseException ex)
                {
                    resourceError = ex;
                }
                catch (Exception ex) when (ex is NullReferenceException or InvalidCastException)
                {
                    // InitializeComponent succeeded (resources resolved); the ctor then
                    // touches App.Current.Services, which requires a running Pulsar.App.
                    // Under parallel xUnit, Application.Current may be a plain WPF
                    // Application, so tolerate environment-dependent failures here.
                }
            });

            resourceError.Should().BeNull(
                "SlotWheelEditor must resolve SlotStyles.xaml resources at construction time");
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
