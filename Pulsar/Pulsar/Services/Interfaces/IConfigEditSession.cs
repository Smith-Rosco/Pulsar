using System.Threading.Tasks;
using Pulsar.Models;
using Pulsar.Services.Validation;

namespace Pulsar.Services.Interfaces
{
    /// <summary>
    /// Transactional edit seam for an isolated Profiles.json draft.
    /// Commit resolves one optimistic-concurrency conflict by rebasing untouched
    /// regions and retrying once.
    /// </summary>
    public interface IConfigEditSession
    {
        ProfilesConfig Draft { get; }

        bool HasCommitted { get; }

        Task<ValidationResult?> ValidateAsync();

        Task CommitAsync();
    }
}
