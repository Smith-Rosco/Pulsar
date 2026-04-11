using System.Threading.Tasks;

namespace Pulsar.Services.Interfaces
{
    public interface IAppStartupCoordinator
    {
        Task RunBlockingInitializationAsync();

        void StartDeferredInitialization();
    }
}
