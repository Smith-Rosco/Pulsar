using Serilog.Events;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Service for managing logging configuration at runtime
    /// </summary>
    public interface ILoggingConfigService
    {
        /// <summary>
        /// Gets the current minimum log level
        /// </summary>
        LogEventLevel CurrentLevel { get; }

        /// <summary>
        /// Sets the minimum log level for the application
        /// </summary>
        void SetLogLevel(LogEventLevel level);

        /// <summary>
        /// Gets available log levels for UI display
        /// </summary>
        LogEventLevel[] GetAvailableLevels();
    }
}
