namespace GensokyoLauncher.Models;

public enum LaunchDisplayMode { GameDefault, Windowed, BorderlessFullscreen }

public sealed class LaunchProfile
{
    public const string OriginalJapaneseId = "original-ja";
    public const string EnglishId = "english";
    public string Id { get; set; } = Guid.NewGuid().ToString("N");
    public string Name { get; set; } = "New profile";
    public string? LanguagePatchId { get; set; }
    public LaunchDisplayMode DisplayMode { get; set; }
    public int WindowWidth { get; set; } = 1280;
    public int WindowHeight { get; set; } = 960;
    public bool UseThprac { get; set; }

    public string LanguageLabel => LanguagePatchId is null ? "Original Japanese" : LanguagePatchId;
    public string DisplayLabel => DisplayMode switch
    {
        LaunchDisplayMode.Windowed => $"Windowed · {WindowWidth}×{WindowHeight}",
        LaunchDisplayMode.BorderlessFullscreen => "Borderless fullscreen",
        _ => "Game default"
    };

    public void Normalize()
    {
        if (string.IsNullOrWhiteSpace(Id)) Id = Guid.NewGuid().ToString("N");
        if (string.IsNullOrWhiteSpace(Name)) Name = LanguageLabel;
        WindowWidth = Math.Clamp(WindowWidth, 320, 7680);
        WindowHeight = Math.Clamp(WindowHeight, 240, 4320);
    }

    public static LaunchProfile OriginalJapanese() => new() { Id = OriginalJapaneseId, Name = "Original Japanese" };
    public static LaunchProfile English() => new() { Id = EnglishId, Name = "English", LanguagePatchId = "lang_en" };
}

public sealed record OfficialLanguage(
    string PatchId,
    string Name,
    string? Description = null,
    string RepositoryId = "thpatch",
    bool IsInstalled = false);
