namespace GensokyoLauncher.Models;

public enum GameCategory { Mainline, Pc98, OffMainline }

public sealed record GameDefinition(string Id, string Title, GameCategory Category, params string[] FileNames);

public sealed record DiscoveredGame(
    GameDefinition Definition,
    string ExecutablePath,
    IReadOnlyList<string>? AvailableDiskImages = null);
