using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public sealed class ThpracBootstrapService(IThpracReleaseClient releaseClient, AppPaths paths) : IThpracBootstrapService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<BootstrapResult> EnsureInstalledAsync(string? installedVersion,
        IProgress<BootstrapProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
            var validInstall = File.Exists(paths.ThpracExecutable);
            if (validInstall)
                progress?.Report(new BootstrapProgress($"thprac {installedVersion ?? "installation"} is ready.", 1));
            progress?.Report(new BootstrapProgress(validInstall
                ? "thprac is ready; checking for updates…"
                : "Checking the latest thprac release…"));

            ThpracRelease release;
            try { release = await releaseClient.GetLatestAsync(cancellationToken); }
            catch when (validInstall && !cancellationToken.IsCancellationRequested)
            {
                progress?.Report(new BootstrapProgress($"thprac {installedVersion ?? "installation"} is ready (update check failed).", 1));
                return new BootstrapResult(installedVersion ?? "installed", paths.ThpracDirectory);
            }

            if (validInstall && string.Equals(installedVersion, release.Version, StringComparison.OrdinalIgnoreCase))
            {
                progress?.Report(new BootstrapProgress($"thprac {release.Version} is ready.", 1));
                return new BootstrapResult(release.Version, paths.ThpracDirectory);
            }

            Directory.CreateDirectory(paths.ThpracDirectory);
            var temporary = Path.Combine(paths.ThpracDirectory, $"thprac-{Guid.NewGuid():N}.download");
            try
            {
                progress?.Report(new BootstrapProgress($"Downloading thprac {release.Version}…", 0));
                await using (var target = new FileStream(temporary, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    var downloadProgress = new Progress<double>(fraction =>
                        progress?.Report(new BootstrapProgress($"Downloading thprac {fraction:P0}", fraction)));
                    await releaseClient.DownloadAsync(release.DownloadUri, target, downloadProgress, cancellationToken);
                }
                File.Move(temporary, paths.ThpracExecutable, true);
                progress?.Report(new BootstrapProgress($"thprac {release.Version} is ready.", 1));
                return new BootstrapResult(release.Version, paths.ThpracDirectory);
            }
            finally
            {
                if (File.Exists(temporary)) File.Delete(temporary);
            }
        }
        finally { _gate.Release(); }
    }
}
