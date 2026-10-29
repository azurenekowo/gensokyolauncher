using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class GameDiscoveryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-tests-{Guid.NewGuid():N}");

    public GameDiscoveryServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task DiscoversKnownExecutablesAndIgnoresUnknownFiles()
    {
        var game = Directory.CreateDirectory(Path.Combine(_root, "Touhou 6"));
        await File.WriteAllTextAsync(Path.Combine(game.FullName, "th06.exe"), "test");
        await File.WriteAllTextAsync(Path.Combine(game.FullName, "config.exe"), "test");

        var result = await new GameDiscoveryService().DiscoverAsync(_root);

        var found = Assert.Single(result);
        Assert.Equal("th06", found.Definition.Id);
    }

    [Fact]
    public async Task DiscoversTouhouSixStandaloneEnglishWrapper()
    {
        var game = Directory.CreateDirectory(Path.Combine(_root, "Touhou 6"));
        var executable = Path.Combine(game.FullName, "th06 (thpatch-en).exe");
        await File.WriteAllTextAsync(executable, "test");

        var found = Assert.Single(await new GameDiscoveryService().DiscoverAsync(_root));

        Assert.Equal("th06", found.Definition.Id);
        Assert.Equal(Path.GetFullPath(executable), found.ExecutablePath);
    }

    [Fact]
    public async Task DoesNotUseOriginalJapaneseTouhouSixExecutableAsDiscoveryTarget()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "東方紅魔郷.exe"), "test");
        Assert.Empty(await new GameDiscoveryService().DiscoverAsync(_root));
    }

    [Fact]
    public async Task DuplicateExecutablesPreferTheShallowestPath()
    {
        var shallow = Path.Combine(_root, "th08.exe");
        var nestedDirectory = Directory.CreateDirectory(Path.Combine(_root, "copy", "nested"));
        await File.WriteAllTextAsync(shallow, "test");
        await File.WriteAllTextAsync(Path.Combine(nestedDirectory.FullName, "th08.exe"), "test");

        var result = await new GameDiscoveryService().DiscoverAsync(_root);

        Assert.Equal(Path.GetFullPath(shallow), Assert.Single(result).ExecutablePath);
    }

    [Fact]
    public async Task MissingRootReturnsEmptyResult()
    {
        var result = await new GameDiscoveryService().DiscoverAsync(Path.Combine(_root, "missing"));
        Assert.Empty(result);
    }

    [Fact]
    public async Task DiscoveryIsCaseInsensitiveForExecutableExtension()
    {
        var path = Path.Combine(_root, "TH10.EXE");
        await File.WriteAllTextAsync(path, "test");

        var result = await new GameDiscoveryService().DiscoverAsync(_root);

        Assert.Equal("th10", Assert.Single(result).Definition.Id);
    }

    [Fact]
    public async Task DiscoversPc98DiskImagesFromKnownNames()
    {
        var directory = Directory.CreateDirectory(Path.Combine(_root, "Touhou 4 Lotus Land Story"));
        var path = Path.Combine(directory.FullName, "th04.hdi");
        await File.WriteAllTextAsync(path, "test");

        var found = Assert.Single(await new GameDiscoveryService().DiscoverAsync(_root));

        Assert.Equal("th04", found.Definition.Id);
        Assert.Equal(GensokyoLauncher.Models.GameCategory.Pc98, found.Definition.Category);
        Assert.Equal(Path.GetFullPath(path), found.ExecutablePath);
    }

    [Fact]
    public async Task IgnoresUnidentifiedPc98Images()
    {
        await File.WriteAllTextAsync(Path.Combine(_root, "unrelated.hdi"), "test");
        Assert.Empty(await new GameDiscoveryService().DiscoverAsync(_root));
    }

    [Fact]
    public async Task DiscoversEnglishAndJapanesePc98DiskVariantsForOneTitle()
    {
        var english = Path.Combine(_root, "th01e.hdi");
        var japanese = Path.Combine(_root, "th01j.hdi");
        await File.WriteAllTextAsync(english, "english");
        await File.WriteAllTextAsync(japanese, "japanese");
        await File.WriteAllTextAsync(Path.Combine(_root, "th01e.ini"), "configuration");
        await File.WriteAllTextAsync(Path.Combine(_root, "th01e.bat"), "launcher");

        var found = Assert.Single(await new GameDiscoveryService().DiscoverAsync(_root));

        Assert.Equal("th01", found.Definition.Id);
        Assert.Equal(new[] { english, japanese }.OrderBy(x => x, StringComparer.OrdinalIgnoreCase),
            found.AvailableDiskImages);
    }

    public void Dispose() => Directory.Delete(_root, true);
}
