namespace GensokyoLauncher.Models;

public sealed record ThcrapRelease(string Version, Uri DownloadUri);

public sealed record ThpracRelease(string Version, Uri DownloadUri);

public sealed record BootstrapProgress(string Message, double? Fraction = null);

public sealed record BootstrapResult(string Version, string InstallDirectory);

public sealed record ThcrapSyncProgress(
    string Message,
    string? PatchId = null,
    int CompletedFiles = 0,
    int TotalFiles = 0,
    double? Fraction = null);

public sealed record ThcrapSyncResult(IReadOnlyList<string> ArchivePaths);
