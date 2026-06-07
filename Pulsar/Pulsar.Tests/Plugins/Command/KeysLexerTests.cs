using System.Collections.Generic;
using System.Linq;
using FluentAssertions;
using Pulsar.Core.Plugin;
using Pulsar.Native;
using Pulsar.Plugins.Extensions.Command;

namespace Pulsar.Tests.Plugins.Command
{
    public class KeysLexerTests
    {
        [Fact]
        public void Parse_SingleWord_ReturnsTextInstruction()
        {
            var result = KeysLexer.Parse("hello");
            result.Should().HaveCount(1);
            result[0].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("hello");
        }

        [Fact]
        public void Parse_MultipleWords_ReturnsSingleTextInstruction()
        {
            var result = KeysLexer.Parse("hello world");
            result.Should().HaveCount(1);
            result[0].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("hello world");
        }

        [Fact]
        public void Parse_EmptyString_ReturnsEmptyList()
        {
            var result = KeysLexer.Parse("");
            result.Should().BeEmpty();
        }

        [Fact]
        public void Parse_NamedKey_ReturnsKeyPressInstruction()
        {
            var result = KeysLexer.Parse("{ENTER}");
            result.Should().HaveCount(1);
            result[0].Should().BeOfType<KeyPressInstruction>().Which.VkCode.Should().Be(InputHelper.VK_RETURN);
        }

        [Fact]
        public void Parse_UnknownNamedKey_ReturnsLiteralText()
        {
            var result = KeysLexer.Parse("{UNKNOWN}");
            result.Should().HaveCount(1);
            result[0].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("{UNKNOWN}");
        }

        [Fact]
        public void Parse_UnclosedBrace_ReturnsLiteralText()
        {
            var result = KeysLexer.Parse("abc{ENTER");
            result.Should().HaveCount(3);
            result[0].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("abc");
            result[1].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("{");
            result[2].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("ENTER");
        }

        [Fact]
        public void Parse_SingleModifier_ReturnsKeyCombinationInstruction()
        {
            var result = KeysLexer.Parse("^c");
            result.Should().HaveCount(1);
            var combo = result[0].Should().BeOfType<KeyCombinationInstruction>().Which;
            combo.Keys.Should().Contain(InputHelper.VK_CONTROL);
            combo.Keys.Should().Contain(InputHelper.CharToVkCode('c'));
        }

        [Fact]
        public void Parse_MultipleModifiers_ReturnsKeyCombinationInstruction()
        {
            var result = KeysLexer.Parse("^+a");
            result.Should().HaveCount(1);
            var combo = result[0].Should().BeOfType<KeyCombinationInstruction>().Which;
            combo.Keys.Should().Contain(InputHelper.VK_CONTROL);
            combo.Keys.Should().Contain(InputHelper.VK_SHIFT);
            combo.Keys.Should().Contain(InputHelper.CharToVkCode('a'));
        }

        [Fact]
        public void Parse_ModifierWithNamedKey_ReturnsCombinationWithNamedVk()
        {
            var result = KeysLexer.Parse("^{F4}");
            result.Should().HaveCount(1);
            var combo = result[0].Should().BeOfType<KeyCombinationInstruction>().Which;
            combo.Keys.Should().Contain(InputHelper.VK_CONTROL);
            combo.Keys.Should().Contain(InputHelper.VK_F4);
        }

        [Fact]
        public void Parse_ConsecutiveModifiers_AllAccumulated()
        {
            var result = KeysLexer.Parse("^%+v");
            result.Should().HaveCount(1);
            var combo = result[0].Should().BeOfType<KeyCombinationInstruction>().Which;
            combo.Keys.Should().Contain(InputHelper.VK_CONTROL);
            combo.Keys.Should().Contain(InputHelper.VK_MENU);
            combo.Keys.Should().Contain(InputHelper.VK_SHIFT);
            combo.Keys.Should().Contain(InputHelper.CharToVkCode('v'));
        }

        [Fact]
        public void Parse_ModifierWithUnknownBrace_ReturnsLiteralText()
        {
            var result = KeysLexer.Parse("^{BAD}");
            result.Should().HaveCount(1);
            result[0].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("{BAD}");
        }

        [Fact]
        public void Parse_MixedSequence_ReturnsCorrectInstructionOrder()
        {
            var result = KeysLexer.Parse("^c{ENTER}world");
            result.Should().HaveCount(3);
            result[0].Should().BeOfType<KeyCombinationInstruction>();
            result[1].Should().BeOfType<KeyPressInstruction>().Which.VkCode.Should().Be(InputHelper.VK_RETURN);
            result[2].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("world");
        }

        [Fact]
        public void Parse_TextInterruptedByNamedKey_ReturnsCorrectOrder()
        {
            var result = KeysLexer.Parse("user{TAB}pass");
            result.Should().HaveCount(3);
            result[0].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("user");
            result[1].Should().BeOfType<KeyPressInstruction>().Which.VkCode.Should().Be(InputHelper.VK_TAB);
            result[2].Should().BeOfType<TextInstruction>().Which.Text.Should().Be("pass");
        }

        [Fact]
        public void Parse_NullString_ReturnsEmptyList()
        {
            var result = KeysLexer.Parse(null!);
            result.Should().BeEmpty();
        }

        [Fact]
        public void Parse_TabAndEscape_ReturnsKeyPressInstructions()
        {
            var result = KeysLexer.Parse("{TAB}{ESC}");
            result.Should().HaveCount(2);
            result[0].Should().BeOfType<KeyPressInstruction>().Which.VkCode.Should().Be(InputHelper.VK_TAB);
            result[1].Should().BeOfType<KeyPressInstruction>().Which.VkCode.Should().Be(InputHelper.VK_ESCAPE);
        }
    }
}
