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

    [Test]
    public async Task JsonSubscription_KeepsOnlyMasqueAndXdriveProxyOutbounds()
    {
        const string json = """
            [{"remarks":"Plain","outbounds":[{"protocol":"vless","tag":"proxy","settings":{},"streamSettings":{"network":"ws"}},{"protocol":"freedom","tag":"direct"}]},
             {"remarks":"Masque","outbounds":[{"protocol":"masque","tag":"proxy","settings":{},"streamSettings":{"network":"masque"}},{"protocol":"freedom","tag":"direct"}]},
             {"remarks":"Drive","outbounds":[{"protocol":"vless","tag":"proxy","settings":{},"streamSettings":{"network":"xdrive"}},{"protocol":"blackhole","tag":"block"}]}]
            """;
        var list = SubscriptionHandler.SelectJsonOnlyOutbounds(json);
        await list.Count.Should().BeEqualTo(2);
        await list[0].Remarks.Should().BeEqualTo("Masque");
        await list[1].Remarks.Should().BeEqualTo("Drive");
        await list[1].Outbound["protocol"]!.ToString().Should().BeEqualTo("vless");
    }

    [Test]
    public async Task JsonSubscription_IgnoresAnswersThatAreNotAList()
    {
        await SubscriptionHandler.SelectJsonOnlyOutbounds("<html></html>").Count.Should().BeEqualTo(0);
        await SubscriptionHandler.FindJsonSubUrl("x \"subJsonUrl\":\"https://p.example/j/1\" y").Should().BeEqualTo("https://p.example/j/1");
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

    // 2026-11-25: v0.1.8-b4 was Android-only and carried the b3 zip; Windows took it as "latest" and would
    // reinstall b3 on every check.
    [Test]
    public async Task AndroidOnlyRelease_IsSkippedByWindows()
    {
        var list = new List<GitHubRelease>
        {
            new() { TagName = "v0.1.8-b4", Name = "Saila 0.1.8-b4 (Android)" },
            new() { TagName = "v0.1.8-b3", Name = "Saila 0.1.8-b3", Body = "<!-- android -->a<!-- /android --><!-- windows -->w<!-- /windows -->" },
            new() { TagName = "v0.1.8-b2", Name = "Saila 0.1.8-b2 (Windows)" },
        };
        await UpdateService.PickRelease(list, preRelease: false, skipAndroidOnly: true)!.TagName.Should().BeEqualTo("v0.1.8-b3");
        await UpdateService.PickRelease(list, preRelease: false)!.TagName.Should().BeEqualTo("v0.1.8-b4");
        await UpdateService.IsAndroidOnly(new GitHubRelease { TagName = "v1.0.0-b1", Body = "<!-- android -->only<!-- /android -->" }).Should().BeTrue();
        await UpdateService.IsAndroidOnly(list[2]).Should().BeFalse();
    }

    [Test]
    public async Task AtomFeed_KeepsTheTitlesSoAndroidOnlyCanBeSkipped()
    {
        const string atom = "<feed><title>Release notes from Saila</title>"
            + "<entry><id>tag:github.com,2008:Repository/1/v0.1.8-b4</id><link rel=\"alternate\" type=\"text/html\" href=\"https://github.com/Plus98ir/Saila/releases/tag/v0.1.8-b4\"/><title>Saila 0.1.8-b4 (Android)</title></entry>"
            + "<entry><link rel=\"alternate\" href=\"https://github.com/Plus98ir/Saila/releases/tag/v0.1.8-b3\"/><title>Saila 0.1.8-b3</title></entry></feed>";
        var list = UpdateService.ParseAtomReleases(atom);
        await list.Count.Should().BeEqualTo(2);
        await list[0].Name.Should().BeEqualTo("Saila 0.1.8-b4 (Android)");
        await UpdateService.PickRelease(list, preRelease: false, skipAndroidOnly: true)!.TagName.Should().BeEqualTo("v0.1.8-b3");
    }
}
