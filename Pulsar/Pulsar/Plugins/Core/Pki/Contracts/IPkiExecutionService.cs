using System.Collections.Generic;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Plugins.Core.Pki.Models.Execution;

namespace Pulsar.Plugins.Core.Pki.Contracts
{
    public interface IPkiExecutionService
    {
        Task<PkiExecutionResult> ExecuteAsync(
            IReadOnlyDictionary<string, string> args,
            PulsarContext context);
    }
}
