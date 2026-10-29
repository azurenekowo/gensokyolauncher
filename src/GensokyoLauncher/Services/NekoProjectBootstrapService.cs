using System.IO.Compression;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public interface INekoProjectBootstrapService
{
    Task<BootstrapResult> EnsureInstalledAsync(string? installedVersion, IProgress<BootstrapProgress>? progress = null, CancellationToken cancellationToken = default);
    string? FindEmulator();
    string PrepareLaunchArgument(string emulatorPath, string diskImagePath);
}

public sealed class NekoProjectBootstrapService(HttpClient client, AppPaths paths) : INekoProjectBootstrapService
{
    private const string Version = "0.86-rev106";
    private static readonly Uri DownloadUri = new("https://drive.usercontent.google.com/download?id=1DZ_xrPfn0kqHXhjuuu2C2l3GCoZ5pYzp&export=download&confirm=t");

    public string? FindEmulator()
    {
        if (!Directory.Exists(paths.NekoProjectDirectory)) return null;
        var preferredNames = Environment.Is64BitOperatingSystem
            ? new[] { "np21x64w.exe", "np21w.exe", "np2x64w.exe", "np2w.exe" }
            : new[] { "np21w.exe", "np2w.exe" };
        foreach (var name in preferredNames)
        {
            var match = Directory.EnumerateFiles(paths.NekoProjectDirectory, name, SearchOption.AllDirectories)
                .OrderBy(path => path.Contains($"{Path.DirectorySeparatorChar}vs2010{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .ThenBy(path => path.Contains($"{Path.DirectorySeparatorChar}haxm{Path.DirectorySeparatorChar}", StringComparison.OrdinalIgnoreCase))
                .FirstOrDefault();
            if (match is not null) return match;
        }
        return null;
    }

    public string PrepareLaunchArgument(string emulatorPath, string diskImagePath)
    {
        if (!File.Exists(diskImagePath))
            throw new FileNotFoundException("The PC-98 disk image no longer exists.", diskImagePath);

        var extension = Path.GetExtension(diskImagePath).ToLowerInvariant();
        if (extension is not (".hdi" or ".hdd" or ".t98"))
            return diskImagePath;

        var isNekoProject21 = Path.GetFileName(emulatorPath).StartsWith("np21", StringComparison.OrdinalIgnoreCase);
        var configurationSection = isNekoProject21 ? "NekoProject21" : "NekoProjectII";
        var imageConfiguration = Path.ChangeExtension(diskImagePath, ".ini");
        var emulatorConfiguration = Path.ChangeExtension(emulatorPath, ".ini");
        var baseConfiguration = File.Exists(imageConfiguration) ? imageConfiguration : emulatorConfiguration;
        var text = File.Exists(baseConfiguration)
            ? File.ReadAllText(baseConfiguration)
            : $"[{configurationSection}]" + Environment.NewLine;
        var escapedPath = Path.GetFullPath(diskImagePath);
        var lines = text.Replace("\r\n", "\n").Split('\n').ToList();
        var sectionHeader = lines.FindIndex(line => string.Equals(line.Trim(), $"[{configurationSection}]", StringComparison.OrdinalIgnoreCase));
        if (sectionHeader < 0)
        {
            lines.Add($"[{configurationSection}]");
            sectionHeader = lines.Count - 1;
        }
        var sectionEnd = lines.FindIndex(sectionHeader + 1, line => line.TrimStart().StartsWith("[", StringComparison.Ordinal));
        if (sectionEnd < 0) sectionEnd = lines.Count;
        SetSectionValue(lines, sectionHeader, ref sectionEnd, "HDD1FILE", escapedPath);
        // Prevent a previously persisted image from taking precedence in the second SASI slot.
        SetSectionValue(lines, sectionHeader, ref sectionEnd, "HDD2FILE", "");
        // PC-98 Touhou switches entirely to the FM-board joystick when one is exposed.
        // Disk-image INIs commonly enable it, making every keyboard key appear dead when
        // a host controller is connected. Launcher profiles are keyboard-first, so do not
        // expose the host joystick through NP21/NP2.
        SetSectionValue(lines, sectionHeader, ref sectionEnd, "Joystick", "false");

        Directory.CreateDirectory(paths.NekoProjectConfigDirectory);
        var safeName = string.Concat(Path.GetFileNameWithoutExtension(diskImagePath)
            .Select(character => char.IsLetterOrDigit(character) || character is '-' or '_' ? character : '_'));
        var pathHash = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(Path.GetFullPath(diskImagePath))))[..8];
        var configuration = Path.Combine(paths.NekoProjectConfigDirectory,
            $"{safeName}-{pathHash}" + (isNekoProject21 ? ".npcfg" : ".np2cfg"));
        var temporary = configuration + ".tmp";
        File.WriteAllText(temporary, string.Join(Environment.NewLine, lines));
        File.Move(temporary, configuration, true);
        return configuration;
    }

    private static void SetSectionValue(List<string> lines, int sectionHeader, ref int sectionEnd, string key, string value)
    {
        var prefix = key + "=";
        var index = lines.FindIndex(sectionHeader + 1, sectionEnd - sectionHeader - 1,
            line => line.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));
        if (index >= 0)
            lines[index] = prefix + value;
        else
        {
            lines.Insert(sectionEnd, prefix + value);
            sectionEnd++;
        }
    }

    public async Task<BootstrapResult> EnsureInstalledAsync(string? installedVersion, IProgress<BootstrapProgress>? progress = null, CancellationToken cancellationToken = default)
    {
        var existing = FindEmulator();
        if (existing is not null && string.Equals(installedVersion, Version, StringComparison.OrdinalIgnoreCase))
        {
            progress?.Report(new BootstrapProgress($"Neko Project II {Version} is ready.", 1));
            return new BootstrapResult(Version, paths.NekoProjectDirectory);
        }

        Directory.CreateDirectory(paths.DataRoot);
        var archivePath = Path.Combine(paths.DataRoot, $"np2-{Guid.NewGuid():N}.zip");
        var staging = Path.Combine(paths.DataRoot, $"np2-staging-{Guid.NewGuid():N}");
        try
        {
            progress?.Report(new BootstrapProgress($"Downloading Neko Project II {Version}…", 0));
            using var response = await client.GetAsync(DownloadUri, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
            response.EnsureSuccessStatusCode();
            await using (var source = await response.Content.ReadAsStreamAsync(cancellationToken))
            await using (var target = File.Create(archivePath))
                await source.CopyToAsync(target, cancellationToken);
            progress?.Report(new BootstrapProgress("Installing Neko Project II…"));
            Directory.CreateDirectory(staging);
            ThcrapBootstrapService.ExtractArchiveSafe(archivePath, staging);
            if (Directory.Exists(paths.NekoProjectDirectory)) Directory.Delete(paths.NekoProjectDirectory, true);
            Directory.Move(staging, paths.NekoProjectDirectory);
            if (FindEmulator() is null) throw new InvalidDataException("The Neko Project II archive did not contain a supported NP21/NP2 executable.");
            progress?.Report(new BootstrapProgress($"Neko Project II {Version} is ready.", 1));
            return new BootstrapResult(Version, paths.NekoProjectDirectory);
        }
        finally
        {
            if (File.Exists(archivePath)) File.Delete(archivePath);
            if (Directory.Exists(staging)) Directory.Delete(staging, true);
        }
    }
}
