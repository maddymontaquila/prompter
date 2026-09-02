using System.Text.Json.Nodes;
using Prompter.Core.CameraHub;
using Xunit;

namespace Prompter.Core.Tests;

/// <summary>
/// Exercises <see cref="CameraHubStore"/> against a disposable fixture directory that
/// mimics Camera Hub's on-disk layout (AppSettings.json + Texts/&lt;GUID&gt;.json). Never
/// touches a real Camera Hub installation. The "is Camera Hub running" check is always
/// injected as false here so tests are deterministic regardless of whether the machine
/// running them happens to have Camera Hub installed and open.
/// </summary>
public class CameraHubStoreTests : IDisposable
{
    private readonly TempDirectory _hub = new();
    private readonly TempDirectory _backups = new();

    private CameraHubStore Store => new(_hub.Path, _backups.Path, backupRetention: 10, isCameraHubRunning: () => false);

    public void Dispose()
    {
        _hub.Dispose();
        _backups.Dispose();
    }

    [Fact]
    public void ReadAll_MissingDirectory_ReportsNotFound_NotFatal()
    {
        Directory.Delete(_hub.Path, recursive: true);
        var result = Store.ReadAll();

        Assert.False(result.CameraHubDirectoryFound);
        Assert.True(result.Success);
    }

    [Fact]
    public void ReadAll_MissingAppSettings_IsValidEmpty()
    {
        var result = Store.ReadAll();

        Assert.True(result.CameraHubDirectoryFound);
        Assert.False(result.AppSettingsFound);
        Assert.True(result.Success);
        Assert.Empty(result.Entries);
    }

    [Fact]
    public void ReadAll_MalformedAppSettingsJson_IsFatal()
    {
        File.WriteAllText(Path.Combine(_hub.Path, "AppSettings.json"), "{ not valid json");
        var result = Store.ReadAll();

        Assert.False(result.Success);
        Assert.NotNull(result.FatalError);
    }

    [Fact]
    public void ReadAll_MissingReferencedTextFile_IsReportedPerEntry_NotFatal()
    {
        var id = Guid.NewGuid();
        WriteAppSettings([id]);
        // Deliberately do not create Texts/<id>.json.

        var result = Store.ReadAll();

        Assert.True(result.Success);
        var entry = Assert.Single(result.Entries);
        Assert.Null(entry.Text);
        Assert.NotNull(entry.Error);
    }

    [Fact]
    public void PushOne_NewScript_CreatesTextFileAndUpdatesLibraryList()
    {
        var store = Store;
        var id = Guid.NewGuid();

        var result = store.PushOne(id, "My Pushed Script", ["Chapter one.", "Chapter two."]);

        Assert.True(result.Success);
        Assert.True(File.Exists(store.TextPath(id)));

        var read = store.ReadAll();
        var entry = Assert.Single(read.Entries);
        Assert.Equal(id, entry.Id);
        Assert.Equal("My Pushed Script", entry.Text!.FriendlyName);
    }

    [Fact]
    public void PushOne_CreatesBackupBeforeWriting()
    {
        var store = Store;
        var id = Guid.NewGuid();
        store.PushOne(id, "First Version", ["Body."]);

        var result = store.PushOne(id, "Second Version", ["Body."]);

        Assert.True(result.Success);
        Assert.NotNull(result.BackupDirectory);
        Assert.True(Directory.Exists(result.BackupDirectory));
        // The backup captures the pre-write state (first version), not the just-written one.
        var backedUpText = Path.Combine(result.BackupDirectory!, "Texts", id + ".json");
        Assert.True(File.Exists(backedUpText));
        Assert.Contains("First Version", File.ReadAllText(backedUpText));
    }

    [Fact]
    public void PushOne_UpdateExisting_PreservesLibraryListPosition()
    {
        var store = Store;
        var idA = Guid.NewGuid();
        var idB = Guid.NewGuid();
        store.PushOne(idA, "A", ["Body."]);
        store.PushOne(idB, "B", ["Body."]);

        store.PushOne(idA, "A Updated", ["New body."]);

        var read = store.ReadAll();
        Assert.Equal(2, read.Entries.Count);
        Assert.Equal(0, read.Entries.First(e => e.Id == idA).PositionInLibraryList);
        Assert.Equal(1, read.Entries.First(e => e.Id == idB).PositionInLibraryList);
    }

    [Fact]
    public void PushOne_CameraHubRunning_Refuses_AndWritesNothing()
    {
        var runningStore = new CameraHubStore(_hub.Path, _backups.Path, isCameraHubRunning: () => true);
        var id = Guid.NewGuid();

        var result = runningStore.PushOne(id, "Should Not Write", ["Body."]);

        Assert.False(result.Success);
        Assert.Contains("running", result.Error, StringComparison.OrdinalIgnoreCase);
        Assert.False(File.Exists(Path.Combine(_hub.Path, "Texts", id + ".json")));
        Assert.False(File.Exists(Path.Combine(_hub.Path, "AppSettings.json")));
    }

    [Fact]
    public void PushOne_MissingDirectory_Refuses()
    {
        Directory.Delete(_hub.Path, recursive: true);
        var result = Store.PushOne(Guid.NewGuid(), "X", ["Body."]);

        Assert.False(result.Success);
        Assert.Contains("not found", result.Error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void PushOne_SchemaDrift_RefusesWithoutWriting()
    {
        // libraryList is a string instead of an array: schema drift prompter must not guess at.
        File.WriteAllText(
            Path.Combine(_hub.Path, "AppSettings.json"),
            """{ "applogic": { "prompter": { "libraryList": "not-an-array" } } }""");

        var id = Guid.NewGuid();
        var result = Store.PushOne(id, "Should Not Write", ["Body."]);

        Assert.False(result.Success);
        Assert.False(File.Exists(Path.Combine(_hub.Path, "Texts", id + ".json")));
    }

    [Fact]
    public void PruneBackups_KeepsOnlyNewestWithinRetention()
    {
        var store = new CameraHubStore(_hub.Path, _backups.Path, backupRetention: 2, isCameraHubRunning: () => false);
        var id = Guid.NewGuid();

        store.PushOne(id, "v1", ["Body."]);
        store.PushOne(id, "v2", ["Body."]);
        store.PushOne(id, "v3", ["Body."]);

        var remaining = Directory.GetDirectories(_backups.Path);
        Assert.True(remaining.Length <= 2, $"Expected at most 2 backups, found {remaining.Length}.");
    }

    private void WriteAppSettings(IReadOnlyList<Guid> ids)
    {
        var root = new JsonObject();
        CameraHubSchema.SetLibraryList(root, ids);
        File.WriteAllText(Path.Combine(_hub.Path, "AppSettings.json"), root.ToJsonString());
    }
}
