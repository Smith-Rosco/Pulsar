using System.Collections.Generic;
using System.Threading.Tasks;

namespace Pulsar.Features.Tutorial.Services.Prerequisites
{
    public interface IPrerequisiteProvider
    {
        Task<IReadOnlyList<IPrerequisiteChecker>> GetCheckersAsync();

        Task<IReadOnlyList<PrerequisiteResult>> CheckAllAsync();
    }
}
