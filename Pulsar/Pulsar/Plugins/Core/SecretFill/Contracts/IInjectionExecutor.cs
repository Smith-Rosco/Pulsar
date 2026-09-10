using System.Threading.Tasks;
using Pulsar.Plugins.Core.SecretFill.Models.Execution;

namespace Pulsar.Plugins.Core.SecretFill.Contracts
{
    public interface IInjectionExecutor
    {
        Task<SecretFillExecutionResult> ExecuteAsync(InjectionPlan plan);
    }
}
