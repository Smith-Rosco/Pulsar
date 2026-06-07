using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace Pulsar.Core.Plugin
{
    public interface IKeySender
    {
        void SendText(string text);
        void SendKeyCombination(params ushort[] virtualKeys);
        ushort? GetNamedKey(string name);
        ushort CharToVkCode(char c);
        void Execute(KeyInstruction instruction);
        void Execute(IEnumerable<KeyInstruction> instructions, CancellationToken cancellationToken = default);
        Task ExecuteAsync(IReadOnlyList<KeyInstruction> instructions, CancellationToken cancellationToken);
    }
}
