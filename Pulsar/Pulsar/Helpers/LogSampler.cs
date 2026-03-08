// [Path]: Pulsar/Pulsar/Helpers/LogSampler.cs

namespace Pulsar.Helpers
{
    /// <summary>
    /// Helper class for sampling high-frequency log statements.
    /// Reduces log volume by logging only every Nth occurrence.
    /// </summary>
    /// <remarks>
    /// Thread-safety: This class is NOT thread-safe by design for performance.
    /// Minor inaccuracies in sampling rate are acceptable for logging purposes.
    /// </remarks>
    public class LogSampler
    {
        private int _counter = 0;
        private readonly int _rate;

        /// <summary>
        /// Creates a new log sampler with the specified sample rate.
        /// </summary>
        /// <param name="rate">Sample rate (e.g., 10 means log 1 in every 10 calls)</param>
        public LogSampler(int rate)
        {
            _rate = rate > 0 ? rate : 1;
        }

        /// <summary>
        /// Returns true every Nth call (1 in rate).
        /// </summary>
        /// <returns>True if this call should be logged, false otherwise</returns>
        public bool ShouldLog()
        {
            return (++_counter % _rate) == 0;
        }

        /// <summary>
        /// Gets the sample rate for logging context.
        /// </summary>
        public int Rate => _rate;

        /// <summary>
        /// Resets the internal counter (useful for testing or manual control).
        /// </summary>
        public void Reset()
        {
            _counter = 0;
        }
    }
}
