using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Core.Plugin
{
    /// <summary>
    /// Optional plugin interface that supports cancellation.
    /// </summary>
    public interface ICancellablePulsarPlugin
    {
        /// <summary>
        /// Execute plugin action with cancellation support.
        /// </summary>
        Task<PluginResult> ExecuteAsync(
            string action,
            IReadOnlyDictionary<string, string> args,
            PulsarContext context,
            CancellationToken cancellationToken
        );
    }
}
