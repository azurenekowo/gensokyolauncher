using System.Text.Json;
using GensokyoLauncher.Models;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class LaunchProfileTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-profiles-{Guid.NewGuid():N}");

    [Fact]
    public void EmptySettingsSeedJapaneseAndEnglishProfiles()
    {
        var settings = new AppSettings();
        settings.NormalizeProfiles();
        Assert.Equal(new[] { LaunchProfile.OriginalJapaneseId, LaunchProfile.EnglishId }, settings.LaunchProfiles.Select(x => x.Id));
        Assert.Equal(LaunchProfile.OriginalJapaneseId, settings.GlobalDefaultProfileId);
    }

    [Fact]
    public void MissingAssignmentFallsBackToGlobalThenJapanese()
    {
        var settings = new AppSettings();
        settings.NormalizeProfiles();
        settings.GlobalDefaultProfileId = LaunchProfile.EnglishId;
        settings.GameProfileAssignments["th06"] = "deleted";
        Assert.Equal(LaunchProfile.EnglishId, settings.ResolveProfile("th06").Id);
        settings.GlobalDefaultProfileId = "deleted";
        Assert.Equal(LaunchProfile.OriginalJapaneseId, settings.ResolveProfile().Id);
    }

    [Fact]
    public async Task RunConfigurationUsesSelectedLanguageAndEmptyJapaneseStack()
    {
        var service = new ThcrapRunConfigurationService(new AppPaths(_root));
        var english = await service.GenerateAsync(LaunchProfile.English(), "th06",
            ["repos/nmlgc/base_tsa/", "repos/thpatch/lang_en/"]);
        using (var document = JsonDocument.Parse(await File.ReadAllTextAsync(english)))
            Assert.Equal(["repos/nmlgc/base_tsa/", "repos/thpatch/lang_en/"], document.RootElement.GetProperty("patches")
                .EnumerateArray().Select(item => item.GetProperty("archive").GetString()));
        var japanese = await service.GenerateAsync(LaunchProfile.OriginalJapanese(), "th06", []);
        using var original = JsonDocument.Parse(await File.ReadAllTextAsync(japanese));
        Assert.Empty(original.RootElement.GetProperty("patches").EnumerateArray());
    }

    [Fact]
    public void DimensionsAreValidated()
    {
        var profile = new LaunchProfile { WindowWidth = 2, WindowHeight = 99999 };
        profile.Normalize();
        Assert.Equal(320, profile.WindowWidth);
        Assert.Equal(4320, profile.WindowHeight);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
