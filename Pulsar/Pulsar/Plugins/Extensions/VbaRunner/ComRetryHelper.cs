using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;

namespace Pulsar.Plugins.Extensions.VbaRunner
{
    /// <summary>
    /// COM retry helper for handling transient COM errors
    /// </summary>
    public static class ComRetryHelper
    {
        private static ILogger _logger = NullLogger.Instance;
        
        /// <summary>
        /// Initialize the logger for ComRetryHelper. Should be called once during application startup.
        /// </summary>
        /// <param name="loggerFactory">Logger factory from DI container</param>
        public static void Initialize(ILoggerFactory loggerFactory)
        {
            _logger = loggerFactory?.CreateLogger("VbaRunner.ComRetryHelper") ?? NullLogger.Instance;
        }
        
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
                    _logger.LogDebug("{Operation} rejected (Busy/Unavailable). Retrying {Attempt}/{MaxRetries}", operationName, i + 1, maxRetries);
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
                    _logger.LogDebug("{Operation} rejected (Busy/Unavailable). Retrying {Attempt}/{MaxRetries}", operationName, i + 1, maxRetries);
                    Thread.Sleep(delayMs);
                }
            }
            return default!; // Should be unreachable
        }
    }
}
