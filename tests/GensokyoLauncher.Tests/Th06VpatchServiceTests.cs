using GensokyoLauncher.Models;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class Th06VpatchServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-vpatch-{Guid.NewGuid():N}");

    public Th06VpatchServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void WindowedProfileUsesVpatchWithProfileDimensionsAndSixtyFps()
    {
        var game = Path.Combine(_root, "東方紅魔郷.exe");
        var vpatch = Path.Combine(_root, "vpatch.exe");
        File.WriteAllText(game, "game");
        File.WriteAllText(vpatch, "vpatch");
        File.WriteAllText(Path.Combine(_root, "vpatch.ini"),
            "[Window]\r\nenabled = 0\r\nWidth = 640\r\nHeight = 480\r\n\r\n[Option]\r\nGameFPS = 9999\r\nVsync = 0\r\n");
        var profile = new LaunchProfile
        {
            DisplayMode = LaunchDisplayMode.Windowed,
            WindowWidth = 1280,
            WindowHeight = 960
        };

        var executable = new Th06VpatchService().PrepareLaunchExecutable(game, profile);

        Assert.Equal(vpatch, executable);
        var configuration = File.ReadAllText(Path.Combine(_root, "vpatch.ini"));
        Assert.Contains("enabled = 1", configuration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("Width = 1280", configuration);
        Assert.Contains("Height = 960", configuration);
        Assert.Contains("GameFPS = 60", configuration);
        Assert.Contains("Vsync = 0", configuration);
        var gameConfiguration = File.ReadAllBytes(Path.Combine(_root, "東方紅魔郷.cfg"));
        Assert.Equal(0x38, gameConfiguration.Length);
        Assert.Equal(1, gameConfiguration[0x1e]);
    }

    [Fact]
    public void WindowedProfilePreservesValidGameConfigurationWhileSettingWindowedFlag()
    {
        var game = Path.Combine(_root, "東方紅魔郷.exe");
        File.WriteAllText(game, "game");
        File.WriteAllText(Path.Combine(_root, "vpatch.exe"), "vpatch");
        var configuration = new byte[0x38];
        System.Buffers.Binary.BinaryPrimitives.WriteInt32LittleEndian(configuration.AsSpan(0x14, 4), 0x0102);
        configuration[0x18] = 4;
        configuration[0x1e] = 0;
        File.WriteAllBytes(Path.Combine(_root, "東方紅魔郷.cfg"), configuration);

        new Th06VpatchService().PrepareLaunchExecutable(game, new LaunchProfile { DisplayMode = LaunchDisplayMode.Windowed });

        var updated = File.ReadAllBytes(Path.Combine(_root, "東方紅魔郷.cfg"));
        Assert.Equal(4, updated[0x18]);
        Assert.Equal(1, updated[0x1e]);
    }

    [Fact]
    public void GameDefaultStillUsesVpatchForFrameLimitingWithoutChangingWindowMode()
    {
        var game = Path.Combine(_root, "東方紅魔郷.exe");
        var vpatch = Path.Combine(_root, "vpatch.exe");
        File.WriteAllText(game, "game");
        File.WriteAllText(vpatch, "vpatch");
        File.WriteAllText(Path.Combine(_root, "vpatch.ini"), "[Window]\nenabled = 0\n[Option]\nGameFPS = 4000\n");

        var executable = new Th06VpatchService().PrepareLaunchExecutable(game, LaunchProfile.OriginalJapanese());

        Assert.Equal(vpatch, executable);
        var configuration = File.ReadAllText(Path.Combine(_root, "vpatch.ini"));
        Assert.Contains("enabled = 0", configuration, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("GameFPS = 60", configuration);
    }

    [Fact]
    public void StandaloneEnglishWrapperUsesProfileManagedVpatchAndTracksJapaneseGameProcess()
    {
        var wrapper = Path.Combine(_root, "th06 (thpatch-en).exe");
        var game = Path.Combine(_root, "東方紅魔郷.exe");
        File.WriteAllText(wrapper, "wrapper");
        File.WriteAllText(game, "game");
        File.WriteAllText(Path.Combine(_root, "vpatch.exe"), "vpatch");
        var service = new Th06VpatchService();

        var executable = service.PrepareLaunchExecutable(wrapper,
            new LaunchProfile { DisplayMode = LaunchDisplayMode.Windowed });

        Assert.Equal(Path.Combine(_root, "vpatch.exe"), executable);
        Assert.Equal(game, service.ResolveGameProcessExecutable(wrapper));
        Assert.True(Th06VpatchService.IsStandaloneThpatchWrapper(wrapper));
        Assert.Equal(1, File.ReadAllBytes(Path.Combine(_root, "東方紅魔郷.cfg"))[0x1e]);
    }

    [Fact]
    public void OtherGamesAndIncompleteVpatchSetupsAreUnchanged()
    {
        var th07 = Path.Combine(_root, "th07.exe");
        var th06 = Path.Combine(_root, "th06.exe");
        File.WriteAllText(th07, "game");
        File.WriteAllText(th06, "game");
        File.WriteAllText(Path.Combine(_root, "vpatch.exe"), "vpatch");
        var service = new Th06VpatchService();

        Assert.Equal(th07, service.PrepareLaunchExecutable(th07, LaunchProfile.English()));
        Assert.Equal(th06, service.PrepareLaunchExecutable(th06, LaunchProfile.English()));
        Assert.False(File.Exists(Path.Combine(_root, "vpatch.ini")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }
}
