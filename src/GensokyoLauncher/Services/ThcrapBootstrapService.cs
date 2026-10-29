using System.IO.Compression;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public sealed class ThcrapBootstrapService(IThcrapReleaseClient releaseClient, AppPaths paths) : IThcrapBootstrapService
{
    private readonly SemaphoreSlim _gate = new(1, 1);

    public async Task<BootstrapResult> EnsureInstalledAsync(
        string? installedVersion,
        IProgress<BootstrapProgress>? progress = null,
        CancellationToken cancellationToken = default)
    {
        await _gate.WaitAsync(cancellationToken);
        try
        {
        var validInstall = Directory.Exists(paths.ThcrapDirectory)
            && Directory.EnumerateFiles(paths.ThcrapDirectory, "thcrap_loader.exe", SearchOption.AllDirectories).Any();
        if (validInstall)
            progress?.Report(new BootstrapProgress($"THCRAP {installedVersion ?? "installation"} is ready.", 1));
        progress?.Report(new BootstrapProgress(validInstall ? "THCRAP is ready; checking for updates…" : "Checking the latest THCRAP release…"));
        ThcrapRelease release;
        try { release = await releaseClient.GetLatestAsync(cancellationToken); }
        catch when (validInstall && !cancellationToken.IsCancellationRequested)
        {
            progress?.Report(new BootstrapProgress($"THCRAP {installedVersion ?? "installation"} is ready (update check failed).", 1));
            return new BootstrapResult(installedVersion ?? "installed", paths.ThcrapDirectory);
        }
        if (validInstall && string.Equals(installedVersion, release.Version, StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new BootstrapProgress($"THCRAP {release.Version} is ready.", 1));
            return new BootstrapResult(release.Version, paths.ThcrapDirectory);
        }

        Directory.CreateDirectory(paths.DataRoot);
        var temporaryZip = Path.Combine(paths.DataRoot, $"thcrap-{Guid.NewGuid():N}.zip");
        var staging = Path.Combine(paths.DataRoot, $"thcrap-staging-{Guid.NewGuid():N}");
        var backup = paths.ThcrapDirectory + ".previous";

        try
        {
            progress?.Report(new BootstrapProgress($"Downloading THCRAP {release.Version}…", 0));
            await using (var target = new FileStream(temporaryZip, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                var downloadProgress = new Progress<double>(fraction =>
                    progress?.Report(new BootstrapProgress($"Downloading THCRAP {fraction:P0}", fraction)));
                await releaseClient.DownloadAsync(release.DownloadUri, target, downloadProgress, cancellationToken);
            }

            progress?.Report(new BootstrapProgress("Installing THCRAP…"));
            Directory.CreateDirectory(staging);
            ExtractArchiveSafe(temporaryZip, staging);

            if (Directory.Exists(backup))
                Directory.Delete(backup, true);
            if (Directory.Exists(paths.ThcrapDirectory))
                Directory.Move(paths.ThcrapDirectory, backup);
            Directory.Move(staging, paths.ThcrapDirectory);
            if (Directory.Exists(backup))
                Directory.Delete(backup, true);

            progress?.Report(new BootstrapProgress($"THCRAP {release.Version} is ready.", 1));
            return new BootstrapResult(release.Version, paths.ThcrapDirectory);
        }
        catch
        {
            if (!Directory.Exists(paths.ThcrapDirectory) && Directory.Exists(backup))
                Directory.Move(backup, paths.ThcrapDirectory);
            throw;
        }
        finally
        {
            if (File.Exists(temporaryZip))
                File.Delete(temporaryZip);
            if (Directory.Exists(staging))
                Directory.Delete(staging, true);
        }
        }
        finally { _gate.Release(); }
    }

    public static void ExtractArchiveSafe(string archivePath, string destinationDirectory)
    {
        var root = Path.GetFullPath(destinationDirectory) + Path.DirectorySeparatorChar;
        using var archive = ZipFile.OpenRead(archivePath);
        foreach (var entry in archive.Entries)
        {
            var target = Path.GetFullPath(Path.Combine(destinationDirectory, entry.FullName));
            if (!target.StartsWith(root, StringComparison.OrdinalIgnoreCase))
                throw new InvalidDataException("The THCRAP archive contains an unsafe path.");

            if (string.IsNullOrEmpty(entry.Name))
            {
                Directory.CreateDirectory(target);
                continue;
            }

            Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            entry.ExtractToFile(target, true);
        }
    }
}
