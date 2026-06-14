using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Services.Tutorial.Prerequisites
{
    public interface IPrerequisiteProvider
    {
        Task<IReadOnlyList<IPrerequisiteChecker>> GetCheckersAsync();

        Task<IReadOnlyList<PrerequisiteResult>> CheckAllAsync();
    }
}
