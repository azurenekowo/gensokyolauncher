using GensokyoLauncher.Models;
using Avalonia;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using System.Diagnostics;

namespace GensokyoLauncher.ViewModels;

public sealed class GameItemViewModel : ObservableObject
{
    private readonly DiscoveredGame _game;
    private static readonly Color[][] Palettes =
    [
        [Color.Parse("#743D8F"), Color.Parse("#25142E")],
        [Color.Parse("#397C91"), Color.Parse("#122A34")],
        [Color.Parse("#A44E55"), Color.Parse("#35171B")],
        [Color.Parse("#4E668F"), Color.Parse("#161E35")],
        [Color.Parse("#8D7240"), Color.Parse("#302514")],
        [Color.Parse("#477B67"), Color.Parse("#142B24")]
    ];
    private string? _error;
    private string? _status;
    private bool _showGroupHeading;
    public GameItemViewModel(DiscoveredGame game, Func<Task> launch, Func<Task> showProperties)
    {
        _game = game;
        CardBrush = CreateCardBrush(game.Definition.Id);
        CoverImage = LoadCover(game.Definition.Id);
        LaunchCommand = new AsyncCommand(launch);
        OpenDirectoryCommand = new RelayCommand(OpenDirectory);
        PropertiesCommand = new AsyncCommand(showProperties);
    }

    public string Title => _game.Definition.Title;
    public string ShortTitle => Title.Contains(" — ", StringComparison.Ordinal)
        ? Title[(Title.IndexOf(" — ", StringComparison.Ordinal) + 3)..]
        : Title;
    public string GameNumber => FormatGameNumber(_game.Definition.Id);
    public string CatalogId => _game.Definition.Id.ToUpperInvariant();
    public IBrush CardBrush { get; }
    public Bitmap? CoverImage { get; }
    public bool HasCover => CoverImage is not null;
    public string ExecutablePath => _game.ExecutablePath;
    public string ExecutableName => Path.GetFileName(_game.ExecutablePath);
    public GameCategory Category => _game.Definition.Category;
    public string GroupName => Category switch
    {
        GameCategory.Pc98 => "PC-98 era",
        GameCategory.OffMainline => "Off-mainline fighting games",
        _ => "Mainline"
    };
    public bool ShowGroupHeading { get => _showGroupHeading; set => SetProperty(ref _showGroupHeading, value); }
    public string? Error { get => _error; set { if (SetProperty(ref _error, value)) OnPropertyChanged(nameof(HasError)); } }
    public bool HasError => !string.IsNullOrWhiteSpace(Error);
    public string? Status { get => _status; set { if (SetProperty(ref _status, value)) OnPropertyChanged(nameof(HasStatus)); } }
    public bool HasStatus => !string.IsNullOrWhiteSpace(Status);
    public AsyncCommand LaunchCommand { get; }
    public RelayCommand OpenDirectoryCommand { get; }
    public AsyncCommand PropertiesCommand { get; }

    private void OpenDirectory()
    {
        try
        {
            var directory = Path.GetDirectoryName(ExecutablePath)
                ?? throw new InvalidOperationException("The game directory could not be determined.");
            if (!Directory.Exists(directory))
                throw new DirectoryNotFoundException("The game directory no longer exists.");
            ProcessStartInfo info;
            if (OperatingSystem.IsWindows())
            {
                info = new ProcessStartInfo(directory) { UseShellExecute = true };
                _ = Process.Start(info);
                return;
            }
            else
            {
                info = new ProcessStartInfo(OperatingSystem.IsMacOS() ? "open" : "xdg-open") { UseShellExecute = false };
                info.ArgumentList.Add(directory);
            }
            _ = Process.Start(info) ?? throw new InvalidOperationException("The operating system did not open the game directory.");
        }
        catch (Exception exception) { Error = exception.Message; }
    }

    private static IBrush CreateCardBrush(string id)
    {
        var palette = Palettes[Math.Abs(StringComparer.Ordinal.GetHashCode(id)) % Palettes.Length];
        return new LinearGradientBrush
        {
            StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
            EndPoint = new RelativePoint(1, 1, RelativeUnit.Relative),
            GradientStops =
            {
                new GradientStop(palette[0], 0),
                new GradientStop(palette[1], 1)
            }
        };
    }

    private static string FormatGameNumber(string id)
    {
        var digits = id[2..];
        if (!int.TryParse(digits, out var value))
            return digits;
        return digits.Length == 3 ? $"{value / 10}.{value % 10}" : value.ToString();
    }

    private static Bitmap? LoadCover(string id)
    {
        if (id.Length != 4)
            return null;

        var extension = id switch
        {
            "th01" or "th02" or "th03" or "th04" or "th05" => "webp",
            "th19" => "png",
            _ => "jpg"
        };
        var assetName = $"Th{id[2..]}cover.{extension}";
        var uri = new Uri($"avares://GensokyoLauncher/Assets/{assetName}");
        return AssetLoader.Exists(uri) ? new Bitmap(AssetLoader.Open(uri)) : null;
    }
}
