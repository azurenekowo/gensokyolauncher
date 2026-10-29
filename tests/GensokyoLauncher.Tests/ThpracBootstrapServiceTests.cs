using GensokyoLauncher.Models;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class ThpracBootstrapServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-thprac-{Guid.NewGuid():N}");

    [Fact]
    public async Task DownloadsLatestExecutableAndReportsReady()
    {
        var paths = new AppPaths(_root);
        var client = new FakeReleaseClient("v2.3.1.1", [0x4d, 0x5a, 0x90]);
        var messages = new List<BootstrapProgress>();
        var service = new ThpracBootstrapService(client, paths);

        var result = await service.EnsureInstalledAsync(null, new InlineProgress<BootstrapProgress>(messages.Add));

        Assert.Equal("v2.3.1.1", result.Version);
        Assert.Equal(client.Payload, await File.ReadAllBytesAsync(paths.ThpracExecutable));
        Assert.Contains(messages, item => item.Message == "thprac v2.3.1.1 is ready.");
    }

    [Fact]
    public async Task MatchingInstallDoesNotDownloadAgain()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.ThpracDirectory);
        await File.WriteAllTextAsync(paths.ThpracExecutable, "existing");
        var client = new FakeReleaseClient("v2.3.1.1", [1, 2, 3]);
        var service = new ThpracBootstrapService(client, paths);

        await service.EnsureInstalledAsync("v2.3.1.1");

        Assert.Equal(0, client.DownloadCount);
        Assert.Equal("existing", await File.ReadAllTextAsync(paths.ThpracExecutable));
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class FakeReleaseClient(string version, byte[] payload) : IThpracReleaseClient
    {
        public byte[] Payload { get; } = payload;
        public int DownloadCount { get; private set; }

        public Task<ThpracRelease> GetLatestAsync(CancellationToken cancellationToken = default) =>
            Task.FromResult(new ThpracRelease(version, new Uri("https://example.test/thprac.exe")));

        public async Task DownloadAsync(Uri uri, Stream destination, IProgress<double>? progress = null,
            CancellationToken cancellationToken = default)
        {
            DownloadCount++;
            await destination.WriteAsync(Payload, cancellationToken);
            progress?.Report(1);
        }
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }
}
