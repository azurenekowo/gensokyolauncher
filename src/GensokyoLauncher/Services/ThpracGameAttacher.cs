using System.Diagnostics;

namespace GensokyoLauncher.Services;

public sealed class ThpracGameAttacher(AppPaths paths) : IThpracGameAttacher
{
    public async Task AttachAsync(string gameExecutable, DateTimeOffset launchedAt, string wineCommand,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.ThpracExecutable))
            throw new FileNotFoundException("thprac is not installed yet. Check Settings and retry.", paths.ThpracExecutable);

        var info = new ProcessStartInfo
        {
            UseShellExecute = false,
            WorkingDirectory = paths.ThpracDirectory
        };
        if (OperatingSystem.IsWindows())
        {
            progress?.Report("Waiting for the game before attaching thprac…");
            var processId = await WaitForGameAsync(gameExecutable, launchedAt, cancellationToken);
            info.FileName = paths.ThpracExecutable;
            info.ArgumentList.Add("--attach");
            info.ArgumentList.Add(processId.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }
        else
        {
            await Task.Delay(TimeSpan.FromSeconds(1), cancellationToken);
            info.FileName = string.IsNullOrWhiteSpace(wineCommand) ? "wine" : wineCommand;
            info.ArgumentList.Add(paths.ThpracExecutable);
            info.ArgumentList.Add("--attach");
        }

        _ = Process.Start(info) ?? throw new InvalidOperationException("The operating system did not start thprac.");
        progress?.Report("thprac started for this profile.");
    }

    private static async Task<int> WaitForGameAsync(string gameExecutable, DateTimeOffset launchedAt,
        CancellationToken cancellationToken)
    {
        var processName = Path.GetFileNameWithoutExtension(gameExecutable);
        var expectedPath = Path.GetFullPath(gameExecutable);
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            int? fallback = null;
            foreach (var process in Process.GetProcessesByName(processName))
            {
                using (process)
                {
                    try
                    {
                        if (process.StartTime.ToUniversalTime() < launchedAt.UtcDateTime.AddSeconds(-2)) continue;
                        fallback ??= process.Id;
                        try
                        {
                            if (string.Equals(process.MainModule?.FileName, expectedPath, StringComparison.OrdinalIgnoreCase))
                                return process.Id;
                        }
                        catch
                        {
                            // Some game processes deny module inspection; the name and launch time still identify them.
                        }
                    }
                    catch
                    {
                        // The process may exit while it is being inspected.
                    }
                }
            }
            if (fallback is { } processId) return processId;
            await Task.Delay(100, cancellationToken);
        }
        throw new TimeoutException("The game started through THCRAP, but thprac could not find its process within 15 seconds.");
    }
}
