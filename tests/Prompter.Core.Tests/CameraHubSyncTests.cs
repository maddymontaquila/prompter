using System.Text.Json.Nodes;
using Prompter.Core.CameraHub;
using Xunit;

namespace Prompter.Core.Tests;

public class CameraHubSyncTests : IDisposable
{
    private readonly TempDirectory _hub = new();
    private readonly TempDirectory _backups = new();
    private readonly TempDirectory _home = new();

    private CameraHubStore Store => new(_hub.Path, _backups.Path, isCameraHubRunning: () => false);
    private LocalLibrary Library => new(_home.Path);

    public void Dispose()
    {
        _hub.Dispose();
        _backups.Dispose();
        _home.Dispose();
    }

    [Fact]
    public void Pull_NewEntry_ImportsIntoLocalLibrary()
    {
        var store = Store;
        var library = Library;
        var id = Guid.NewGuid();
        store.PushOne(id, "Hub Script", ["Chapter one."]);

        var summary = CameraHubSync.Pull(store, library, PullConflictPolicy.Skip);

        Assert.True(summary.Success);
        var outcome = Assert.Single(summary.Outcomes);
        Assert.Equal("imported", outcome.Action);
        Assert.NotNull(library.Get(id));
    }

    [Fact]
    public void Pull_ExistingLocalScript_DefaultSkipPolicy_DoesNotOverwrite()
    {
        var store = Store;
        var library = Library;
        var id = Guid.NewGuid();
        store.PushOne(id, "Hub Version", ["Hub body."]);
        library.Import(id, "Local Version (authored)", ["Local body - do not lose this."], order: 0);

        var summary = CameraHubSync.Pull(store, library, PullConflictPolicy.Skip);

        var outcome = Assert.Single(summary.Outcomes);
        Assert.Equal("skipped-conflict", outcome.Action);
        var local = library.Get(id)!;
        Assert.Equal("Local Version (authored)", local.Name);
        Assert.Equal(["Local body - do not lose this."], local.Chapters);
    }

    [Fact]
    public void Pull_ExistingLocalScript_OverwritePolicy_ReplacesLocalContent()
    {
        var store = Store;
        var library = Library;
        var id = Guid.NewGuid();
        store.PushOne(id, "Hub Version", ["Hub body."]);
        library.Import(id, "Local Version", ["Local body."], order: 0);

        var summary = CameraHubSync.Pull(store, library, PullConflictPolicy.Overwrite);

        var outcome = Assert.Single(summary.Outcomes);
        Assert.Equal("overwritten", outcome.Action);
        var local = library.Get(id)!;
        Assert.Equal("Hub Version", local.Name);
        Assert.Equal(["Hub body."], local.Chapters);
    }

    [Fact]
    public void Pull_MalformedEntry_IsSkippedNotFatal_OtherEntriesStillImported()
    {
        var store = Store;
        var library = Library;
        var goodId = Guid.NewGuid();
        var badId = Guid.NewGuid();

        store.PushOne(goodId, "Good Script", ["Body."]);
        // Manually reference a second id in libraryList whose Texts file is missing.
        var root = JsonNode.Parse(File.ReadAllText(Path.Combine(_hub.Path, "AppSettings.json")))!;
        var validation = CameraHubSchema.ValidateLibraryList(root);
        var ids = validation.Ids.ToList();
        ids.Add(badId);
        CameraHubSchema.SetLibraryList((JsonObject)root, ids);
        File.WriteAllText(Path.Combine(_hub.Path, "AppSettings.json"), root.ToJsonString());

        var summary = CameraHubSync.Pull(store, library, PullConflictPolicy.Skip);

        Assert.True(summary.Success);
        Assert.Equal(2, summary.Outcomes.Count);
        Assert.Contains(summary.Outcomes, o => o.Id == goodId && o.Action == "imported");
        Assert.Contains(summary.Outcomes, o => o.Id == badId && o.Action == "skipped-malformed");
        Assert.NotNull(library.Get(goodId));
        Assert.Null(library.Get(badId));
    }

    [Fact]
    public void Pull_OnlyId_FiltersToThatEntry()
    {
        var store = Store;
        var library = Library;
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        store.PushOne(id1, "First", ["Body."]);
        store.PushOne(id2, "Second", ["Body."]);

        var summary = CameraHubSync.Pull(store, library, PullConflictPolicy.Skip, onlyId: id2);

        var outcome = Assert.Single(summary.Outcomes);
        Assert.Equal(id2, outcome.Id);
        Assert.Null(library.Get(id1));
        Assert.NotNull(library.Get(id2));
    }

    [Fact]
    public void Pull_CameraHubDirectoryMissing_ReturnsEmptyNonFatalSummary()
    {
        Directory.Delete(_hub.Path, recursive: true);
        var summary = CameraHubSync.Pull(Store, Library, PullConflictPolicy.Skip);

        Assert.True(summary.Success);
        Assert.False(summary.CameraHubFound);
        Assert.Empty(summary.Outcomes);
    }

    [Fact]
    public void Push_RoundTripsScriptRecordThroughStore()
    {
        var store = Store;
        var library = Library;
        var script = library.Create("Push Me", ["One.", "Two."]);

        var result = CameraHubSync.Push(store, script);

        Assert.True(result.Success);
        var read = store.ReadAll();
        var entry = Assert.Single(read.Entries);
        Assert.Equal(script.Id, entry.Id);
        Assert.Equal(script.Chapters, entry.Text!.Chapters);
    }
}
