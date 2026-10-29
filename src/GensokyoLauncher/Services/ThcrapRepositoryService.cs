using System.Collections.Concurrent;
using System.Text.Json;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public sealed class ThcrapRepositoryService : IThcrapRepositoryService, IThcrapPatchSynchronizer
{
    private static readonly IReadOnlyDictionary<string, Uri[]> OfficialRepositories =
        new Dictionary<string, Uri[]>(StringComparer.OrdinalIgnoreCase)
        {
            ["thpatch"] = [new Uri("https://srv.thpatch.net/")],
            ["nmlgc"] = [new Uri("https://mirrors.thpatch.net/nmlgc/")]
        };

    private static readonly ConcurrentDictionary<string, SemaphoreSlim> PatchLocks = new(StringComparer.OrdinalIgnoreCase);
    private readonly HttpClient _httpClient;
    private readonly AppPaths _paths;
    private readonly TimeSpan _requestTimeout;
    private readonly int _maximumDownloads;

    public ThcrapRepositoryService(HttpClient httpClient, AppPaths paths, TimeSpan? requestTimeout = null,
        int maximumDownloads = 4)
    {
        _httpClient = httpClient;
        _paths = paths;
        _requestTimeout = requestTimeout ?? TimeSpan.FromSeconds(30);
        _maximumDownloads = Math.Max(1, maximumDownloads);
    }

    public async Task<IReadOnlyList<OfficialLanguage>> GetOfficialLanguagesAsync(CancellationToken cancellationToken = default)
    {
        var repository = await LoadRepositoryAsync("thpatch", cancellationToken, preferRemote: true);
        return repository.Patches
            .Where(patch => patch.Key.StartsWith("lang_", StringComparison.OrdinalIgnoreCase))
            .Select(patch => new OfficialLanguage(
                patch.Key,
                patch.Value,
                RepositoryId: repository.Id,
                IsInstalled: Directory.Exists(GetPatchDirectory(repository.Id, patch.Key))))
            .OrderBy(language => language.Name, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(language => language.PatchId, StringComparer.OrdinalIgnoreCase)
            .ToArray();
    }

    public async Task<ThcrapSyncResult> SyncForGameAsync(string repositoryId, string patchId, string gameId,
        IProgress<ThcrapSyncProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(repositoryId) || string.IsNullOrWhiteSpace(patchId) || string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Repository, patch, and game IDs are required.");
        ValidateId(repositoryId, "repository");
        ValidateId(patchId, "patch");
        ValidateId(gameId, "game");

        progress?.Report(new ThcrapSyncProgress($"Resolving {patchId}…", patchId));
        var ordered = new List<ResolvedPatch>();
        var states = new Dictionary<string, VisitState>(StringComparer.OrdinalIgnoreCase);
        await ResolveAsync(repositoryId, patchId, states, ordered, cancellationToken);

        foreach (var patch in ordered)
        {
            cancellationToken.ThrowIfCancellationRequested();
            await SynchronizePatchAsync(patch, gameId, progress, cancellationToken);
        }

        return new ThcrapSyncResult(ordered.Select(patch => $"repos/{patch.RepositoryId}/{patch.PatchId}/").ToArray());
    }

    private async Task ResolveAsync(string repositoryId, string patchId, Dictionary<string, VisitState> states,
        List<ResolvedPatch> ordered, CancellationToken cancellationToken)
    {
        var key = $"{repositoryId}/{patchId}";
        if (states.TryGetValue(key, out var state))
        {
            if (state == VisitState.Visiting)
                throw new InvalidDataException($"THCRAP patch dependency cycle detected at {key}.");
            return;
        }

        states[key] = VisitState.Visiting;
        var repository = await LoadRepositoryAsync(repositoryId, cancellationToken);
        if (!repository.Patches.ContainsKey(patchId))
            throw new InvalidDataException($"THCRAP repository '{repositoryId}' does not contain patch '{patchId}'.");

        var patchBytes = await DownloadFromServersAsync(repository.Servers, $"{patchId}/patch.js", cancellationToken);
        var metadata = ParsePatch(repositoryId, patchId, patchBytes, repository.Servers);
        foreach (var dependency in metadata.Dependencies)
        {
            var slash = dependency.IndexOf('/');
            var dependencyRepository = slash > 0 ? dependency[..slash] : repositoryId;
            var dependencyPatch = slash > 0 ? dependency[(slash + 1)..] : dependency;
            ValidateId(dependencyRepository, "dependency repository");
            ValidateId(dependencyPatch, "dependency patch");
            await ResolveAsync(dependencyRepository, dependencyPatch, states, ordered, cancellationToken);
        }

        states[key] = VisitState.Complete;
        ordered.Add(metadata);
    }

    private async Task SynchronizePatchAsync(ResolvedPatch patch, string gameId,
        IProgress<ThcrapSyncProgress>? progress, CancellationToken cancellationToken)
    {
        var key = $"{patch.RepositoryId}/{patch.PatchId}";
        var gate = PatchLocks.GetOrAdd(key, _ => new SemaphoreSlim(1, 1));
        await gate.WaitAsync(cancellationToken);
        try
        {
            progress?.Report(new ThcrapSyncProgress($"Checking {patch.Title}…", patch.PatchId));
            var manifestBytes = await DownloadFromServersAsync(patch.Servers, "files.js", cancellationToken);
            var manifest = ParseManifest(manifestBytes);
            var explicitPaths = CollectExplicitManifestPaths(patch.PatchJson, manifest.Keys);
            var selected = manifest
                .Where(file => IsRelevantFile(file.Key, gameId, explicitPaths))
                .OrderBy(file => file.Key, StringComparer.Ordinal)
                .ToArray();

            var liveDirectory = GetPatchDirectory(patch.RepositoryId, patch.PatchId);
            var parent = Path.GetDirectoryName(liveDirectory)!;
            Directory.CreateDirectory(parent);
            var stagingDirectory = Path.Combine(parent, $".{patch.PatchId}.staging-{Guid.NewGuid():N}");
            Directory.CreateDirectory(stagingDirectory);

            try
            {
                if (Directory.Exists(liveDirectory)) CopyDirectory(liveDirectory, stagingDirectory);
                await WriteAtomicAsync(Path.Combine(stagingDirectory, "patch.js"), patch.PatchBytes, cancellationToken);
                await WriteAtomicAsync(Path.Combine(stagingDirectory, "files.js"), manifestBytes, cancellationToken);

                if (manifest.TryGetValue("patch.js", out var patchCrc) && patchCrc.HasValue
                    && ComputeCrc32(patch.PatchBytes) != patchCrc.Value)
                    throw new InvalidDataException($"CRC check failed for {key}/patch.js.");

                var completed = selected.Count(file =>
                    !file.Value.HasValue
                    || file.Key.Equals("patch.js", StringComparison.OrdinalIgnoreCase)
                    || file.Key.Equals("files.js", StringComparison.OrdinalIgnoreCase));
                foreach (var deleted in selected.Where(file => !file.Value.HasValue))
                {
                    var destination = GetSafeDestination(stagingDirectory, deleted.Key);
                    if (File.Exists(destination)) File.Delete(destination);
                }
                using var downloads = new SemaphoreSlim(_maximumDownloads, _maximumDownloads);
                var tasks = selected
                    .Where(file => file.Value.HasValue)
                    .Where(file => !file.Key.Equals("patch.js", StringComparison.OrdinalIgnoreCase)
                        && !file.Key.Equals("files.js", StringComparison.OrdinalIgnoreCase))
                    .Select(async file =>
                    {
                        var destination = GetSafeDestination(stagingDirectory, file.Key);
                        var downloaded = false;
                        if (!await HasExpectedCrcAsync(destination, file.Value!.Value, cancellationToken))
                        {
                            await downloads.WaitAsync(cancellationToken);
                            try
                            {
                                var bytes = await DownloadFromServersAsync(patch.Servers, file.Key, cancellationToken);
                                if (ComputeCrc32(bytes) != file.Value.Value)
                                    throw new InvalidDataException($"CRC check failed for {key}/{file.Key}.");
                                await WriteAtomicAsync(destination, bytes, cancellationToken);
                                downloaded = true;
                            }
                            finally { downloads.Release(); }
                        }

                        var current = Interlocked.Increment(ref completed);
                        var fraction = selected.Length == 0 ? 1d : (double)current / selected.Length;
                        progress?.Report(new ThcrapSyncProgress(
                            $"{(downloaded ? "Downloading" : "Checking")} {patch.Title}… {Math.Round(fraction * 100)}%",
                            patch.PatchId, current, selected.Length, fraction));
                    })
                    .ToArray();
                await Task.WhenAll(tasks);

                foreach (var file in selected)
                {
                    var destination = GetSafeDestination(stagingDirectory, file.Key);
                    if (file.Value.HasValue && !await HasExpectedCrcAsync(destination, file.Value.Value, cancellationToken))
                        throw new InvalidDataException($"CRC check failed for staged file {key}/{file.Key}.");
                    if (!file.Value.HasValue && File.Exists(destination))
                        throw new InvalidDataException($"Deleted THCRAP file remained in staging: {key}/{file.Key}.");
                }

                ReplaceDirectory(stagingDirectory, liveDirectory);
                progress?.Report(new ThcrapSyncProgress($"{patch.Title} is ready.", patch.PatchId,
                    selected.Length, selected.Length, 1));
            }
            catch
            {
                TryDeleteDirectory(stagingDirectory);
                throw;
            }
        }
        finally { gate.Release(); }
    }

    private async Task<RepositoryMetadata> LoadRepositoryAsync(string repositoryId, CancellationToken cancellationToken,
        bool preferRemote = false)
    {
        ValidateId(repositoryId, "repository");
        var localFile = Path.Combine(_paths.ThcrapDirectory, "repos", repositoryId, "repo.js");
        if (!preferRemote && File.Exists(localFile))
        {
            try
            {
                var bytes = await File.ReadAllBytesAsync(localFile, cancellationToken);
                return ParseRepository(bytes, repositoryId);
            }
            catch (JsonException) { }
        }

        if (!OfficialRepositories.TryGetValue(repositoryId, out var servers))
            throw new InvalidDataException($"Unknown THCRAP repository '{repositoryId}'.");
        try
        {
            var downloaded = await DownloadFromServersAsync(servers, "repo.js", cancellationToken);
            var repository = ParseRepository(downloaded, repositoryId);
            await WriteAtomicAsync(localFile, downloaded, cancellationToken);
            return repository;
        }
        catch when (preferRemote && File.Exists(localFile) && !cancellationToken.IsCancellationRequested)
        {
            var bytes = await File.ReadAllBytesAsync(localFile, cancellationToken);
            return ParseRepository(bytes, repositoryId);
        }
    }

    private async Task<byte[]> DownloadFromServersAsync(IReadOnlyList<Uri> servers, string relativePath,
        CancellationToken cancellationToken)
    {
        Exception? lastError = null;
        foreach (var server in servers)
        {
            for (var attempt = 1; attempt <= 3; attempt++)
            {
                using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                timeout.CancelAfter(_requestTimeout);
                try
                {
                    var uri = new Uri(EnsureTrailingSlash(server), EscapePath(relativePath));
                    using var response = await _httpClient.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
                    response.EnsureSuccessStatusCode();
                    return await response.Content.ReadAsByteArrayAsync(timeout.Token);
                }
                catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
                {
                    lastError = new TimeoutException($"Timed out downloading {relativePath} from {server}.");
                }
                catch (Exception exception) when (exception is HttpRequestException or IOException)
                {
                    lastError = exception;
                }

                if (attempt < 3) await Task.Delay(TimeSpan.FromMilliseconds(attempt * 250), cancellationToken);
            }
        }

        throw new HttpRequestException($"Could not download THCRAP file '{relativePath}' from any official server.", lastError);
    }

    private static RepositoryMetadata ParseRepository(byte[] bytes, string expectedId)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : expectedId;
        if (!string.Equals(id, expectedId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected THCRAP repository '{expectedId}', but received '{id}'.");
        var servers = ReadUris(root, "servers");
        if (servers.Count == 0 && OfficialRepositories.TryGetValue(expectedId, out var official)) servers.AddRange(official);
        var patches = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (root.TryGetProperty("patches", out var patchElement))
            foreach (var property in patchElement.EnumerateObject())
                patches[property.Name] = property.Value.ValueKind == JsonValueKind.String
                    ? property.Value.GetString() ?? property.Name
                    : property.Name;
        return new RepositoryMetadata(id ?? expectedId, servers, patches);
    }

    private static ResolvedPatch ParsePatch(string repositoryId, string expectedPatchId, byte[] bytes,
        IReadOnlyList<Uri> repositoryServers)
    {
        using var document = JsonDocument.Parse(bytes);
        var root = document.RootElement;
        var id = root.TryGetProperty("id", out var idElement) ? idElement.GetString() : expectedPatchId;
        if (!string.Equals(id, expectedPatchId, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Expected THCRAP patch '{expectedPatchId}', but received '{id}'.");
        var dependencies = root.TryGetProperty("dependencies", out var dependencyElement)
            ? dependencyElement.EnumerateArray().Select(item => item.GetString()).Where(item => !string.IsNullOrWhiteSpace(item)).Cast<string>().ToArray()
            : [];
        var servers = ReadUris(root, "servers");
        if (servers.Count == 0)
            servers.AddRange(repositoryServers.Select(server => new Uri(EnsureTrailingSlash(server), expectedPatchId + "/")));
        var title = root.TryGetProperty("title", out var titleElement) ? titleElement.GetString() : expectedPatchId;
        return new ResolvedPatch(repositoryId, expectedPatchId, title ?? expectedPatchId, dependencies, servers,
            bytes, root.Clone());
    }

    private static Dictionary<string, uint?> ParseManifest(byte[] bytes)
    {
        using var document = JsonDocument.Parse(bytes);
        var files = new Dictionary<string, uint?>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in document.RootElement.EnumerateObject())
        {
            if (property.Value.ValueKind == JsonValueKind.Null)
            {
                files[property.Name.Replace('\\', '/')] = null;
                continue;
            }
            var value = property.Value.GetInt64();
            if (value < int.MinValue || value > uint.MaxValue)
                throw new InvalidDataException($"Invalid CRC value for '{property.Name}'.");
            files[property.Name.Replace('\\', '/')] = unchecked((uint)value);
        }
        return files;
    }

    private static HashSet<string> CollectExplicitManifestPaths(JsonElement patchJson, IEnumerable<string> manifestPaths)
    {
        var manifest = manifestPaths.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var result = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        Walk(patchJson);
        return result;

        void Walk(JsonElement element)
        {
            switch (element.ValueKind)
            {
                case JsonValueKind.Object:
                    foreach (var property in element.EnumerateObject())
                    {
                        if (manifest.Contains(property.Name)) result.Add(property.Name);
                        Walk(property.Value);
                    }
                    break;
                case JsonValueKind.Array:
                    foreach (var child in element.EnumerateArray()) Walk(child);
                    break;
                case JsonValueKind.String:
                    var value = element.GetString();
                    if (value is not null && manifest.Contains(value)) result.Add(value);
                    break;
            }
        }
    }

    private static bool IsRelevantFile(string path, string gameId, ISet<string> explicitPaths) =>
        explicitPaths.Contains(path)
        || !path.Contains('/')
        || path.StartsWith(gameId + "/", StringComparison.OrdinalIgnoreCase);

    private static List<Uri> ReadUris(JsonElement root, string propertyName)
    {
        var result = new List<Uri>();
        if (!root.TryGetProperty(propertyName, out var element) || element.ValueKind != JsonValueKind.Array) return result;
        foreach (var item in element.EnumerateArray())
            if (Uri.TryCreate(item.GetString(), UriKind.Absolute, out var uri)) result.Add(uri);
        return result;
    }

    private string GetPatchDirectory(string repositoryId, string patchId) =>
        Path.Combine(_paths.ThcrapDirectory, "repos", repositoryId, patchId);

    private static string GetSafeDestination(string root, string relativePath)
    {
        var normalized = relativePath.Replace('/', Path.DirectorySeparatorChar);
        var rootPath = Path.GetFullPath(root) + Path.DirectorySeparatorChar;
        var destination = Path.GetFullPath(Path.Combine(root, normalized));
        if (!destination.StartsWith(rootPath, StringComparison.OrdinalIgnoreCase))
            throw new InvalidDataException($"Unsafe path in THCRAP manifest: {relativePath}");
        return destination;
    }

    private static async Task<bool> HasExpectedCrcAsync(string path, uint expected, CancellationToken cancellationToken)
    {
        if (!File.Exists(path)) return false;
        var bytes = await File.ReadAllBytesAsync(path, cancellationToken);
        return ComputeCrc32(bytes) == expected;
    }

    internal static uint ComputeCrc32(ReadOnlySpan<byte> bytes)
    {
        uint crc = uint.MaxValue;
        foreach (var value in bytes)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++) crc = (crc >> 1) ^ (0xEDB88320u & (uint)-(int)(crc & 1));
        }
        return ~crc;
    }

    private static async Task WriteAtomicAsync(string path, byte[] bytes, CancellationToken cancellationToken)
    {
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        var temporary = path + $".tmp-{Guid.NewGuid():N}";
        try
        {
            await File.WriteAllBytesAsync(temporary, bytes, cancellationToken);
            File.Move(temporary, path, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }

    private static void CopyDirectory(string source, string destination)
    {
        foreach (var directory in Directory.EnumerateDirectories(source, "*", SearchOption.AllDirectories))
            Directory.CreateDirectory(Path.Combine(destination, Path.GetRelativePath(source, directory)));
        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(destination, Path.GetRelativePath(source, file));
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target, true);
        }
    }

    private static void ReplaceDirectory(string staging, string live)
    {
        var backup = live + $".backup-{Guid.NewGuid():N}";
        var hadLiveDirectory = Directory.Exists(live);
        if (hadLiveDirectory) Directory.Move(live, backup);
        try
        {
            Directory.Move(staging, live);
            if (hadLiveDirectory) TryDeleteDirectory(backup);
        }
        catch
        {
            if (!Directory.Exists(live) && Directory.Exists(backup)) Directory.Move(backup, live);
            throw;
        }
    }

    private static void TryDeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, true); }
        catch { }
    }

    private static Uri EnsureTrailingSlash(Uri uri) => uri.AbsoluteUri.EndsWith('/') ? uri : new Uri(uri.AbsoluteUri + "/");
    private static string EscapePath(string path) => string.Join('/', path.Replace('\\', '/').Split('/').Select(Uri.EscapeDataString));

    private static void ValidateId(string value, string kind)
    {
        if (value.Length == 0 || value.Any(character => !(char.IsAsciiLetterOrDigit(character) || character is '_' or '-')))
            throw new InvalidDataException($"Invalid THCRAP {kind} ID '{value}'.");
    }

    private enum VisitState { Visiting, Complete }
    private sealed record RepositoryMetadata(string Id, IReadOnlyList<Uri> Servers, IReadOnlyDictionary<string, string> Patches);
    private sealed record ResolvedPatch(string RepositoryId, string PatchId, string Title,
        IReadOnlyList<string> Dependencies, IReadOnlyList<Uri> Servers, byte[] PatchBytes, JsonElement PatchJson);
}
