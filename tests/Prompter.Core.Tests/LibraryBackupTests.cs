using Prompter.Core;
using Prompter.Core.Backup;
using Xunit;

namespace Prompter.Core.Tests;

public class LibraryBackupTests : IDisposable
{
    private readonly TempDirectory _home = new();
    private readonly TempDirectory _restoreTarget = new();

    public void Dispose()
    {
        _home.Dispose();
        _restoreTarget.Dispose();
    }

    [Fact]
    public void Create_ThenRestore_RoundTripsAllScripts()
    {
        var library = new LocalLibrary(_home.Path);
        library.Create("First", ["Body one."]);
        library.Create("Second", ["Body two."]);

        var zipPath = LibraryBackup.Create(_home.Path);
        Assert.True(File.Exists(zipPath));

        LibraryBackup.Restore(_restoreTarget.Path, zipPath);

        var restoredLibrary = new LocalLibrary(_restoreTarget.Path);
        var restored = restoredLibrary.Load();
        Assert.Empty(restored.Errors);
        Assert.Equal(2, restored.Scripts.Count);
        Assert.Contains(restored.Scripts, s => s.Name == "First");
        Assert.Contains(restored.Scripts, s => s.Name == "Second");
    }

    [Fact]
    public void Restore_OverwritesExistingLibraryContents()
    {
        var library = new LocalLibrary(_home.Path);
        library.Create("Original", ["Body."]);
        var zipPath = LibraryBackup.Create(_home.Path);

        var targetLibrary = new LocalLibrary(_restoreTarget.Path);
        targetLibrary.Create("Should Be Replaced", ["Stale."]);

        LibraryBackup.Restore(_restoreTarget.Path, zipPath);

        var restored = targetLibrary.Load().Scripts;
        Assert.Single(restored);
        Assert.Equal("Original", restored[0].Name);
    }

    [Fact]
    public void Restore_MissingBackupFile_Throws()
    {
        Assert.Throws<FileNotFoundException>(() =>
            LibraryBackup.Restore(_restoreTarget.Path, Path.Combine(_home.Path, "does-not-exist.zip")));
    }

    [Fact]
    public void Restore_ArchiveWithoutLibraryFolder_ThrowsAndDoesNotTouchTarget()
    {
        var targetLibrary = new LocalLibrary(_restoreTarget.Path);
        targetLibrary.Create("Keep Me", ["Body."]);

        var badZipPath = Path.Combine(_home.Path, "bad.zip");
        using (var archive = System.IO.Compression.ZipFile.Open(badZipPath, System.IO.Compression.ZipArchiveMode.Create))
        {
            var entry = archive.CreateEntry("unrelated/file.txt");
            using var writer = new StreamWriter(entry.Open());
            writer.Write("not a library backup");
        }

        Assert.Throws<InvalidDataException>(() => LibraryBackup.Restore(_restoreTarget.Path, badZipPath));

        var stillThere = targetLibrary.Load().Scripts;
        Assert.Single(stillThere);
        Assert.Equal("Keep Me", stillThere[0].Name);
    }

    [Fact]
    public void Create_TwiceInSameSecond_ProducesDistinctFiles()
    {
        var library = new LocalLibrary(_home.Path);
        library.Create("Script", ["Body."]);

        var first = LibraryBackup.Create(_home.Path);
        var second = LibraryBackup.Create(_home.Path);

        Assert.NotEqual(first, second);
        Assert.True(File.Exists(first));
        Assert.True(File.Exists(second));
    }
}
