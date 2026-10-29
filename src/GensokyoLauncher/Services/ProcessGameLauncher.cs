using System.Diagnostics;

namespace GensokyoLauncher.Services;

public sealed class ProcessGameLauncher : IGameLauncher
{
    public Task LaunchAsync(string executablePath, string wineCommand, string? argument = null,
        CancellationToken cancellationToken = default, string? workingDirectory = null)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!File.Exists(executablePath))
            throw new FileNotFoundException("The game executable no longer exists.", executablePath);

        ProcessStartInfo startInfo;
        if (OperatingSystem.IsWindows())
        {
            startInfo = new ProcessStartInfo(executablePath)
            {
                UseShellExecute = true,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
            };
            if (!string.IsNullOrWhiteSpace(argument)) startInfo.ArgumentList.Add(argument);
        }
        else
        {
            if (string.IsNullOrWhiteSpace(wineCommand))
                throw new InvalidOperationException("Configure a Wine command in Settings before launching.");
            startInfo = new ProcessStartInfo
            {
                FileName = wineCommand,
                UseShellExecute = false,
                WorkingDirectory = workingDirectory ?? Path.GetDirectoryName(executablePath) ?? Environment.CurrentDirectory
            };
            startInfo.ArgumentList.Add(executablePath);
            if (!string.IsNullOrWhiteSpace(argument)) startInfo.ArgumentList.Add(argument);
        }

        _ = Process.Start(startInfo) ?? throw new InvalidOperationException("The operating system did not start the game.");
        return Task.CompletedTask;
    }
}
