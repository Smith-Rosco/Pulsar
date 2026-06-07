using System.Collections.Generic;

namespace Pulsar.Core.Plugin
{
    public abstract record KeyInstruction;

    public sealed record TextInstruction(string Text) : KeyInstruction;

    public sealed record KeyPressInstruction(ushort VkCode) : KeyInstruction;

    public sealed record KeyCombinationInstruction(IReadOnlyList<ushort> Keys) : KeyInstruction;
}
