using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Pulsar.Core.Plugin;
using Pulsar.Native;

namespace Pulsar.Services.Simulation
{
    /// <summary>
    /// A no-op <see cref="IKeySender"/> that logs the intended action instead of
    /// injecting real keystrokes. Used by the headless simulator in dry-run mode
    /// so "simulation" never touches the active window.
    /// </summary>
    public sealed class DryRunKeySender : IKeySender
    {
        private readonly ILogger<DryRunKeySender> _logger;

        public DryRunKeySender(ILogger<DryRunKeySender> logger)
        {
            _logger = logger;
        }

        public void SendText(string text)
        {
            _logger.LogInformation("[DryRun] would send text: {Text}", text);
        }

        public void SendKeyCombination(params ushort[] virtualKeys)
        {
            _logger.LogInformation("[DryRun] would send key combination: {Keys}", string.Join(",", virtualKeys));
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
            _logger.LogInformation("[DryRun] would execute instruction: {Instruction}", Describe(instruction));
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
            Execute(instructions, cancellationToken);
            return Task.CompletedTask;
        }

        private static string Describe(KeyInstruction instruction)
        {
            return instruction switch
            {
                TextInstruction text => $"text({text.Text})",
                KeyPressInstruction keyPress => $"key({keyPress.VkCode})",
                KeyCombinationInstruction combo => $"combo({string.Join(",", combo.Keys)})",
                _ => instruction.GetType().Name
            };
        }
    }
}
