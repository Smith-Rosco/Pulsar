using FluentAssertions;
using Pulsar.Services;

namespace Pulsar.Tests.Services
{
    public class ScriptValidationServiceTests
    {
        [Fact]
        public void Validate_ValidContent_PassesWithoutErrors()
        {
            var service = new ScriptValidationService();

            var result = service.Validate("alert('hello');");

            result.IsValid.Should().BeTrue();
            result.Errors.Should().BeEmpty();
            result.ProcessedScript.Should().Be("alert('hello');");
        }

        [Fact]
        public void Validate_StripsJavascriptPrefix()
        {
            var service = new ScriptValidationService();

            var result = service.Validate("javascript:alert('hello');");

            result.IsValid.Should().BeTrue();
            result.ProcessedScript.Should().Be("alert('hello');");
        }

        [Fact]
        public void Validate_EmptyContent_ReturnsErrors()
        {
            var service = new ScriptValidationService();

            var result = service.Validate("   ");

            result.IsValid.Should().BeFalse();
            result.Errors.Should().NotBeEmpty();
        }

        [Fact]
        public void Validate_RemovesBom()
        {
            var service = new ScriptValidationService();

            var result = service.Validate("\uFEFFalert('hi');");

            result.IsValid.Should().BeTrue();
            result.ProcessedScript.Should().Be("alert('hi');");
        }

        [Fact]
        public void Validate_StripsComments_AndCollapsesWhitespace()
        {
            var service = new ScriptValidationService();

            var result = service.Validate("alert(\n  'hi'  ); // trailing");

            result.IsValid.Should().BeTrue();
            result.ProcessedScript.Should().NotContain("//");
        }
    }
}
