using System.Text.Json;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public sealed class JsonSettingsService(AppPaths paths) : ISettingsService
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true };

    public async Task<AppSettings> LoadAsync(CancellationToken cancellationToken = default)
    {
        if (!File.Exists(paths.SettingsFile))
            return new AppSettings();

        try
        {
            await using var stream = File.OpenRead(paths.SettingsFile);
            return await JsonSerializer.DeserializeAsync<AppSettings>(stream, Options, cancellationToken)
                   ?? new AppSettings();
        }
        catch (JsonException)
        {
            return new AppSettings();
        }
        catch (IOException)
        {
            return new AppSettings();
        }
    }

    public async Task SaveAsync(AppSettings settings, CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.DataRoot);
        var temporaryFile = paths.SettingsFile + ".tmp";
        await using (var stream = File.Create(temporaryFile))
            await JsonSerializer.SerializeAsync(stream, settings, Options, cancellationToken);

        File.Move(temporaryFile, paths.SettingsFile, true);
    }
}
