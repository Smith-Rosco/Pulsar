using Pulsar.Services.Interfaces;
using Serilog;
using Serilog.Core;
using Serilog.Events;

namespace Pulsar.Services
{
    /// <summary>
    /// Service for managing logging configuration at runtime
    /// </summary>
    public class LoggingConfigService : ILoggingConfigService
    {
        private readonly LoggingLevelSwitch _levelSwitch;

        public LogEventLevel CurrentLevel => _levelSwitch.MinimumLevel;

        public LoggingConfigService(LoggingLevelSwitch levelSwitch)
        {
            _levelSwitch = levelSwitch;
        }

        public void SetLogLevel(LogEventLevel level)
        {
            _levelSwitch.MinimumLevel = level;
            Log.Information("[LoggingConfig] Log level changed to {Level}", level);
        }

        public LogEventLevel[] GetAvailableLevels()
        {
            return new[]
            {
                LogEventLevel.Verbose,
                LogEventLevel.Debug,
                LogEventLevel.Information,
                LogEventLevel.Warning,
                LogEventLevel.Error,
                LogEventLevel.Fatal
            };
        }
    }
}
