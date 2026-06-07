using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Pulsar.Core.Plugin;
using Pulsar.Native;

namespace Pulsar.Plugins.Extensions.Command
{
    public class KeySender : IKeySender
    {
        public void SendText(string text)
        {
            InputHelper.SendText(text);
        }

        public void SendKeyCombination(params ushort[] virtualKeys)
        {
            InputHelper.SendKeyCombination(virtualKeys);
        }

        public ushort? GetNamedKey(string name)
        {
            return InputHelper.GetNamedKey(name);
        }

        public ushort CharToVkCode(char c)
        {
            return InputHelper.CharToVkCode(c);
        }

        public void Execute(KeyInstruction instruction)
        {
            switch (instruction)
            {
                case TextInstruction text:
                    SendText(text.Text);
                    break;
                case KeyPressInstruction keyPress:
                    SendKeyCombination(keyPress.VkCode);
                    break;
                case KeyCombinationInstruction combo:
                    SendKeyCombination(combo.Keys.ToArray());
                    break;
            }
        }

        public void Execute(IEnumerable<KeyInstruction> instructions, CancellationToken cancellationToken = default)
        {
            foreach (var instruction in instructions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Execute(instruction);
            }
        }

        public Task ExecuteAsync(IReadOnlyList<KeyInstruction> instructions, CancellationToken cancellationToken)
        {
            cancellationToken.ThrowIfCancellationRequested();
            foreach (var instruction in instructions)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Execute(instruction);
            }
            return Task.CompletedTask;
        }
    }
}
