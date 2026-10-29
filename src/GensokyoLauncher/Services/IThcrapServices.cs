using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public interface IThcrapReleaseClient
{
    Task<ThcrapRelease> GetLatestAsync(CancellationToken cancellationToken = default);
    Task DownloadAsync(Uri uri, Stream destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

public interface IThcrapBootstrapService
{
    Task<BootstrapResult> EnsureInstalledAsync(
        string? installedVersion,
        IProgress<BootstrapProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IThpracReleaseClient
{
    Task<ThpracRelease> GetLatestAsync(CancellationToken cancellationToken = default);
    Task DownloadAsync(Uri uri, Stream destination, IProgress<double>? progress = null, CancellationToken cancellationToken = default);
}

public interface IThpracBootstrapService
{
    Task<BootstrapResult> EnsureInstalledAsync(
        string? installedVersion,
        IProgress<BootstrapProgress>? progress = null,
        CancellationToken cancellationToken = default);
}

public interface IThpracGameAttacher
{
    Task AttachAsync(string gameExecutable, DateTimeOffset launchedAt, string wineCommand,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public interface IThcrapRunConfigurationService
{
    Task<string> GenerateAsync(LaunchProfile profile, string gameId, IReadOnlyList<string> archivePaths,
        CancellationToken cancellationToken = default);
}

public interface IThcrapRepositoryService
{
    Task<IReadOnlyList<OfficialLanguage>> GetOfficialLanguagesAsync(CancellationToken cancellationToken = default);
}

public interface IThcrapPatchSynchronizer
{
    Task<ThcrapSyncResult> SyncForGameAsync(string repositoryId, string patchId, string gameId,
        IProgress<ThcrapSyncProgress>? progress = null, CancellationToken cancellationToken = default);
}

public interface IThcrapGameLauncher
{
    Task LaunchAsync(string gameExecutable, string configurationPath, LaunchProfile profile, string wineCommand,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default);
}

public interface IGameWindowManager
{
    Task<string?> ApplyAsync(int processId, string executablePath, DateTimeOffset launchedAt,
        LaunchProfile profile, CancellationToken cancellationToken = default);
}
