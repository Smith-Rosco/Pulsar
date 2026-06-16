// [Path]: Pulsar.Tests/Tutorial/TutorialStepLoaderTests.cs

using System;
using System.IO;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Pulsar.Core.Localization;
using Pulsar.Features.Tutorial.Models;
using Pulsar.Features.Tutorial.Services;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class TutorialStepLoaderTests
    {
        private static TutorialStepLoader CreateLoader()
        {
            var logger = new Mock<ILogger<TutorialStepLoader>>();
            var loc = new Mock<ILocalizationService>();
            loc.Setup(l => l.GetString(It.IsAny<string>())).Returns((string key) => key);
            return new TutorialStepLoader(logger.Object, loc.Object);
        }

        [Fact]
        public void LoadSteps_ShouldReturnFallback_WhenFileMissing()
        {
            var loader = CreateLoader();

            var steps = loader.LoadSteps(Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-missing.json"));

            steps.Should().NotBeNull();
            steps.Should().NotBeEmpty("fallback steps should be provided when config is missing");
            steps.Should().OnlyContain(s => !string.IsNullOrWhiteSpace(s.Id));
        }

        [Fact]
        public void LoadSteps_ShouldParseValidJsonConfig()
        {
            var loader = CreateLoader();

            var json = "{" +
                       "\"version\":\"1.0\"," +
                       "\"steps\":[" +
                       "{" +
                       "\"id\":\"s1\"," +
                       "\"title\":\"t\"," +
                       "\"description\":\"d\"," +
                       "\"type\":\"Instruction\"," +
                       "\"focusMode\":\"AlwaysObserving\"," +
                       "\"completionTrigger\":{\"type\":\"ButtonClick\"}" +
                       "}" +
                       "]" +
                       "}";

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-tutorial.json");
            File.WriteAllText(path, json);
            try
            {
                var steps = loader.LoadSteps(path);

                steps.Should().HaveCount(1);
                steps[0].Id.Should().Be("s1");
                steps[0].Type.Should().Be(TutorialStepType.Instruction);
                steps[0].CompletionTrigger.Should().NotBeNull();
                steps[0].CompletionTrigger!.Type.Should().Be(TutorialTriggerType.ButtonClick);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }

        [Fact]
        public void LoadSteps_ShouldFallback_WhenJsonHasDuplicateIds()
        {
            var loader = CreateLoader();

            var json = "{" +
                       "\"version\":\"1.0\"," +
                       "\"steps\":[" +
                       "{" +
                       "\"id\":\"dup\"," +
                       "\"title\":\"t1\"," +
                       "\"description\":\"d1\"," +
                       "\"type\":\"Instruction\"," +
                       "\"completionTrigger\":{\"type\":\"ButtonClick\"}" +
                       "}," +
                       "{" +
                       "\"id\":\"dup\"," +
                       "\"title\":\"t2\"," +
                       "\"description\":\"d2\"," +
                       "\"type\":\"Instruction\"," +
                       "\"completionTrigger\":{\"type\":\"ButtonClick\"}" +
                       "}" +
                       "]" +
                       "}";

            var path = Path.Combine(Path.GetTempPath(), Guid.NewGuid() + "-tutorial.json");
            File.WriteAllText(path, json);
            try
            {
                var steps = loader.LoadSteps(path);

                steps.Should().NotBeEmpty("loader should not crash when config is invalid");
                steps.Should().NotContain(s => s.Id == string.Empty);
            }
            finally
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
