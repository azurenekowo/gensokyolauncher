namespace GensokyoLauncher.Models;

public sealed class AppSettings
{
    public string? GamesRoot { get; set; }
    public string WineCommand { get; set; } = "wine";
    public string? ThcrapVersion { get; set; }
    public int GameViewMode { get; set; }
    public int GameGroupingMode { get; set; }
    public int AppearanceBackdropMode { get; set; }
    public int TabAnimationMode { get; set; }
    public string? NekoProjectVersion { get; set; }
    public string? ThpracVersion { get; set; }
    public List<LaunchProfile> LaunchProfiles { get; set; } = [];
    public string? GlobalDefaultProfileId { get; set; }
    public Dictionary<string, string> GameProfileAssignments { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> GameExecutableOverrides { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    public DateTimeOffset? ThcrapLastUpdateCheck { get; set; }

    public void NormalizeProfiles()
    {
        LaunchProfiles ??= [];
        GameProfileAssignments ??= new(StringComparer.OrdinalIgnoreCase);
        GameExecutableOverrides ??= new(StringComparer.OrdinalIgnoreCase);
        if (LaunchProfiles.Count == 0)
        {
            LaunchProfiles.Add(LaunchProfile.OriginalJapanese());
            LaunchProfiles.Add(LaunchProfile.English());
        }
        foreach (var profile in LaunchProfiles)
            profile.Normalize();
        if (!LaunchProfiles.Any(p => p.Id == GlobalDefaultProfileId))
            GlobalDefaultProfileId = LaunchProfiles.FirstOrDefault(p => p.Id == LaunchProfile.OriginalJapaneseId)?.Id
                ?? LaunchProfiles[0].Id;
        var valid = LaunchProfiles.Select(p => p.Id).ToHashSet(StringComparer.Ordinal);
        foreach (var key in GameProfileAssignments.Where(x => !valid.Contains(x.Value)).Select(x => x.Key).ToArray())
            GameProfileAssignments.Remove(key);
    }

    public LaunchProfile ResolveProfile(string? gameId = null)
    {
        NormalizeProfiles();
        if (gameId is not null && GameProfileAssignments.TryGetValue(gameId, out var assigned))
        {
            var preferred = LaunchProfiles.FirstOrDefault(p => p.Id == assigned);
            if (preferred is not null) return preferred;
        }
        return LaunchProfiles.FirstOrDefault(p => p.Id == GlobalDefaultProfileId)
            ?? LaunchProfiles.FirstOrDefault(p => p.Id == LaunchProfile.OriginalJapaneseId)
            ?? LaunchProfiles[0];
    }
}
