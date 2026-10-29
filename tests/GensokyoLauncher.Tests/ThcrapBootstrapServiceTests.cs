using System.IO.Compression;
using GensokyoLauncher.Services;

namespace GensokyoLauncher.Tests;

public sealed class ThcrapBootstrapServiceTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), $"gensokyo-thcrap-{Guid.NewGuid():N}");

    public ThcrapBootstrapServiceTests() => Directory.CreateDirectory(_root);

    [Fact]
    public void ExtractArchiveSafeExtractsNormalFiles()
    {
        var archivePath = Path.Combine(_root, "normal.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("bin/thcrap.exe");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("test");
        }

        var destination = Path.Combine(_root, "output");
        Directory.CreateDirectory(destination);
        ThcrapBootstrapService.ExtractArchiveSafe(archivePath, destination);

        Assert.True(File.Exists(Path.Combine(destination, "bin", "thcrap.exe")));
    }

    [Fact]
    public void ExtractArchiveSafeRejectsParentTraversal()
    {
        var archivePath = Path.Combine(_root, "unsafe.zip");
        using (var archive = ZipFile.Open(archivePath, ZipArchiveMode.Create))
            archive.CreateEntry("../escaped.exe");
        var destination = Path.Combine(_root, "output");
        Directory.CreateDirectory(destination);

        Assert.Throws<InvalidDataException>(() => ThcrapBootstrapService.ExtractArchiveSafe(archivePath, destination));
    }

    public void Dispose() => Directory.Delete(_root, true);
}
