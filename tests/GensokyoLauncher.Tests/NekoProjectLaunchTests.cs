using GensokyoLauncher.Models;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class NekoProjectLaunchTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-np2-{Guid.NewGuid():N}");

    [Fact]
    public void TouhouCompatibleNp21BuildIsPreferred()
    {
        var paths = new AppPaths(_root);
        var bin = Path.Combine(paths.NekoProjectDirectory, "bin");
        Directory.CreateDirectory(bin);
        File.WriteAllText(Path.Combine(bin, "np2x64w.exe"), "");
        File.WriteAllText(Path.Combine(bin, "np21x64w.exe"), "");
        var service = new NekoProjectBootstrapService(new HttpClient(), paths);

        Assert.Equal("np21x64w.exe", Path.GetFileName(service.FindEmulator()));
    }

    [Fact]
    public void HardDiskImageIsMountedThroughGeneratedConfiguration()
    {
        Directory.CreateDirectory(_root);
        var paths = new AppPaths(_root);
        var emulator = Path.Combine(_root, "np2x64w.exe");
        var sourceConfiguration = Path.ChangeExtension(emulator, ".ini");
        var image = Path.Combine(_root, "Touhou 01.hdi");
        File.WriteAllText(emulator, "");
        File.WriteAllText(image, "image");
        File.WriteAllText(sourceConfiguration, "[NekoProjectII]\r\nHDD1FILE=old.hdi\r\nHDD2FILE=other.hdi\r\nWin_Snap=true\r\nJoystick=true");
        var service = new NekoProjectBootstrapService(new HttpClient(), paths);

        var argument = service.PrepareLaunchArgument(emulator, image);

        Assert.EndsWith(".np2cfg", argument);
        var generated = File.ReadAllText(argument);
        Assert.Contains("HDD1FILE=" + image, generated);
        Assert.Contains("HDD2FILE=", generated);
        Assert.DoesNotContain("old.hdi", generated);
        Assert.Contains("Win_Snap=true", generated);
        Assert.Contains("Joystick=false", generated);
        Assert.DoesNotContain("Joystick=true", generated);
    }

    [Fact]
    public void Np21ConfigurationWritesDiskIntoNekoProject21Section()
    {
        var paths = new AppPaths(_root);
        var bin = Path.Combine(paths.NekoProjectDirectory, "bin");
        Directory.CreateDirectory(bin);
        var emulator = Path.Combine(bin, "np21x64w.exe");
        var image = Path.Combine(_root, "th04.hdi");
        File.WriteAllText(emulator, "");
        File.WriteAllText(image, "image");
        File.WriteAllText(Path.ChangeExtension(emulator, ".ini"), "[NekoProjectII]\r\nHDD1FILE=wrong.hdi\r\n[NekoProject21]\r\nHDD1FILE=\r\nHDD2FILE=");
        File.WriteAllText(Path.ChangeExtension(image, ".ini"), "[NekoProject21]\r\nclk_mult=40\r\nHDD1FILE=.\\th04.hdi\r\nHDD2FILE=");
        var service = new NekoProjectBootstrapService(new HttpClient(), paths);

        var configPath = service.PrepareLaunchArgument(emulator, image);
        var config = File.ReadAllText(configPath);

        Assert.EndsWith(".npcfg", configPath);
        Assert.Contains("[NekoProject21]\r\nclk_mult=40\r\nHDD1FILE=" + image, config);
        Assert.DoesNotContain("wrong.hdi", config);
    }

    [Fact]
    public void SparseImageConfigurationGetsKeyboardFirstInputSetting()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(_root);
        var emulator = Path.Combine(_root, "np21x64w.exe");
        var image = Path.Combine(_root, "th05.hdi");
        File.WriteAllText(emulator, "");
        File.WriteAllText(image, "image");
        File.WriteAllText(Path.ChangeExtension(image, ".ini"), "[NekoProject21]\r\nclk_mult=40");
        var service = new NekoProjectBootstrapService(new HttpClient(), paths);

        var config = File.ReadAllText(service.PrepareLaunchArgument(emulator, image));

        Assert.Contains("Joystick=false", config);
        Assert.Contains("HDD1FILE=" + image, config);
    }

    [Fact]
    public void FloppyImageRemainsAPositionalArgument()
    {
        Directory.CreateDirectory(_root);
        var image = Path.Combine(_root, "game.d88");
        File.WriteAllText(image, "image");
        var service = new NekoProjectBootstrapService(new HttpClient(), new AppPaths(_root));
        Assert.Equal(image, service.PrepareLaunchArgument(Path.Combine(_root, "np2.exe"), image));
    }

    [Fact]
    public async Task ExistingMatchingInstallReportsReadyStatus()
    {
        var paths = new AppPaths(_root);
        Directory.CreateDirectory(paths.NekoProjectDirectory);
        File.WriteAllText(Path.Combine(paths.NekoProjectDirectory, "np21x64w.exe"), "");
        var messages = new List<BootstrapProgress>();
        var service = new NekoProjectBootstrapService(new HttpClient(), paths);

        await service.EnsureInstalledAsync("0.86-rev106", new InlineProgress<BootstrapProgress>(messages.Add));

        Assert.Contains(messages, item => item.Message.Contains("is ready", StringComparison.OrdinalIgnoreCase));
    }

    private sealed class InlineProgress<T>(Action<T> report) : IProgress<T>
    {
        public void Report(T value) => report(value);
    }

    public void Dispose() { if (Directory.Exists(_root)) Directory.Delete(_root, true); }
}
