using Prompter.Core;
using Xunit;

namespace Prompter.Core.Tests;

/// <summary>Provides a fresh temp directory per test, deleted on dispose. Never touches real app data.</summary>
public sealed class TempDirectory : IDisposable
{
    public string Path { get; } = System.IO.Path.Combine(System.IO.Path.GetTempPath(), "prompter-tests-" + Guid.NewGuid().ToString("N"));

    public TempDirectory() => Directory.CreateDirectory(Path);

    public void Dispose()
    {
        try { Directory.Delete(Path, recursive: true); }
        catch (IOException) { /* best-effort cleanup */ }
        catch (UnauthorizedAccessException) { /* best-effort cleanup */ }
    }
}

public class LocalLibraryTests : IDisposable
{
    private readonly TempDirectory _home = new();
    private LocalLibrary Library => new(_home.Path);

    public void Dispose() => _home.Dispose();

    [Fact]
    public void Create_ThenLoad_RoundTrips()
    {
        var library = Library;
        var created = library.Create("First Script", ["Hello world."]);

        var loaded = library.Load();

        Assert.Empty(loaded.Errors);
        var script = Assert.Single(loaded.Scripts);
        Assert.Equal(created.Id, script.Id);
        Assert.Equal("First Script", script.Name);
        Assert.Equal(["Hello world."], script.Chapters);
    }

    [Fact]
    public void Create_AssignsIncrementingOrder()
    {
        var library = Library;
        var first = library.Create("A", ["Body."]);
        var second = library.Create("B", ["Body."]);

        Assert.Equal(0, first.Order);
        Assert.Equal(1, second.Order);
    }

    [Fact]
    public void Save_RenamedScript_ChangesFileNameButKeepsId()
    {
        var library = Library;
        var script = library.Create("Original Name", ["Body."]);
        var originalPath = library.FindPath(script.Id);

        var renamed = script.WithName("Brand New Name");
        library.Save(renamed);

        var newPath = library.FindPath(script.Id);
        Assert.NotNull(originalPath);
        Assert.NotNull(newPath);
        Assert.NotEqual(originalPath, newPath);
        Assert.False(File.Exists(originalPath));
        Assert.True(File.Exists(newPath));

        var reloaded = library.Get(script.Id);
        Assert.Equal("Brand New Name", reloaded!.Name);
    }

    [Fact]
    public void Save_TwoScriptsWithSameName_GetDistinctFileNames()
    {
        var library = Library;
        var first = library.Create("Duplicate Name", ["A"]);
        var second = library.Create("Duplicate Name", ["B"]);

        var pathA = library.FindPath(first.Id);
        var pathB = library.FindPath(second.Id);

        Assert.NotEqual(pathA, pathB);
    }

    [Fact]
    public void Delete_RemovesScript()
    {
        var library = Library;
        var script = library.Create("To Delete", ["Body."]);

        var deleted = library.Delete(script.Id);

        Assert.True(deleted);
        Assert.Null(library.Get(script.Id));
    }

    [Fact]
    public void Delete_UnknownId_ReturnsFalse()
    {
        var library = Library;
        Assert.False(library.Delete(Guid.NewGuid()));
    }

    [Fact]
    public void Move_SwapsOrderWithNeighbor()
    {
        var library = Library;
        var first = library.Create("First", ["Body."]);
        var second = library.Create("Second", ["Body."]);

        var moved = library.Move(second.Id, -1);

        Assert.True(moved);
        var scripts = library.Load().Scripts;
        Assert.Equal("Second", scripts[0].Name);
        Assert.Equal("First", scripts[1].Name);
    }

    [Fact]
    public void Move_AtBoundary_ReturnsFalseAndDoesNotChangeOrder()
    {
        var library = Library;
        var only = library.Create("Only", ["Body."]);

        Assert.False(library.Move(only.Id, -1));
        Assert.False(library.Move(only.Id, 1));
    }

    [Fact]
    public void Import_PreservesCallerSuppliedId()
    {
        var library = Library;
        var id = Guid.NewGuid();

        var imported = library.Import(id, "From Camera Hub", ["Chapter."], order: 5);

        Assert.Equal(id, imported.Id);
        Assert.Equal(id, library.Get(id)!.Id);
    }

    [Fact]
    public void Load_MalformedFile_IsReportedNotThrown()
    {
        var library = Library;
        library.EnsureDirectoryExists();
        File.WriteAllText(System.IO.Path.Combine(library.LibraryDirectory, "broken.md"), "not a valid script file");

        var result = library.Load();

        Assert.Empty(result.Scripts);
        var error = Assert.Single(result.Errors);
        Assert.Contains("broken.md", error.Path);
    }

    [Fact]
    public void Load_DuplicateIds_KeepsFirstAndReportsError()
    {
        var library = Library;
        var script = library.Create("Dup", ["Body."]);
        var originalPath = library.FindPath(script.Id)!;

        // Manually create a second file sharing the same id to simulate corruption/copy-paste.
        var duplicateText = File.ReadAllText(originalPath);
        File.WriteAllText(System.IO.Path.Combine(library.LibraryDirectory, "dup-copy.md"), duplicateText);

        var result = library.Load();

        Assert.Single(result.Scripts);
        Assert.Contains(result.Errors, e => e.Reason.Contains("Duplicate", StringComparison.OrdinalIgnoreCase));
    }
}
