using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text.Json;
using GensokyoLauncher.Models;

namespace GensokyoLauncher.Services;

public sealed class ThcrapRunConfigurationService(AppPaths paths) : IThcrapRunConfigurationService
{
    public async Task<string> GenerateAsync(LaunchProfile profile, string gameId, IReadOnlyList<string> archivePaths,
        CancellationToken cancellationToken = default)
    {
        Directory.CreateDirectory(paths.ThcrapConfigDirectory);
        var file = Path.Combine(paths.ThcrapConfigDirectory, $"gensokyo-{Sanitize(profile.Id)}-{Sanitize(gameId)}.js");
        var patches = archivePaths.Select(path => new { archive = path.EndsWith('/') ? path : path + "/" }).ToArray();
        var payload = new { console = false, dat_dump = false, patches };
        var temporary = file + ".tmp";
        await File.WriteAllTextAsync(temporary, JsonSerializer.Serialize(payload, new JsonSerializerOptions { WriteIndented = true }), cancellationToken);
        File.Move(temporary, file, true);
        return file;
    }

    private static string Sanitize(string value) => string.Concat(value.Select(c => char.IsLetterOrDigit(c) || c is '-' ? c : '_'));
}

public sealed class ThcrapGameLauncher(
    AppPaths paths,
    IGameWindowManager windows,
    IThpracGameAttacher thprac,
    Th06VpatchService vpatch) : IThcrapGameLauncher
{
    public async Task LaunchAsync(string gameExecutable, string configurationPath, LaunchProfile profile, string wineCommand,
        IProgress<string>? progress = null, CancellationToken cancellationToken = default)
    {
        var launchExecutable = vpatch.PrepareLaunchExecutable(gameExecutable, profile);
        var gameProcessExecutable = vpatch.ResolveGameProcessExecutable(gameExecutable);
        var loader = Directory.EnumerateFiles(paths.ThcrapDirectory, "thcrap_loader.exe", SearchOption.AllDirectories).FirstOrDefault()
            ?? throw new FileNotFoundException("THCRAP is not installed (thcrap_loader.exe was not found).", paths.ThcrapDirectory);
        var info = new ProcessStartInfo { WorkingDirectory = Path.GetDirectoryName(loader)!, UseShellExecute = false };
        if (OperatingSystem.IsWindows()) info.FileName = loader;
        else
        {
            info.FileName = string.IsNullOrWhiteSpace(wineCommand) ? "wine" : wineCommand;
            info.ArgumentList.Add(loader);
        }
        info.ArgumentList.Add(configurationPath);
        info.ArgumentList.Add(launchExecutable);
        var launchedAt = DateTimeOffset.UtcNow;
        var process = Process.Start(info) ?? throw new InvalidOperationException("The operating system did not start THCRAP.");
        if (profile.UseThprac)
            await thprac.AttachAsync(gameProcessExecutable, launchedAt, wineCommand, progress, cancellationToken);
        if (profile.DisplayMode == LaunchDisplayMode.GameDefault) return;
        if (!OperatingSystem.IsWindows())
        {
            progress?.Report("Managed display modes are unsupported on this platform; using the game default.");
            return;
        }
        var warning = await windows.ApplyAsync(process.Id, gameProcessExecutable, launchedAt, profile, cancellationToken);
        if (warning is not null) progress?.Report(warning);
    }
}

public sealed class WindowsGameWindowManager : IGameWindowManager
{
    public async Task<string?> ApplyAsync(int processId, string executablePath, DateTimeOffset launchedAt, LaunchProfile profile,
        CancellationToken cancellationToken = default)
    {
        if (!OperatingSystem.IsWindows() || profile.DisplayMode == LaunchDisplayMode.GameDefault) return null;
        var deadline = DateTime.UtcNow.AddSeconds(15);
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var handle = FindWindow(executablePath, launchedAt);
            if (handle != IntPtr.Zero)
            {
                ShowWindow(handle, 9);
                var monitor = MonitorFromWindow(handle, 2);
                var info = new MonitorInfo { Size = Marshal.SizeOf<MonitorInfo>() };
                if (!GetMonitorInfo(monitor, ref info)) return "The game is running, but its monitor could not be determined.";
                if (profile.DisplayMode == LaunchDisplayMode.BorderlessFullscreen)
                {
                    SetWindowLongPtr(handle, -16, new IntPtr(0x10000000));
                    SetWindowPos(handle, IntPtr.Zero, info.Monitor.Left, info.Monitor.Top, info.Monitor.Width, info.Monitor.Height, 0x0040);
                }
                else
                {
                    var rect = new Rect { Right = profile.WindowWidth, Bottom = profile.WindowHeight };
                    AdjustWindowRect(ref rect, (uint)GetWindowLongPtr(handle, -16).ToInt64(), false);
                    var width = rect.Width; var height = rect.Height;
                    var x = info.Work.Left + (info.Work.Width - width) / 2;
                    var y = info.Work.Top + (info.Work.Height - height) / 2;
                    SetWindowPos(handle, IntPtr.Zero, x, y, width, height, 0x0040);
                }
                return null;
            }
            await Task.Delay(250, cancellationToken);
        }
        return "The game is running, but its window could not be found for display management.";
    }

    private static IntPtr FindWindow(string executablePath, DateTimeOffset launchedAt)
    {
        IntPtr found = IntPtr.Zero;
        EnumWindows((window, _) =>
        {
            if (!IsWindowVisible(window)) return true;
            GetWindowThreadProcessId(window, out var id);
            try
            {
                using var process = Process.GetProcessById((int)id);
                if (process.StartTime.ToUniversalTime() >= launchedAt.UtcDateTime.AddSeconds(-2)
                    && string.Equals(process.MainModule?.FileName, executablePath, StringComparison.OrdinalIgnoreCase))
                { found = window; return false; }
            }
            catch { }
            return true;
        }, IntPtr.Zero);
        return found;
    }

    private delegate bool EnumProc(IntPtr hWnd, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumProc callback, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr hWnd);
    [DllImport("user32.dll")] private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint processId);
    [DllImport("user32.dll")] private static extern bool ShowWindow(IntPtr hWnd, int command);
    [DllImport("user32.dll")] private static extern IntPtr MonitorFromWindow(IntPtr hWnd, uint flags);
    [DllImport("user32.dll", CharSet = CharSet.Auto)] private static extern bool GetMonitorInfo(IntPtr monitor, ref MonitorInfo info);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int index);
    [DllImport("user32.dll", EntryPoint = "SetWindowLongPtrW")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int index, IntPtr value);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr after, int x, int y, int width, int height, uint flags);
    [DllImport("user32.dll")] private static extern bool AdjustWindowRect(ref Rect rect, uint style, bool menu);
    [StructLayout(LayoutKind.Sequential)] private struct Rect { public int Left, Top, Right, Bottom; public int Width => Right - Left; public int Height => Bottom - Top; }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Auto)] private struct MonitorInfo { public int Size; public Rect Monitor; public Rect Work; public uint Flags; }
}
