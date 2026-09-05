// [Path]: Pulsar/Pulsar.Tests/E2E/StatsFixtureValidatorTests.cs

using System;
using System.IO;
using FluentAssertions;
using Pulsar.E2E.Driver;
using Xunit;

namespace Pulsar.Tests.E2E
{
    /// <summary>
    /// Unit tests for the E2E stats-fixture structural pre-check. The fixture
    /// must be a top-level camelCase JSON array whose items carry a string
    /// 'pluginId' — anything else silently renders an empty stats page at
    /// runtime, so the validator must fail fast with an actionable error.
    /// </summary>
    public class StatsFixtureValidatorTests
    {
        private static string WriteTemp(string json)
        {
            var path = Path.Combine(Path.GetTempPath(), $"pulsar-e2e-stats-{Guid.NewGuid():N}.json");
            File.WriteAllText(path, json);
            return path;
        }

        [Fact]
        public void Validate_ValidArray_DoesNotThrow()
        {
            var path = WriteTemp("""
            [
              { "pluginId": "com.pulsar.winswitcher", "executions": 6, "successes": 6, "totalDurationMs": 108 },
              { "pluginId": "com.pulsar.command", "executions": 2, "successes": 1, "totalDurationMs": 40 }
            ]
            """);

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_EmptyArray_IsStructurallyValid()
        {
            var path = WriteTemp("[]");

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().NotThrow();
        }

        [Fact]
        public void Validate_ArrayItemMissingPluginId_Throws()
        {
            var path = WriteTemp("""
            [ { "executions": 6 } ]
            """);

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*'pluginId'*")
               .WithMessage("*camelCase*");
        }

        [Fact]
        public void Validate_PascalCasePluginId_Throws()
        {
            var path = WriteTemp("""
            [ { "PluginId": "com.pulsar.winswitcher", "executions": 6 } ]
            """);

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*'pluginId'*")
               .WithMessage("*PascalCase*");
        }

        [Fact]
        public void Validate_DictionaryKeyedByPluginId_Throws()
        {
            var path = WriteTemp("""
            { "com.pulsar.winswitcher": { "executions": 6 } }
            """);

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*top-level JSON array*");
        }

        [Fact]
        public void Validate_SingleObject_Throws()
        {
            var path = WriteTemp("""
            { "pluginId": "com.pulsar.winswitcher", "executions": 6 }
            """);

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*top-level JSON array*");
        }

        [Fact]
        public void Validate_ArrayWithNonObjectItem_Throws()
        {
            var path = WriteTemp("""[ "com.pulsar.winswitcher" ]""");

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*array item must be an object*");
        }

        [Fact]
        public void Validate_NonStringPluginId_Throws()
        {
            var path = WriteTemp("""[ { "pluginId": 6 } ]""");

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*string 'pluginId'*");
        }

        [Fact]
        public void Validate_NotJson_Throws()
        {
            var path = WriteTemp("not-json-at-all");

            var act = () => StatsFixtureValidator.Validate(path);

            act.Should().Throw<InvalidOperationException>()
               .WithMessage("*not valid JSON*");
        }
    }
}
