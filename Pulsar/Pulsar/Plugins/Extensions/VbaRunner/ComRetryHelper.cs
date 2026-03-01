using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    public static class ComRetryHelper
    {
        public static ILogger? Logger { get; set; }
        private const int RPC_E_CALL_REJECTED = unchecked((int)0x80010001);
        private const int MK_E_UNAVAILABLE = unchecked((int)0x800401E3);

        public static void Execute(Action action, string operationName = "COM Operation", int maxRetries = 5, int delayMs = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    action();
                    return;
                }
                catch (COMException ex) when (ex.ErrorCode == RPC_E_CALL_REJECTED || ex.ErrorCode == MK_E_UNAVAILABLE)
                {
                    if (i == maxRetries - 1) throw;
                    Logger?.LogDebug("[ComRetry] {Operation} rejected (Busy/Unavailable). Retrying {Attempt}/{MaxRetries}...", operationName, i + 1, maxRetries);
                    Thread.Sleep(delayMs);
                }
            }
        }

        public static T Execute<T>(Func<T> func, string operationName = "COM Operation", int maxRetries = 5, int delayMs = 500)
        {
            for (int i = 0; i < maxRetries; i++)
            {
                try
                {
                    return func();
                }
                catch (COMException ex) when (ex.ErrorCode == RPC_E_CALL_REJECTED || ex.ErrorCode == MK_E_UNAVAILABLE)
                {
                    if (i == maxRetries - 1) throw;
                    Logger?.LogDebug("[ComRetry] {Operation} rejected (Busy/Unavailable). Retrying {Attempt}/{MaxRetries}...", operationName, i + 1, maxRetries);
                    Thread.Sleep(delayMs);
                }
            }
            return default!; // Should be unreachable
        }
    }
}
