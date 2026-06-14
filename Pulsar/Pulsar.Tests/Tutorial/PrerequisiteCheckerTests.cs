using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Services.Tutorial.Prerequisites;
using Xunit;

namespace Pulsar.Tests.Tutorial
{
    public class PrerequisiteCheckerTests
    {
        [Fact]
        public async Task ExcelExistsChecker_ShouldReturnResult()
        {
            var checker = new ExcelExistsChecker();
            var result = await checker.CheckAsync();

            result.Should().NotBeNull();
            result.Id.Should().Be("ExcelExists");
            result.DisplayNameKey.Should().Be("Prerequisite.ExcelExists");
            result.Severity.Should().Be(PrerequisiteSeverity.Required);
            result.Status.Should().BeOneOf(PrerequisiteStatus.Met, PrerequisiteStatus.NotMet);
        }

        [Fact]
        public async Task VbaSupportChecker_ShouldReturnResult()
        {
            var checker = new VbaSupportChecker();
            var result = await checker.CheckAsync();

            result.Should().NotBeNull();
            result.Id.Should().Be("VbaSupport");
            result.Severity.Should().Be(PrerequisiteSeverity.Recommended);
            result.Status.Should().BeOneOf(PrerequisiteStatus.Met, PrerequisiteStatus.NotMet);
        }

        [Fact]
        public async Task BrowserExistsChecker_ShouldReturnResult()
        {
            var checker = new BrowserExistsChecker();
            var result = await checker.CheckAsync();

            result.Should().NotBeNull();
            result.Id.Should().Be("BrowserExists");
            result.Severity.Should().Be(PrerequisiteSeverity.Required);
            result.Status.Should().BeOneOf(PrerequisiteStatus.Met, PrerequisiteStatus.NotMet);
        }
    }
}
