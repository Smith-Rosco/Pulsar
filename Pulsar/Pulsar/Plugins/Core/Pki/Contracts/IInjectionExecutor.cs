using System.Threading.Tasks;
using Pulsar.Plugins.Core.Pki.Models.Execution;

namespace Pulsar.Plugins.Core.Pki.Contracts
{
    public interface IInjectionExecutor
    {
        Task<PkiExecutionResult> ExecuteAsync(InjectionPlan plan);
    }
}
