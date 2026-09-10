using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.SecretFill.Models.Execution;

namespace Pulsar.Plugins.Core.SecretFill.Contracts
{
    public interface ISecretFillExecutionService
    {
        Task<SecretFillExecutionResult> ExecuteAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context);
    }
}
