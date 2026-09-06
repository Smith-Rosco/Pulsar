using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Services.Interfaces;
using Pulsar.Services.Updates;
using Xunit;

namespace Pulsar.Tests.Services.Updates;

public class UpdateCheckServiceTests
{
    private const string CurrentVersion = "1.10.0";

    private static UpdateCheckService CreateService(IUpdateHttpGateway gateway) => new(gateway);

    [Fact]
    public async Task ApiSucceeds_UsesApiResult_AndShortCircuitsLaterTiers()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => UpdateTestResponses.Json(UpdateTestSamples.RestJson));

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.UpdateAvailable);
        result.Source.Should().Be(UpdateCheckSource.RestApi);
        result.LatestTag.Should().Be("v1.11.0");
        result.Release!.Assets.Should().HaveCount(2);
        gateway.Requests.Should().ContainSingle("a successful tier short-circuits all later tiers");
    }

    [Fact]
    public async Task ApiBlocked_AtomSucceeds_UsesAtomTag()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => UpdateTestResponses.Error(403))
            .Route("releases.atom", _ => UpdateTestResponses.Text(UpdateTestSamples.AtomFeed));

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.UpdateAvailable);
        result.Source.Should().Be(UpdateCheckSource.AtomFeed);
        result.LatestTag.Should().Be("v1.11.0");
        gateway.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task ApiAndAtomBlocked_RedirectProbeSucceeds_ExtractsTagFromLocation()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => UpdateTestResponses.Error(500))
            .Route("releases.atom", _ => throw new InvalidOperationException("connection refused"))
            .Route("releases/latest", _ => UpdateTestResponses.Redirect(UpdateTestSamples.RedirectLocation));

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.UpdateAvailable);
        result.Source.Should().Be(UpdateCheckSource.RedirectProbe);
        result.LatestTag.Should().Be("v1.11.0");
        gateway.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task AllTiersFail_ReportsNetworkError_NeverFalseBadge()
    {
        var gateway = new FakeUpdateGateway().Fallback(_ => throw new TaskCanceledException("timeout"));

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.NetworkError);
        result.LatestTag.Should().BeNull();
        result.Error.Should().NotBeNullOrEmpty();
        gateway.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task AllTiersTimeout_TaskCanceledTreatedAsTierFailureNotCrash()
    {
        var gateway = new FakeUpdateGateway().Fallback(_ => throw new TaskCanceledException());

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.NetworkError);
    }

    [Fact]
    public async Task ApiTimeout_AtomSucceeds_TimeoutIsTierFailureOnly()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => throw new TaskCanceledException("5s API timeout"))
            .Route("releases.atom", _ => UpdateTestResponses.Text(UpdateTestSamples.AtomFeed));

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.UpdateAvailable);
        result.Source.Should().Be(UpdateCheckSource.AtomFeed);
    }

    [Fact]
    public async Task HeaderIsolation_EachTierCarriesOnlyItsOwnAccept()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => UpdateTestResponses.Error(500))
            .Route("releases.atom", _ => UpdateTestResponses.Error(500))
            .Route("releases/latest", _ => UpdateTestResponses.Redirect(UpdateTestSamples.RedirectLocation));

        await CreateService(gateway).CheckAsync(CurrentVersion);

        gateway.Requests.Select(r => (r.Uri.ToString(), r.Accept)).Should().Equal(
            ("https://api.github.com/repos/Smith-Rosco/Pulsar/releases/latest", "application/vnd.github+json"),
            ("https://github.com/Smith-Rosco/Pulsar/releases.atom", "application/atom+xml, application/xml"),
            ("https://github.com/Smith-Rosco/Pulsar/releases/latest", "text/html"));
    }

    [Fact]
    public async Task RedirectProbe_DoesNotFollowRedirects()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => UpdateTestResponses.Error(500))
            .Route("releases.atom", _ => UpdateTestResponses.Error(500))
            .Route("releases/latest", request =>
            {
                request.FollowRedirects.Should().BeFalse("the probe must observe the raw 302 Location header");
                return UpdateTestResponses.Redirect(UpdateTestSamples.RedirectLocation);
            });

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.LatestTag.Should().Be("v1.11.0");
    }

    [Fact]
    public async Task PerTierTimeouts_AreConfiguredAsSpecified()
    {
        var gateway = new FakeUpdateGateway()
            .Route("api.github.com", _ => UpdateTestResponses.Error(500))
            .Route("releases.atom", _ => UpdateTestResponses.Error(500))
            .Route("releases/latest", _ => UpdateTestResponses.Redirect(UpdateTestSamples.RedirectLocation));

        await CreateService(gateway).CheckAsync(CurrentVersion);

        gateway.Requests.Select(r => r.Timeout).Should().Equal(
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(6),
            TimeSpan.FromSeconds(5));
    }

    [Theory]
    [InlineData("1.10.0", "v1.9.0", UpdateCheckOutcome.UpToDate)]
    [InlineData("1.10.0", "v1.10.0", UpdateCheckOutcome.UpToDate)]
    [InlineData("1.9.0", "v1.10.0", UpdateCheckOutcome.UpdateAvailable)]
    public async Task VersionComparison_MapsToThreeOutcomes(string current, string latest, UpdateCheckOutcome expected)
    {
        var gateway = new FakeUpdateGateway().Route(
            "api.github.com",
            _ => UpdateTestResponses.Json($$"""{"tag_name":"{{latest}}","assets":[]}"""));

        var result = await CreateService(gateway).CheckAsync(current);

        result.Outcome.Should().Be(expected);
    }

    [Fact]
    public async Task UnparseableLatestTag_ReportsParseFailed_NeverAutoDownload()
    {
        var gateway = new FakeUpdateGateway().Route(
            "api.github.com",
            _ => UpdateTestResponses.Json("""{"tag_name":"latest-release","assets":[]}"""));

        var result = await CreateService(gateway).CheckAsync(CurrentVersion);

        result.Outcome.Should().Be(UpdateCheckOutcome.ParseFailed);
    }

    [Fact]
    public async Task UnparseableCurrentVersion_ReportsParseFailed()
    {
        var gateway = new FakeUpdateGateway().Route("api.github.com", _ => UpdateTestResponses.Json(UpdateTestSamples.RestJson));

        var result = await CreateService(gateway).CheckAsync("not-a-version");

        result.Outcome.Should().Be(UpdateCheckOutcome.ParseFailed);
    }

    [Fact]
    public async Task UserCancellation_Propagates_OutOfTheCheck()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var gateway = new CancellingStub();

        await Assert.ThrowsAsync<OperationCanceledException>(() => gateway.Wrapped().CheckAsync(CurrentVersion, cts.Token));
    }

    /// <summary>Gateway that always observes and honors caller cancellation (unlike tier-failure fakes).</summary>
    private sealed class CancellingStub : IUpdateHttpGateway
    {
        public UpdateCheckService Wrapped() => new(this);

        public Task<UpdateHttpResponse> SendAsync(UpdateHttpRequest request, CancellationToken cancellationToken = default)
        {
            cancellationToken.ThrowIfCancellationRequested();
            throw new OperationCanceledException(cancellationToken);
        }
    }
}
