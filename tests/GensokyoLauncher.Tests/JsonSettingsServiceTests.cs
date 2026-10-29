using GensokyoLauncher.Models;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class JsonSettingsServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-settings-{Guid.NewGuid():N}");

    [Fact]
    public async Task SettingsRoundTrip()
    {
        var service = new JsonSettingsService(new AppPaths(_root));
        var expected = new AppSettings
        {
            GamesRoot = "/games",
            WineCommand = "wine64",
            ThcrapVersion = "2026-10-21",
            ThpracVersion = "v2.3.1.1",
            AppearanceBackdropMode = 1,
            TabAnimationMode = 2,
            GameExecutableOverrides = new() { ["th06"] = "/games/th06-custom.exe" },
            LaunchProfiles = [new LaunchProfile { Id = "practice", Name = "Practice", UseThprac = true }]
        };

        await service.SaveAsync(expected);
        var actual = await service.LoadAsync();

        Assert.Equal(expected.GamesRoot, actual.GamesRoot);
        Assert.Equal(expected.WineCommand, actual.WineCommand);
        Assert.Equal(expected.ThcrapVersion, actual.ThcrapVersion);
        Assert.Equal(expected.ThpracVersion, actual.ThpracVersion);
        Assert.Equal(expected.AppearanceBackdropMode, actual.AppearanceBackdropMode);
        Assert.Equal(expected.TabAnimationMode, actual.TabAnimationMode);
        Assert.True(Assert.Single(actual.LaunchProfiles).UseThprac);
        Assert.Equal("/games/th06-custom.exe", actual.GameExecutableOverrides["th06"]);
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
