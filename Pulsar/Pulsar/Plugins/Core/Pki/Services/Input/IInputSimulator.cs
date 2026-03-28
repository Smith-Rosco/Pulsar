using System.Threading.Tasks;

namespace Pulsar.Plugins.Core.Pki.Services.Input
{
    public interface IInputSimulator
    {
        Task SimulateTextForceSendKeysAsync(string text);
        Task<bool> TrySimulateTextUiaAsync(string text);
        Task SimulateKeyAsync(string key);
    }
}