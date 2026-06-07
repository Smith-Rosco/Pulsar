using System;
using System.Collections.Generic;
using System.Text;
using Pulsar.Core.Plugin;
using Pulsar.Native;

namespace Pulsar.Plugins.Extensions.Command
{
    public static class KeysLexer
    {
        public static IReadOnlyList<KeyInstruction> Parse(string keys)
        {
            if (string.IsNullOrEmpty(keys))
                return Array.Empty<KeyInstruction>();

            var instructions = new List<KeyInstruction>();
            var sb = new StringBuilder();
            int i = 0;

            while (i < keys.Length)
            {
                char c = keys[i];

                if (c == '{')
                {
                    FlushTextBuffer(sb, instructions);
                    int close = keys.IndexOf('}', i + 1);
                    if (close < 0)
                    {
                        instructions.Add(new TextInstruction("{"));
                        i++;
                        continue;
                    }

                    string token = keys.Substring(i + 1, close - i - 1);
                    i = close + 1;

                    if (InputHelper.GetNamedKey(token) is ushort vk)
                    {
                        instructions.Add(new KeyPressInstruction(vk));
                        continue;
                    }

                    sb.Append('{').Append(token).Append('}');
                    continue;
                }

                if (c == '^' || c == '+' || c == '%')
                {
                    FlushTextBuffer(sb, instructions);
                    var modifiers = new List<ushort>();
                    while (i < keys.Length && (keys[i] == '^' || keys[i] == '+' || keys[i] == '%'))
                    {
                        switch (keys[i])
                        {
                            case '^': modifiers.Add(InputHelper.VK_CONTROL); break;
                            case '+': modifiers.Add(InputHelper.VK_SHIFT); break;
                            case '%': modifiers.Add(InputHelper.VK_MENU); break;
                        }
                        i++;
                    }

                    if (i < keys.Length)
                    {
                        if (keys[i] == '{')
                        {
                            int close = keys.IndexOf('}', i + 1);
                            if (close >= 0)
                            {
                                string token = keys.Substring(i + 1, close - i - 1);
                                i = close + 1;
                                if (InputHelper.GetNamedKey(token) is ushort namedVk)
                                {
                                    modifiers.Add(namedVk);
                                    instructions.Add(new KeyCombinationInstruction(modifiers.ToArray()));
                                    continue;
                                }

                                sb.Append('{').Append(token).Append('}');
                                continue;
                            }

                            instructions.Add(new TextInstruction("{"));
                            i++;
                            continue;
                        }
                        else
                        {
                            char keyChar = keys[i];
                            i++;
                            var vk = InputHelper.CharToVkCode(keyChar);
                            modifiers.Add(vk);
                            instructions.Add(new KeyCombinationInstruction(modifiers.ToArray()));
                            continue;
                        }
                    }

                    continue;
                }

                sb.Append(c);
                i++;
            }

            FlushTextBuffer(sb, instructions);

            return instructions;
        }

        private static void FlushTextBuffer(StringBuilder sb, List<KeyInstruction> instructions)
        {
            if (sb.Length > 0)
            {
                instructions.Add(new TextInstruction(sb.ToString()));
                sb.Clear();
            }
        }
    }
}
