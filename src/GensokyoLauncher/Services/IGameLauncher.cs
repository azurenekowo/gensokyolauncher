namespace GensokyoLauncher.Services;

public interface IGameLauncher
{
    Task LaunchAsync(string executablePath, string wineCommand, string? argument = null,
        CancellationToken cancellationToken = default, string? workingDirectory = null);
}
