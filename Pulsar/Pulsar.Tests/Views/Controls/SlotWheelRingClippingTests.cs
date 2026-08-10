using System;
using System.Runtime.ExceptionServices;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using FluentAssertions;
using Xunit;

namespace Pulsar.Tests.Views.Controls
{
    /// <summary>
    /// Regression guard for the wheel editor: WPF Shapes (Ellipse) get clipped to the
    /// arrange size when placed in a fixed-size Grid cell, so slot rings must live in a
    /// container large enough (ContainerSize) instead of overflowing a Size×Size box.
    /// </summary>
    public class SlotWheelRingClippingTests
    {
        [Fact]
        public void EnlargedContainer_RendersRingStroke_WhereSmallContainerWasClipped()
        {
            RunInSta(() =>
            {
                // 8-slot layout: SlotSize ~= 52, ring = Size*1.3 = 67.6, container = Size*1.5 = 78.
                byte[] RenderTopOfRing(double containerSize)
                {
                    var grid = new Grid { Width = containerSize, Height = containerSize, Background = Brushes.White };
                    var ring = new Ellipse
                    {
                        Width = 67.6, Height = 67.6,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Stroke = new SolidColorBrush(Colors.Red), StrokeThickness = 2
                    };
                    grid.Children.Add(ring);

                    var host = new Canvas();
                    host.Children.Add(grid);
                    Canvas.SetLeft(grid, 10); Canvas.SetTop(grid, 10);

                    host.Measure(new Size(120, 120));
                    host.Arrange(new Rect(0, 0, 120, 120));

                    var bmp = new RenderTargetBitmap(120, 120, 96, 96, PixelFormats.Pbgra32);
                    bmp.Render(host);

                    // Ring center = (10 + container/2, 10 + container/2); stroke top ≈ center.Y - 32.8.
                    double center = 10 + containerSize / 2;
                    int sampleY = (int)Math.Round(center - 32.8);
                    var px = new byte[4];
                    bmp.CopyPixels(new Int32Rect((int)Math.Round(center), sampleY, 1, 1), px, 4, 0);
                    return px;
                }

                var clipped = RenderTopOfRing(52);
                var intact = RenderTopOfRing(78);

                // Small container: ring top is clipped away (transparent).
                clipped[2].Should().BeLessThan(100, "ring must be clipped inside a too-small container");
                // Enlarged container: red stroke visible at the ring's top (B channel high in PBGRA).
                intact[2].Should().BeGreaterThan(100, "ring must render fully inside the enlarged container");
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
