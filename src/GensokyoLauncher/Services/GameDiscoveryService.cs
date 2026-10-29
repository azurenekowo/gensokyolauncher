using GensokyoLauncher.Models;
using System.Text.RegularExpressions;

namespace GensokyoLauncher.Services;

public sealed class GameDiscoveryService : IGameDiscoveryService
{
    public Task<IReadOnlyList<DiscoveredGame>> DiscoverAsync(string root, CancellationToken cancellationToken = default) =>
        Task.Run<IReadOnlyList<DiscoveredGame>>(() => Discover(root, cancellationToken), cancellationToken);

    private static IReadOnlyList<DiscoveredGame> Discover(string root, CancellationToken cancellationToken)
    {
        if (!Directory.Exists(root))
            return [];

        var definitions = GameCatalog.All
            .Where(definition => definition.Category != GameCategory.Pc98)
            .SelectMany(definition => definition.FileNames.Select(name => (name, definition)))
            .ToDictionary(item => item.name, item => item.definition, StringComparer.OrdinalIgnoreCase);
        var matches = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
        var options = new EnumerationOptions
        {
            RecurseSubdirectories = true,
            IgnoreInaccessible = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        try
        {
            foreach (var file in Directory.EnumerateFiles(root, "*", options))
            {
                cancellationToken.ThrowIfCancellationRequested();
                GameDefinition? definition;
                if (string.Equals(Path.GetExtension(file), ".exe", StringComparison.OrdinalIgnoreCase))
                {
                    if (!definitions.TryGetValue(Path.GetFileName(file), out definition)) continue;
                }
                else
                {
                    if (!IsPc98DiskImage(file)) continue;
                    var relative = Path.GetRelativePath(root, file);
                    var relativeDirectory = Path.GetDirectoryName(relative);
                    var searchable = Path.GetFileNameWithoutExtension(file)
                        + (string.IsNullOrWhiteSpace(relativeDirectory) ? "" : " " + relativeDirectory);
                    var diskName = Path.GetFileNameWithoutExtension(file);
                    definition = GameCatalog.All.FirstOrDefault(game => game.Category == GameCategory.Pc98
                        && (string.Equals(diskName, game.Id, StringComparison.OrdinalIgnoreCase)
                            || Regex.IsMatch(diskName, $"^{Regex.Escape(game.Id)}[ej]$", RegexOptions.IgnoreCase)
                            || game.FileNames.Any(token => ContainsToken(searchable, token))));
                    if (definition is null) continue;
                }

                if (!matches.TryGetValue(definition.Id, out var paths))
                    matches[definition.Id] = paths = [];
                paths.Add(Path.GetFullPath(file));
            }
        }
        catch (IOException)
        {
            // A disappearing directory should not invalidate matches already found.
        }
        catch (UnauthorizedAccessException)
        {
            // EnumerationOptions handles most inaccessible children; the root can still fail.
        }

        return GameCatalog.All
            .Where(definition => matches.ContainsKey(definition.Id))
            .Select(definition =>
            {
                var diskImages = matches[definition.Id]
                    .OrderBy(path => path, StringComparer.OrdinalIgnoreCase)
                    .ToArray();
                var preferred = diskImages
                    .OrderBy(path => path.Count(character => character is '/' or '\\'))
                    .ThenBy(path => Path.GetFileName(path), StringComparer.OrdinalIgnoreCase)
                    .First();
                return new DiscoveredGame(definition, preferred,
                    definition.Category == GameCategory.Pc98 ? diskImages : null);
            })
            .ToArray();
    }

    private static bool IsPc98DiskImage(string path) => Path.GetExtension(path).ToLowerInvariant() is
        ".hdi" or ".hdd" or ".d88" or ".d98" or ".fdi" or ".xdf" or ".hdm" or ".t98" or ".nfd";

    private static bool ContainsToken(string path, string token)
    {
        var normalized = Regex.Replace(path, @"[^\p{L}\p{N}]+", " ");
        var normalizedToken = Regex.Replace(token, @"[^\p{L}\p{N}]+", " ").Trim();
        return Regex.IsMatch(normalized, $@"(?<![\p{{L}}\p{{N}}]){Regex.Escape(normalizedToken)}(?![\p{{L}}\p{{N}}])", RegexOptions.IgnoreCase);
    }
}
