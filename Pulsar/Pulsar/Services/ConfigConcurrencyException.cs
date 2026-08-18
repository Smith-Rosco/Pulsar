using System;

namespace Pulsar.Services
{
    /// <summary>
    /// Raised by <see cref="ConfigService.SaveAsync"/> when a
    /// <see cref="ConfigEditSession"/> commits against a stale revision — i.e. a
    /// concurrent editor committed first. The caller decides how to reconcile
    /// (retry, reload, or surface to the user); a silent overwrite would lose data.
    /// </summary>
    public sealed class ConfigConcurrencyException : InvalidOperationException
    {
        public ConfigConcurrencyException(string message)
            : base(message)
        {
        }

        public ConfigConcurrencyException(string message, Exception innerException)
            : base(message, innerException)
        {
        }
    }
}
