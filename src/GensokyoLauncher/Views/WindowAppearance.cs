using System.Runtime.InteropServices;
using Avalonia.Controls;
using Avalonia.Media;

namespace GensokyoLauncher.Views;

internal static class WindowAppearance
{
    private static readonly IBrush OpaqueBackground = new SolidColorBrush(Color.Parse("#17171D"));
    // Semitransparent tint drawn over the Windows 7 Aero glass so content stays readable.
    private static readonly IBrush AeroTintBackground = new SolidColorBrush(Color.FromArgb(0xA6, 0x17, 0x17, 0x1D));

    public static void ApplyBackdrop(Window window, int backdropMode)
    {
        window.TransparencyBackgroundFallback = OpaqueBackground;
        if (IsWindows7AeroAvailable() && backdropMode != 2)
        {
            if (ApplyAeroGlass(window)) return;
        }
        else if (OperatingSystem.IsWindows())
        {
            DisableAeroGlass(window);
        }
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
                window.TransparencyLevelHint = OperatingSystem.IsLinux()
                    ? [WindowTransparencyLevel.Blur, WindowTransparencyLevel.Transparent, WindowTransparencyLevel.None]
                    : [WindowTransparencyLevel.Mica, WindowTransparencyLevel.AcrylicBlur, WindowTransparencyLevel.None];
                window.Background = Brushes.Transparent;
                break;
        }
    }

    /// <summary>
    /// Applies the Windows 7 Aero glass effect (DWM blur behind) to the whole window.
    /// Avalonia itself does not support transparency on Windows 7, so this is done directly
    /// through DWM. The window then renders a semitransparent tint over the blurred desktop.
    /// </summary>
    private static bool ApplyAeroGlass(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero) return false;
        SetAeroGlass(handle, true);
        // Avalonia resolves an unsupported Mica/Acrylic hint to None, which paints
        // TransparencyBackgroundFallback beneath the content; use a semitransparent tint
        // there so the DWM blur shows through instead of an opaque panel.
        window.TransparencyLevelHint = [WindowTransparencyLevel.None];
        window.TransparencyBackgroundFallback = AeroTintBackground;
        window.Background = Brushes.Transparent;
        return true;
    }

    /// <summary>
    /// True only on Windows 7 (Windows Server 2008 R2) while DWM composition (Aero) is enabled.
    /// </summary>
    internal static bool IsWindows7AeroAvailable() =>
        OperatingSystem.IsWindows()
        && Environment.OSVersion.Version is { Major: 6, Minor: 1 }
        && DwmCompositionIsEnabled();

    private static bool DwmCompositionIsEnabled()
    {
        try { DwmIsCompositionEnabled(out var enabled); return enabled; }
        catch { return false; }
    }

    private static void SetAeroGlass(IntPtr handle, bool enable)
    {
        try
        {
            // DWM_BB_ENABLE on its own applies the blur region to the entire window.
            var blurBehind = new DwmBlurBehind
            {
                DwmFlags = DwmBbEnable,
                Enable = enable,
                BlurRegion = IntPtr.Zero
            };
            DwmEnableBlurBehindWindow(handle, ref blurBehind);
        }
        catch
        {
            // Failure to toggle glass falls back to the regular opaque appearance.
        }
    }

    private static void DisableAeroGlass(Window window)
    {
        var handle = window.TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle != IntPtr.Zero) SetAeroGlass(handle, false);
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct DwmBlurBehind
    {
        public int DwmFlags;
        [MarshalAs(UnmanagedType.Bool)]
        public bool Enable;
        public IntPtr BlurRegion;
    }

    [DllImport("dwmapi.dll")]
    private static extern void DwmIsCompositionEnabled([MarshalAs(UnmanagedType.Bool)] out bool enabled);

    [DllImport("dwmapi.dll")]
    private static extern void DwmEnableBlurBehindWindow(IntPtr handle, ref DwmBlurBehind blurBehind);

    private const int DwmBbEnable = 0x1;
}
