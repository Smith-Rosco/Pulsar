using System;
using System.Linq;
using FluentAssertions;
using Pulsar.Services.Updates;
using Xunit;

namespace Pulsar.Tests.Services.Updates;

public class GitHubReleaseInfoParserTests
{
    [Fact]
    public void TryParseRestJson_RealResponseShape_ParsesTagAndAssetsWithDigest()
    {
        var ok = GitHubReleaseInfoParser.TryParseRestJson(UpdateTestSamples.RestJson, out var info);

        ok.Should().BeTrue();
        info.Should().NotBeNull();
        info!.Tag.Should().Be("v1.11.0");
        info.Assets.Should().HaveCount(2);

        var setup = info.Assets[0];
        setup.Name.Should().Be("Pulsar-v1.11.0-Setup.exe");
        setup.DownloadUrl.Should().Be("https://github.com/Smith-Rosco/Pulsar/releases/download/v1.11.0/Pulsar-v1.11.0-Setup.exe");
        setup.SizeBytes.Should().Be(5242880);
        // digest shape "sha256:<hex>" is normalized to the raw hex
        setup.Sha256.Should().Be("e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855");

        // second asset has no digest field → null (drives the size-verification fallback)
        info.Assets[1].Sha256.Should().BeNull();
        info.Assets[1].SizeBytes.Should().Be(83886080);
    }

    [Fact]
    public void TryParseRestJson_AssetWithoutSize_YieldsNullSize()
    {
        const string json = """{"tag_name":"v1.2.3","assets":[{"name":"a.zip","browser_download_url":"https://example.com/a.zip"}]}""";

        var ok = GitHubReleaseInfoParser.TryParseRestJson(json, out var info);

        ok.Should().BeTrue();
        info!.Assets.Should().ContainSingle();
        info.Assets[0].SizeBytes.Should().BeNull();
        info.Assets[0].Sha256.Should().BeNull();
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("not json at all")]
    [InlineData("[]")]
    [InlineData("{\"name\":\"no tag here\"}")]
    [InlineData("{\"tag_name\":\"\"}")]
    [InlineData("{\"tag_name\":null}")]
    public void TryParseRestJson_MalformedInputs_ReturnFalse(string json)
    {
        GitHubReleaseInfoParser.TryParseRestJson(json, out _).Should().BeFalse();
    }

    [Fact]
    public void TryParseAtomFeed_RealFeedShape_TakesFirstEntryTag()
    {
        var ok = GitHubReleaseInfoParser.TryParseAtomFeed(UpdateTestSamples.AtomFeed, out var info);

        ok.Should().BeTrue();
        info.Should().NotBeNull();
        info!.Tag.Should().Be("v1.11.0"); // first entry = latest, NOT v1.10.0
        info.Assets.Should().BeEmpty(); // feed carries no assets/digests
    }

    [Fact]
    public void TryParseAtomFeed_AbsoluteHttpEntryId_AlsoParses()
    {
        const string xml =
            """
            <?xml version="1.0" encoding="UTF-8"?>
            <feed xmlns="http://www.w3.org/2005/Atom">
              <entry>
                <id>https://github.com/Smith-Rosco/Pulsar/releases/tag/1.9.9</id>
                <title>1.9.9</title>
              </entry>
            </feed>
            """;

        var ok = GitHubReleaseInfoParser.TryParseAtomFeed(xml, out var info);

        ok.Should().BeTrue();
        info!.Tag.Should().Be("1.9.9");
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("<not-xml")]
    [InlineData("<feed xmlns=\"http://www.w3.org/2005/Atom\"><entry><title>no id</title></entry></feed>")]
    public void TryParseAtomFeed_MalformedInputs_ReturnFalse(string xml)
    {
        GitHubReleaseInfoParser.TryParseAtomFeed(xml, out _).Should().BeFalse();
    }

    [Theory]
    [InlineData("https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.11.0", "v1.11.0")]
    [InlineData("https://github.com/Smith-Rosco/Pulsar/releases/tag/1.11.0?foo=bar", "1.11.0")]
    [InlineData("https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.2.3#readme", "v1.2.3")]
    [InlineData("/Smith-Rosco/Pulsar/releases/tag/v1.0.0", "v1.0.0")]
    [InlineData("https://github.com/Smith-Rosco/Pulsar/releases/tag/v1.11%2E0", "v1.11.0")]
    public void TryParseRedirectLocation_ReleaseTagTargets_ExtractTag(string location, string expectedTag)
    {
        var ok = GitHubReleaseInfoParser.TryParseRedirectLocation(location, out var tag);

        ok.Should().BeTrue();
        tag.Should().Be(expectedTag);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("https://github.com/Smith-Rosco/Pulsar/releases")]
    [InlineData("https://example.com/other")]
    [InlineData("relative/path/no/tag")]
    public void TryParseRedirectLocation_NonTagTargets_ReturnFalse(string? location)
    {
        GitHubReleaseInfoParser.TryParseRedirectLocation(location, out _).Should().BeFalse();
    }
}
