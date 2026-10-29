using System.Net;
using System.Collections.Concurrent;
using System.Text;
using System.Text.Json;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class ThcrapRepositoryServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-repository-{Guid.NewGuid():N}");

    public ThcrapRepositoryServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public async Task CatalogIncludesEveryOfficialLanguageEntry()
    {
        var handler = new FakeHttpHandler();
        handler.AddJson("https://srv.thpatch.net/repo.js", new
        {
            id = "thpatch",
            servers = new[] { "https://patches.test/" },
            patches = new Dictionary<string, string>
            {
                ["lang_en"] = "English language pack",
                ["lang_vi"] = "Vietnamese community translations",
                ["lang_ms-jawi"] = "Malay (Jawi script)",
                ["not_a_language"] = "Ignored"
            }
        });
        var service = CreateService(handler);

        var languages = await service.GetOfficialLanguagesAsync();

        Assert.Equal(["lang_en", "lang_ms-jawi", "lang_vi"],
            languages.Select(language => language.PatchId).OrderBy(id => id, StringComparer.Ordinal));
    }

    [Fact]
    public async Task SyncDownloadsSelectedGameAndReturnsDependenciesFirst()
    {
        var handler = CreateVietnameseRepository();
        var service = CreateService(handler);

        var result = await service.SyncForGameAsync("thpatch", "lang_vi", "th07");

        Assert.Equal(["repos/nmlgc/base_tsa/", "repos/thpatch/lang_vi/"], result.ArchivePaths);
        Assert.All(result.ArchivePaths, archive => Assert.True(Directory.Exists(
            Path.Combine(_root, "thcrap", archive.Replace('/', Path.DirectorySeparatorChar)))));
        var languageDirectory = Path.Combine(_root, "thcrap", "repos", "thpatch", "lang_vi");
        Assert.True(File.Exists(Path.Combine(languageDirectory, "th07.js")));
        Assert.True(File.Exists(Path.Combine(languageDirectory, "th07", "title.txt")));
        Assert.False(File.Exists(Path.Combine(languageDirectory, "th08", "other.txt")));
        var dependencyDirectory = Path.Combine(_root, "thcrap", "repos", "nmlgc", "base_tsa");
        Assert.True(File.Exists(Path.Combine(dependencyDirectory, "global.js")));
        Assert.True(File.Exists(Path.Combine(dependencyDirectory, "th07", "engine.js")));
    }

    [Fact]
    public async Task CrcFailureLeavesPreviousPatchUsable()
    {
        var handler = CreateVietnameseRepository();
        var service = CreateService(handler);
        await service.SyncForGameAsync("thpatch", "lang_vi", "th07");
        var installedFile = Path.Combine(_root, "thcrap", "repos", "thpatch", "lang_vi", "th07", "title.txt");
        var original = await File.ReadAllTextAsync(installedFile);

        handler.UpdateManifestEntry("https://files.test/lang_vi/files.js", "th07/title.txt",
            Crc32(Encoding.UTF8.GetBytes("expected update")));
        handler.AddBytes("https://files.test/lang_vi/th07/title.txt", Encoding.UTF8.GetBytes("corrupt update"));

        await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SyncForGameAsync("thpatch", "lang_vi", "th07"));
        Assert.Equal(original, await File.ReadAllTextAsync(installedFile));
    }

    [Fact]
    public async Task DependencyCyclesAreRejectedBeforeInstallation()
    {
        var handler = new FakeHttpHandler();
        AddRepository(handler, "https://srv.thpatch.net/repo.js", "thpatch", "https://patches.test/",
            new Dictionary<string, string> { ["lang_loop"] = "Loop" });
        AddPatch(handler, "https://patches.test/lang_loop/patch.js", "lang_loop", "https://files.test/lang_loop/",
            ["thpatch/lang_loop"]);
        var service = CreateService(handler);

        var error = await Assert.ThrowsAsync<InvalidDataException>(() =>
            service.SyncForGameAsync("thpatch", "lang_loop", "th07"));

        Assert.Contains("cycle", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ConcurrentSyncsShareDownloadedPatchFiles()
    {
        var handler = CreateVietnameseRepository();
        var service = CreateService(handler);

        await Task.WhenAll(
            service.SyncForGameAsync("thpatch", "lang_vi", "th07"),
            service.SyncForGameAsync("thpatch", "lang_vi", "th07"));

        Assert.Equal(1, handler.RequestCount("https://files.test/lang_vi/th07/title.txt"));
        Assert.Equal(1, handler.RequestCount("https://files.test/base_tsa/th07/engine.js"));
    }

    [Fact]
    public async Task CancellationPreservesLivePatchAndRemovesStagingDirectory()
    {
        var handler = new FakeHttpHandler();
        AddRepository(handler, "https://srv.thpatch.net/repo.js", "thpatch", "https://patches.test/",
            new Dictionary<string, string> { ["lang_slow"] = "Slow" });
        var patch = AddPatch(handler, "https://patches.test/lang_slow/patch.js", "lang_slow",
            "https://files.test/lang_slow/", []);
        var updated = Encoding.UTF8.GetBytes("updated");
        handler.AddJson("https://files.test/lang_slow/files.js", new Dictionary<string, uint>
        {
            ["patch.js"] = Crc32(patch),
            ["th07/data.txt"] = Crc32(updated)
        });
        handler.AddDelayedBytes("https://files.test/lang_slow/th07/data.txt", updated, TimeSpan.FromSeconds(5));
        var liveDirectory = Path.Combine(_root, "thcrap", "repos", "thpatch", "lang_slow");
        Directory.CreateDirectory(Path.Combine(liveDirectory, "th07"));
        var liveFile = Path.Combine(liveDirectory, "th07", "data.txt");
        await File.WriteAllTextAsync(liveFile, "previous");
        var service = CreateService(handler);
        using var cancellation = new CancellationTokenSource(TimeSpan.FromMilliseconds(100));

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() =>
            service.SyncForGameAsync("thpatch", "lang_slow", "th07", cancellationToken: cancellation.Token));

        Assert.Equal("previous", await File.ReadAllTextAsync(liveFile));
        Assert.Empty(Directory.EnumerateDirectories(Path.GetDirectoryName(liveDirectory)!, ".lang_slow.staging-*"));
    }

    [Fact]
    public async Task NullManifestEntriesRemoveStaleFilesWithoutDownloadingThem()
    {
        var handler = new FakeHttpHandler();
        AddRepository(handler, "https://srv.thpatch.net/repo.js", "thpatch", "https://patches.test/",
            new Dictionary<string, string> { ["lang_clean"] = "Clean" });
        var patch = AddPatch(handler, "https://patches.test/lang_clean/patch.js", "lang_clean",
            "https://files.test/lang_clean/", []);
        handler.AddJson("https://files.test/lang_clean/files.js", new Dictionary<string, object?>
        {
            ["patch.js"] = Crc32(patch),
            ["th07/removed.bin"] = null
        });
        var liveDirectory = Path.Combine(_root, "thcrap", "repos", "thpatch", "lang_clean", "th07");
        Directory.CreateDirectory(liveDirectory);
        var removedFile = Path.Combine(liveDirectory, "removed.bin");
        await File.WriteAllTextAsync(removedFile, "obsolete");
        var service = CreateService(handler);

        await service.SyncForGameAsync("thpatch", "lang_clean", "th07");

        Assert.False(File.Exists(removedFile));
        Assert.Equal(0, handler.RequestCount("https://files.test/lang_clean/th07/removed.bin"));
    }

    private ThcrapRepositoryService CreateService(HttpMessageHandler handler) =>
        new(new HttpClient(handler), new AppPaths(_root), TimeSpan.FromSeconds(2), maximumDownloads: 2);

    private static FakeHttpHandler CreateVietnameseRepository()
    {
        var handler = new FakeHttpHandler();
        AddRepository(handler, "https://srv.thpatch.net/repo.js", "thpatch", "https://patches.test/",
            new Dictionary<string, string> { ["lang_vi"] = "Vietnamese community translations" });
        AddRepository(handler, "https://mirrors.thpatch.net/nmlgc/repo.js", "nmlgc", "https://dependencies.test/",
            new Dictionary<string, string> { ["base_tsa"] = "Base TSA" });

        var languagePatch = AddPatch(handler, "https://patches.test/lang_vi/patch.js", "lang_vi",
            "https://files.test/lang_vi/", ["nmlgc/base_tsa"]);
        var languageFiles = new Dictionary<string, byte[]>
        {
            ["patch.js"] = languagePatch,
            ["th07.js"] = Encoding.UTF8.GetBytes("th07 config"),
            ["th07/title.txt"] = Encoding.UTF8.GetBytes("Vietnamese title"),
            ["th08/other.txt"] = Encoding.UTF8.GetBytes("not selected")
        };
        AddManifest(handler, "https://files.test/lang_vi/", languageFiles);

        var basePatch = AddPatch(handler, "https://dependencies.test/base_tsa/patch.js", "base_tsa",
            "https://files.test/base_tsa/", []);
        var baseFiles = new Dictionary<string, byte[]>
        {
            ["patch.js"] = basePatch,
            ["global.js"] = Encoding.UTF8.GetBytes("shared"),
            ["th07/engine.js"] = Encoding.UTF8.GetBytes("engine"),
            ["th08/engine.js"] = Encoding.UTF8.GetBytes("other engine")
        };
        AddManifest(handler, "https://files.test/base_tsa/", baseFiles);
        return handler;
    }

    private static void AddRepository(FakeHttpHandler handler, string url, string id, string server,
        Dictionary<string, string> patches) => handler.AddJson(url, new { id, servers = new[] { server }, patches });

    private static byte[] AddPatch(FakeHttpHandler handler, string url, string id, string server, string[] dependencies)
    {
        var bytes = JsonSerializer.SerializeToUtf8Bytes(new { id, title = id, servers = new[] { server }, dependencies });
        handler.AddBytes(url, bytes);
        return bytes;
    }

    private static void AddManifest(FakeHttpHandler handler, string baseUrl, Dictionary<string, byte[]> files)
    {
        var manifest = files.ToDictionary(file => file.Key, file => Crc32(file.Value));
        handler.AddJson(baseUrl + "files.js", manifest);
        foreach (var file in files.Where(file => file.Key != "patch.js")) handler.AddBytes(baseUrl + file.Key, file.Value);
    }

    private static uint Crc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }

    public void Dispose()
    {
        if (Directory.Exists(_root)) Directory.Delete(_root, true);
    }

    private sealed class FakeHttpHandler : HttpMessageHandler
    {
        private readonly Dictionary<string, FakeResponse> _responses = new(StringComparer.OrdinalIgnoreCase);
        private readonly ConcurrentDictionary<string, int> _requestCounts = new(StringComparer.OrdinalIgnoreCase);

        public void AddJson(string url, object value) => AddBytes(url, JsonSerializer.SerializeToUtf8Bytes(value));
        public void AddBytes(string url, byte[] bytes) => _responses[url] = new FakeResponse(bytes, TimeSpan.Zero);
        public void AddDelayedBytes(string url, byte[] bytes, TimeSpan delay) => _responses[url] = new FakeResponse(bytes, delay);
        public int RequestCount(string url) => _requestCounts.GetValueOrDefault(url);

        public void UpdateManifestEntry(string url, string path, uint crc)
        {
            var manifest = JsonSerializer.Deserialize<Dictionary<string, uint>>(_responses[url].Bytes)!;
            manifest[path] = crc;
            AddJson(url, manifest);
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var url = request.RequestUri!.AbsoluteUri;
            _requestCounts.AddOrUpdate(url, 1, (_, count) => count + 1);
            if (_responses.TryGetValue(url, out var response))
            {
                if (response.Delay > TimeSpan.Zero) await Task.Delay(response.Delay, cancellationToken);
                return new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent(response.Bytes) };
            }
            return new HttpResponseMessage(HttpStatusCode.NotFound)
            { RequestMessage = request, Content = new StringContent("missing fake response") };
        }

        private sealed record FakeResponse(byte[] Bytes, TimeSpan Delay);
    }
}
