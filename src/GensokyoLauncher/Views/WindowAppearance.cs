using Avalonia.Controls;
using Avalonia.Media;

namespace GensokyoLauncher.Views;

internal static class WindowAppearance
{
    private static readonly IBrush OpaqueBackground = new SolidColorBrush(Color.Parse("#17171D"));

    public static void ApplyBackdrop(Window window, int backdropMode)
    {
        window.TransparencyBackgroundFallback = OpaqueBackground;
        switch (backdropMode)
        {
            case 1:
                window.TransparencyLevelHint =
                    [WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.Blur, WindowTransparencyLevel.None];
                window.Background = Brushes.Transparent;
                break;
            case 2:
                window.TransparencyLevelHint = [WindowTransparencyLevel.None];
                window.Background = OpaqueBackground;
                break;
            default:
                window.TransparencyLevelHint =
                    [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];
                window.Background = Brushes.Transparent;
                break;
        }
    }
}
