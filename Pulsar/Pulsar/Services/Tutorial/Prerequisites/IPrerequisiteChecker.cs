using System.Threading.Tasks;

namespace Pulsar.Services.Tutorial.Prerequisites
{
    public interface IPrerequisiteChecker
    {
        string Id { get; }

        string DisplayNameKey { get; }

        PrerequisiteSeverity Severity { get; }

        Task<PrerequisiteResult> CheckAsync();
    }
}
