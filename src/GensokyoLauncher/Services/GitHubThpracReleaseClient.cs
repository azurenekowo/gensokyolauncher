using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public sealed class GitHubThpracReleaseClient : IThpracReleaseClient
{
    private readonly HttpClient _client;

    public GitHubThpracReleaseClient(HttpClient client)
    {
        _client = client;
        if (!_client.DefaultRequestHeaders.UserAgent.Any())
            _client.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("GensokyoLauncher", "0.1"));
    }

    public async Task<ThpracRelease> GetLatestAsync(CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromSeconds(30));
        var release = await _client.GetFromJsonAsync<GitHubRelease>(
            "https://api.github.com/repos/touhouworldcup/thprac/releases/latest", timeout.Token)
            ?? throw new InvalidOperationException("GitHub returned an empty thprac release response.");
        var asset = release.Assets.FirstOrDefault(item =>
            item.Name.StartsWith("thprac", StringComparison.OrdinalIgnoreCase)
            && item.Name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase));
        if (asset is null || string.IsNullOrWhiteSpace(release.TagName))
            throw new InvalidOperationException("The latest thprac release has no executable asset.");
        return new ThpracRelease(release.TagName, new Uri(asset.DownloadUrl));
    }

    public async Task DownloadAsync(Uri uri, Stream destination, IProgress<double>? progress = null,
        CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(TimeSpan.FromMinutes(3));
        using var response = await _client.GetAsync(uri, HttpCompletionOption.ResponseHeadersRead, timeout.Token);
        response.EnsureSuccessStatusCode();
        var length = response.Content.Headers.ContentLength;
        await using var source = await response.Content.ReadAsStreamAsync(timeout.Token);
        var buffer = new byte[81920];
        long total = 0;
        int read;
        while ((read = await source.ReadAsync(buffer, timeout.Token)) > 0)
        {
            await destination.WriteAsync(buffer.AsMemory(0, read), timeout.Token);
            total += read;
            if (length is > 0) progress?.Report((double)total / length.Value);
        }
    }

    private sealed class GitHubRelease
    {
        [JsonPropertyName("tag_name")] public string TagName { get; set; } = "";
        [JsonPropertyName("assets")] public List<GitHubAsset> Assets { get; set; } = [];
    }

    private sealed class GitHubAsset
    {
        [JsonPropertyName("name")] public string Name { get; set; } = "";
        [JsonPropertyName("browser_download_url")] public string DownloadUrl { get; set; } = "";
    }
}
