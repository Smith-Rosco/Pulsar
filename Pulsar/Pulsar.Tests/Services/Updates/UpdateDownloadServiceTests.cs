using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using FluentAssertions;
using Pulsar.Services.Updates;
using Xunit;

namespace Pulsar.Tests.Services.Updates;

public class UpdateDownloadServiceTests : IDisposable
{
    private static readonly byte[] Payload = Encoding.UTF8.GetBytes(new string('P', 4096));

    private readonly string _tempDirectory;

    public UpdateDownloadServiceTests()
    {
        _tempDirectory = Path.Combine(Path.GetTempPath(), "pulsar-update-download-tests", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_tempDirectory);
    }

    public void Dispose()
    {
        try
        {
            Directory.Delete(_tempDirectory, recursive: true);
        }
        catch (DirectoryNotFoundException)
        {
        }
    }

    private string TempPath(string name) => Path.Combine(_tempDirectory, name);

    private static GitHubReleaseAsset Asset(
        string url = "https://github.com/Smith-Rosco/Pulsar/releases/download/v1.11.0/Pulsar-v1.11.0-Setup.exe",
        string? sha256 = null,
        long? size = null) => new("Pulsar-v1.11.0-Setup.exe", url, sha256, size);

    private static string Sha256Hex(byte[] bytes)
    {
        using var sha256 = SHA256.Create();
        return Convert.ToHexString(sha256.ComputeHash(bytes));
    }

    [Fact]
    public async Task OfficialSourceSucceeds_CompletesWithSha256Verification()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(sha256: Sha256Hex(Payload)), destination);

        result.Success.Should().BeTrue();
        result.Failure.Should().Be(UpdateDownloadFailure.None);
        result.IntegrityMode.Should().Be(UpdateIntegrityMode.Sha256);
        result.FilePath.Should().Be(destination);
        File.ReadAllBytes(destination).Should().Equal(Payload);
        gateway.Requests.Should().ContainSingle("official source succeeded → no mirror contact");
    }

    [Fact]
    public async Task OfficialNotFound_MirrorSucceeds_FailoverWithoutUserIntervention()
    {
        var gateway = new FakeUpdateGateway()
            .Route("github.com", _ => UpdateTestResponses.Error(404))
            .Route("ghfast.top", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(sha256: Sha256Hex(Payload)), destination);

        result.Success.Should().BeTrue();
        result.IntegrityMode.Should().Be(UpdateIntegrityMode.Sha256);
        gateway.Requests.Select(r => r.Uri.Host).Should().Equal("github.com", "ghfast.top");
    }

    [Theory]
    [InlineData(502)]
    [InlineData(500)]
    [InlineData(403)]
    public async Task MirrorServerError_AdvancesThroughRemainingSources(int errorStatus)
    {
        var gateway = new FakeUpdateGateway()
            .Route("github.com", _ => UpdateTestResponses.Error(errorStatus))
            .Route("ghfast.top", _ => UpdateTestResponses.Error(errorStatus))
            .Route("gh-proxy.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(sha256: Sha256Hex(Payload)), destination);

        result.Success.Should().BeTrue();
        gateway.Requests.Should().HaveCount(3);
    }

    [Fact]
    public async Task TimeoutOnFirstSource_AdvancesToNext()
    {
        var gateway = new FakeUpdateGateway()
            .Route("github.com", _ => throw new TaskCanceledException("60s download timeout"))
            .Route("ghfast.top", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);

        var result = await service.DownloadAsync(Asset(sha256: Sha256Hex(Payload)), TempPath("setup.exe"));

        result.Success.Should().BeTrue();
        gateway.Requests.Should().HaveCount(2);
    }

    [Fact]
    public async Task MidStreamBreak_DiscardsPartialFile_AdvancesToNextSource()
    {
        var gateway = new FakeUpdateGateway()
            .Route("github.com", _ => UpdateTestResponses.MidStreamBreak(Payload[..512]))
            .Route("ghfast.top", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(sha256: Sha256Hex(Payload)), destination);

        result.Success.Should().BeTrue();
        result.IntegrityMode.Should().Be(UpdateIntegrityMode.Sha256);
        File.ReadAllBytes(destination).Should().Equal(Payload);
    }

    [Fact]
    public async Task AllSourcesFail_ReportsAllSourcesFailed_WithoutResidue()
    {
        var gateway = new FakeUpdateGateway().Fallback(_ => UpdateTestResponses.Error(404));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(), destination);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(UpdateDownloadFailure.AllSourcesFailed);
        result.Error.Should().NotBeNullOrEmpty();
        result.AttemptedSources.Should().HaveCount(3); // official + 2 default mirrors
        File.Exists(destination).Should().BeFalse("partial residue must not survive");
    }

    [Fact]
    public async Task HeaderIsolation_DownloadCarriesOnlyBinaryAccept()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);

        await service.DownloadAsync(Asset(), TempPath("setup.exe"));

        var request = gateway.Requests.Should().ContainSingle().Subject;
        request.Accept.Should().Be(UpdateDownloadService.BinaryAccept);
    }

    [Fact]
    public async Task DigestMismatch_DiscardsFile_ReportsIntegrityFailed()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");
        var wrongDigest = Sha256Hex(Encoding.UTF8.GetBytes("other-content"));

        var result = await service.DownloadAsync(Asset(sha256: wrongDigest), destination);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(UpdateDownloadFailure.IntegrityFailed);
        File.Exists(destination).Should().BeFalse("a failed integrity check must discard the asset");
    }

    [Fact]
    public async Task NoDigest_SizeMatches_FallsBackToSizeVerification()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(size: Payload.Length), destination);

        result.Success.Should().BeTrue();
        result.IntegrityMode.Should().Be(UpdateIntegrityMode.SizeBytes, "digest-less path verifies by size (weaker check surfaced to UI)");
    }

    [Fact]
    public async Task NoDigest_SizeMismatch_DiscardsFile()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        var result = await service.DownloadAsync(Asset(size: Payload.Length + 1), destination);

        result.Success.Should().BeFalse();
        result.Failure.Should().Be(UpdateDownloadFailure.IntegrityFailed);
        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public async Task NoDigestNoSize_AcceptedAsUnverified()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);

        var result = await service.DownloadAsync(Asset(), TempPath("setup.exe"));

        result.Success.Should().BeTrue();
        result.IntegrityMode.Should().Be(UpdateIntegrityMode.Unverified);
    }

    [Fact]
    public async Task ProgressEvents_AreRaisedWithContentLengthTotal()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var events = new List<(long Read, long Total)>();
        service.ProgressChanged += (read, total) => events.Add((read, total));

        await service.DownloadAsync(Asset(), TempPath("setup.exe"));

        events.Should().NotBeEmpty();
        events[0].Total.Should().Be(Payload.Length, "total comes from the Content-Length header");
        events[^1].Read.Should().Be(Payload.Length);
        events.Select(e => e.Read).Should().OnlyHaveUniqueItems();
    }

    [Fact]
    public async Task CustomMirrorTemplates_AreUsedInOrder()
    {
        var gateway = new FakeUpdateGateway()
            .Route("github.com", _ => UpdateTestResponses.Error(404))
            .Route("mirror-a.test", _ => UpdateTestResponses.Error(404))
            .Route("mirror-b.test", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway, new[]
        {
            "https://mirror-a.test/{url}",
            "https://mirror-b.test/{url}",
        });

        var result = await service.DownloadAsync(Asset(), TempPath("setup.exe"));

        result.Success.Should().BeTrue();
        result.AttemptedSources.Select(u => new Uri(u).Host).Should().Equal("github.com", "mirror-a.test", "mirror-b.test");
    }

    [Fact]
    public async Task UserCancellation_PropagatesAndLeavesNoResidue()
    {
        using var cts = new CancellationTokenSource();
        cts.Cancel();
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("setup.exe");

        await Assert.ThrowsAsync<OperationCanceledException>(
            () => service.DownloadAsync(Asset(), destination, cts.Token));

        File.Exists(destination).Should().BeFalse();
    }

    [Fact]
    public async Task DestinationDirectory_CreatedOnDemand()
    {
        var gateway = new FakeUpdateGateway().Route("github.com", _ => UpdateTestResponses.Bytes(Payload));
        var service = new UpdateDownloadService(gateway);
        var destination = TempPath("nested/deeper/setup.exe");

        var result = await service.DownloadAsync(Asset(), destination);

        result.Success.Should().BeTrue();
        File.Exists(destination).Should().BeTrue();
    }
}
