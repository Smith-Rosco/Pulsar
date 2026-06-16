using System.Threading.Tasks;

namespace Pulsar.Features.Tutorial.Services.Prerequisites
{
    public interface IPrerequisiteChecker
    {
        string Id { get; }

        string DisplayNameKey { get; }

        PrerequisiteSeverity Severity { get; }

        Task<PrerequisiteResult> CheckAsync();
    }
}
