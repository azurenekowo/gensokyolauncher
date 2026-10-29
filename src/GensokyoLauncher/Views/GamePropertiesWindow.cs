using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Platform.Storage;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Views;

public sealed class GamePropertiesWindow : Window
{
    public GamePropertiesWindow(DiscoveredGame game, int backdropMode)
    {
        Title = "Game properties";
        Width = 620;
        Height = 360;
        MinWidth = 480;
        MinHeight = 300;
        WindowStartupLocation = WindowStartupLocation.CenterOwner;
        WindowAppearance.ApplyBackdrop(this, backdropMode);

        var close = new Button
        {
            Content = "Close",
            MinWidth = 88,
            IsDefault = true,
            IsCancel = true,
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        close.Click += (_, _) => Close(null);
        var changeExecutable = new Button
        {
            Content = "Change executable…",
            HorizontalContentAlignment = HorizontalAlignment.Center
        };
        changeExecutable.Click += async (_, _) =>
        {
            var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
            {
                Title = "Choose the executable for " + game.Definition.Title,
                AllowMultiple = false,
                SuggestedStartLocation = await StorageProvider.TryGetFolderFromPathAsync(
                    Path.GetDirectoryName(game.ExecutablePath) ?? game.ExecutablePath),
                FileTypeFilter =
                [
                    new FilePickerFileType("Windows executable") { Patterns = ["*.exe"] }
                ]
            });
            if (files.Count > 0 && files[0].TryGetLocalPath() is { } path) Close(Path.GetFullPath(path));
        };
        var buttons = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Spacing = 8,
            Children = { changeExecutable, close }
        };

        Content = new Grid
        {
            Margin = new Thickness(24),
            RowDefinitions = new RowDefinitions("*,Auto"),
            Children =
            {
                new ScrollViewer
                {
                    Content = new StackPanel
                    {
                        Spacing = 14,
                        Children =
                        {
                            new TextBlock { Text = game.Definition.Title, FontSize = 21, FontWeight = Avalonia.Media.FontWeight.SemiBold, TextWrapping = Avalonia.Media.TextWrapping.Wrap },
                            Property("Catalog ID", game.Definition.Id),
                            Property("Category", CategoryName(game.Definition.Category)),
                            Property("Executable", Path.GetFileName(game.ExecutablePath)),
                            Property("Directory", Path.GetDirectoryName(game.ExecutablePath) ?? string.Empty),
                            Property("Full path", game.ExecutablePath)
                        }
                    }
                },
                AtRow(buttons, 1)
            }
        };
    }

    private static Control Property(string label, string value) => new StackPanel
    {
        Spacing = 3,
        Children =
        {
            new TextBlock { Text = label, Opacity = .55, FontSize = 12 },
            new SelectableTextBlock { Text = value, TextWrapping = Avalonia.Media.TextWrapping.Wrap }
        }
    };

    private static string CategoryName(GameCategory category) => category switch
    {
        GameCategory.Pc98 => "PC-98 era",
        GameCategory.OffMainline => "Off-mainline fighting game",
        _ => "Mainline"
    };

    private static T AtRow<T>(T control, int row) where T : Control
    {
        Grid.SetRow(control, row);
        return control;
    }
}
