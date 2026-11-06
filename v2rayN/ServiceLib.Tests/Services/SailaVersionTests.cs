namespace ServiceLib.Tests.Services;

public class SailaVersionTests
{
    // GetVersionInfo used AssemblyName.Version, which drops "-b4": an installed 0.1.4-b4 reported 0.1.4,
    // and 0.1.4 sorts above 0.1.4-b5, so the update check could never offer the next build.
    [Test]
    public async Task InformationalVersion_KeepsTheBuildSuffixAndDropsTheCommit()
    {
        await Utils.VersionFromInformational("0.1.4-b4+c5795784c7a4623a", new Version(0, 1, 4, 0)).Should().BeEqualTo("0.1.4-b4");
    }

    [Test]
    public async Task InstalledBuild_IsOlderThanTheNextBuild()
    {
        var installed = new SemanticVersion(Utils.VersionFromInformational("0.1.4-b4+c579", new Version(0, 1, 4, 0)));
        await (installed < new SemanticVersion("v0.1.4-b5")).Should().BeTrue();
    }

    [Test]
    public async Task NoInformationalVersion_FallsBackToTheAssemblyVersion()
    {
        await Utils.VersionFromInformational(null, new Version(1, 2, 3, 0)).Should().BeEqualTo("1.2.3");
    }

    // GitHub's /releases list is not ordered by version: on 2026-10-31 it gave b2, b1, then the b3 pre-release.
    [Test]
    public async Task PreReleaseCheck_PicksTheNewestReleaseWhateverTheListOrder()
    {
        var list = new List<GitHubRelease>
        {
            new() { TagName = "v0.1.4-b2" },
            new() { TagName = "v0.1.4-b1" },
            new() { TagName = "v0.1.4-b3", Prerelease = true },
        };
        await UpdateService.PickRelease(list, preRelease: true)!.TagName.Should().BeEqualTo("v0.1.4-b3");
        await UpdateService.PickRelease(list, preRelease: false)!.TagName.Should().BeEqualTo("v0.1.4-b2");
    }

    // Through a shared VPN exit GitHub's API answers 403 with an object; this used to become version 0.0.0.
    [Test]
    public async Task RateLimitAnswer_IsNotAReleaseList()
    {
        var list = UpdateService.ParseApiReleases("{\"message\":\"API rate limit exceeded\",\"documentation_url\":\"https://docs.github.com\"}");
        await list.Count.Should().BeEqualTo(0);
    }

    [Test]
    public async Task AtomFeed_GivesTheTagsAndPicksTheNewest()
    {
        const string atom = "<feed><entry><link rel=\"alternate\" href=\"https://github.com/Plus98ir/Saila/releases/tag/v0.1.6-b1\"/><id>tag:github.com,2008:Repository/1/v0.1.6-b1</id></entry>"
            + "<entry><link rel=\"alternate\" href=\"https://github.com/Plus98ir/Saila/releases/tag/v0.1.6-b3\"/></entry>"
            + "<entry><link rel=\"alternate\" href=\"https://github.com/Plus98ir/Saila/releases/tag/v0.1.6-b2\"/></entry></feed>";
        var list = UpdateService.ParseAtomReleases(atom);
        await list.Count.Should().BeEqualTo(3);
        await UpdateService.PickRelease(list, true)!.TagName.Should().BeEqualTo("v0.1.6-b3");
    }
}
