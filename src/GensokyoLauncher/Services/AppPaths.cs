namespace GensokyoLauncher.Services;

public sealed class AppPaths
{
    public AppPaths(string? dataRoot = null)
    {
        DataRoot = dataRoot ?? Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "GensokyoLauncher");
    }

    public string DataRoot { get; }
    public string SettingsFile => Path.Combine(DataRoot, "settings.json");
    public string ThcrapDirectory => Path.Combine(DataRoot, "thcrap");
    public string NekoProjectDirectory => Path.Combine(DataRoot, "neko-project-ii");
    public string ThpracDirectory => Path.Combine(DataRoot, "thprac");
    public string ThpracExecutable => Path.Combine(ThpracDirectory, "thprac.exe");
    public string NekoProjectConfigDirectory => Path.Combine(DataRoot, "neko-project-config");
    public string ThcrapConfigDirectory => Path.Combine(ThcrapDirectory, "config");
}
