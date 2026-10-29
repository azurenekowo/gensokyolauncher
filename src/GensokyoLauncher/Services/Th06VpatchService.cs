using GensokyoLauncher.Models;
using System.Buffers.Binary;

namespace GensokyoLauncher.Services;

public sealed class Th06VpatchService
{
    private static readonly Lock ConfigurationLock = new();

    public string PrepareLaunchExecutable(string gameExecutable, LaunchProfile profile)
    {
        if (!IsTouhouSixExecutable(gameExecutable)) return gameExecutable;
        var directory = Path.GetDirectoryName(gameExecutable) ?? Environment.CurrentDirectory;
        var originalExecutable = Path.Combine(directory, "東方紅魔郷.exe");
        var vpatchExecutable = Path.Combine(directory, "vpatch.exe");
        if (!File.Exists(originalExecutable) || !File.Exists(vpatchExecutable)) return gameExecutable;

        lock (ConfigurationLock)
        {
            var configurationPath = Path.Combine(directory, "vpatch.ini");
            var text = File.Exists(configurationPath) ? File.ReadAllText(configurationPath) : string.Empty;
            text = SetValue(text, "Option", "GameFPS", "60");
            if (profile.DisplayMode != LaunchDisplayMode.GameDefault)
            {
                text = SetValue(text, "Window", "AskWindowMode", "0");
                text = SetValue(text, "Window", "enabled", "1");
                text = SetValue(text, "Window", "Width", profile.WindowWidth.ToString(System.Globalization.CultureInfo.InvariantCulture));
                text = SetValue(text, "Window", "Height", profile.WindowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture));
                EnsureWindowedGameConfiguration(directory);
            }
            var temporary = configurationPath + ".gensokyo.tmp";
            File.WriteAllText(temporary, text);
            File.Move(temporary, configurationPath, true);
        }
        return vpatchExecutable;
    }

    public string ResolveGameProcessExecutable(string selectedExecutable)
    {
        if (!IsStandaloneThpatchWrapper(selectedExecutable)) return selectedExecutable;
        var directory = Path.GetDirectoryName(selectedExecutable) ?? Environment.CurrentDirectory;
        var japanese = Path.Combine(directory, "東方紅魔郷.exe");
        if (File.Exists(japanese)) return japanese;
        var renamed = Path.Combine(directory, "th06.exe");
        return File.Exists(renamed) ? renamed : selectedExecutable;
    }

    public static bool IsStandaloneThpatchWrapper(string path) => string.Equals(
        Path.GetFileName(path), "th06 (thpatch-en).exe", StringComparison.OrdinalIgnoreCase);

    private static void EnsureWindowedGameConfiguration(string directory)
    {
        const int configurationSize = 0x38;
        const int versionOffset = 0x14;
        const int windowedOffset = 0x1e;
        var path = Path.Combine(directory, "東方紅魔郷.cfg");
        var data = File.Exists(path) ? File.ReadAllBytes(path) : [];
        if (data.Length != configurationSize
            || BinaryPrimitives.ReadInt32LittleEndian(data.AsSpan(versionOffset, sizeof(int))) != 0x0102)
        {
            data = CreateDefaultConfiguration(directory);
        }
        data[windowedOffset] = 1;
        var temporary = path + ".gensokyo.tmp";
        File.WriteAllBytes(temporary, data);
        File.Move(temporary, path, true);
    }

    private static byte[] CreateDefaultConfiguration(string directory)
    {
        var data = new byte[0x38];
        short[] controllerMapping = [0, 1, 2, 4, -1, -1, -1, -1, 3];
        for (var index = 0; index < controllerMapping.Length; index++)
            BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(index * sizeof(short), sizeof(short)), controllerMapping[index]);
        BinaryPrimitives.WriteInt32LittleEndian(data.AsSpan(0x14, sizeof(int)), 0x0102);
        data[0x18] = 2; // Lives
        data[0x19] = 3; // Bombs
        data[0x1a] = 0xff; // 32-bit color
        data[0x1b] = File.Exists(Path.Combine(directory, "bgm", "th06_01.wav")) ? (byte)1 : (byte)2;
        data[0x1c] = 1; // Sound effects
        data[0x1d] = 1; // Normal difficulty
        data[0x1e] = 1; // Windowed
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x20, sizeof(short)), 600);
        BinaryPrimitives.WriteInt16LittleEndian(data.AsSpan(0x22, sizeof(short)), 600);
        BinaryPrimitives.WriteUInt32LittleEndian(data.AsSpan(0x34, sizeof(uint)), 1); // Software texture blending
        return data;
    }

    private static bool IsTouhouSixExecutable(string path) => Path.GetFileName(path) is { } name
        && (string.Equals(name, "th06.exe", StringComparison.OrdinalIgnoreCase)
            || string.Equals(name, "東方紅魔郷.exe", StringComparison.OrdinalIgnoreCase)
            || IsStandaloneThpatchWrapper(path));

    private static string SetValue(string text, string section, string key, string value)
    {
        var newline = text.Contains("\r\n", StringComparison.Ordinal) ? "\r\n" : "\n";
        var lines = text.Replace("\r\n", "\n", StringComparison.Ordinal).Split('\n').ToList();
        while (lines.Count > 0 && lines[^1].Length == 0) lines.RemoveAt(lines.Count - 1);
        var sectionHeader = lines.FindIndex(line =>
            string.Equals(line.Trim(), $"[{section}]", StringComparison.OrdinalIgnoreCase));
        if (sectionHeader < 0)
        {
            if (lines.Count > 0) lines.Add(string.Empty);
            lines.Add($"[{section}]");
            sectionHeader = lines.Count - 1;
        }
        var sectionEnd = lines.FindIndex(sectionHeader + 1,
            line => line.TrimStart().StartsWith("[", StringComparison.Ordinal));
        if (sectionEnd < 0) sectionEnd = lines.Count;
        var prefix = key + "=";
        var spacedPrefix = key + " ";
        var index = lines.FindIndex(sectionHeader + 1, sectionEnd - sectionHeader - 1, line =>
        {
            var trimmed = line.TrimStart();
            return trimmed.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)
                   || trimmed.StartsWith(spacedPrefix, StringComparison.OrdinalIgnoreCase)
                      && trimmed[key.Length..].TrimStart().StartsWith("=", StringComparison.Ordinal);
        });
        var replacement = $"{key} = {value}";
        if (index >= 0) lines[index] = replacement;
        else lines.Insert(sectionEnd, replacement);
        return string.Join(newline, lines) + newline;
    }
}
