using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace Pulsar.Tests
{
    /// <summary>
    /// Runs a WPF-bound action on a dedicated STA thread with a bounded timeout.
    /// Replaces the historical per-file RunInSta copies (which blocked forever on
    /// <c>ManualResetEventSlim.Wait()</c> / <c>Thread.Join()</c>): a dispatcher or
    /// <see cref="System.Windows.Application"/> deadlock now fails the test with a
    /// <see cref="TimeoutException"/> instead of hanging the whole (single-threaded)
    /// test run for hours. Because xUnit forces <c>maxParallelThreads: 1</c>, one
    /// hung test used to block every subsequent test.
    /// </summary>
    public static class StaTestRunner
    {
        private static readonly TimeSpan DefaultTimeout = TimeSpan.FromSeconds(30);

        public static void RunInSta(Action action)
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

            if (!completed.Wait(DefaultTimeout))
            {
                throw new TimeoutException(
                    "WPF STA action did not complete within " + DefaultTimeout +
                    "; likely a Dispatcher/Application.Current deadlock on the STA thread");
            }

            thread.Join();

            if (capturedException != null)
            {
                ExceptionDispatchInfo.Capture(capturedException).Throw();
            }
        }
    }
}
