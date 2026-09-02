using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Nodes;

namespace Prompter.Core.CameraHub;

/// <summary>One script's status as seen through Camera Hub's <c>libraryList</c> + Texts files.</summary>
public sealed record CameraHubEntry(Guid Id, int PositionInLibraryList, CameraHubTextRecord? Text, string? Error);

/// <summary>Result of a read-only scan of the Camera Hub prompter data.</summary>
public sealed record CameraHubReadResult(
    bool CameraHubDirectoryFound,
    bool AppSettingsFound,
    IReadOnlyList<CameraHubEntry> Entries,
    string? FatalError)
{
    public bool Success => FatalError is null;
}

/// <summary>Result of a Camera Hub write (push) operation.</summary>
public sealed record CameraHubWriteResult(bool Success, string? Error, string? BackupDirectory);

/// <summary>
/// Reads and conservatively writes Elgato Camera Hub's prompter data
/// (<c>AppSettings.json</c> + <c>Texts/&lt;GUID&gt;.json</c>). Writes always take a
/// timestamped backup first, write via a temp-file-then-replace sequence for atomicity per
/// file, verify the result by re-reading it, and roll back every touched file if anything
/// goes wrong. Reads never write and continue past individual malformed records instead of
/// aborting the whole scan.
/// </summary>
public sealed class CameraHubStore
{
    private static readonly JsonSerializerOptions IndentedJson = new() { WriteIndented = true };

    public CameraHubStore(string cameraHubDirectory, string backupRootDirectory, int backupRetention = 10, Func<bool>? isCameraHubRunning = null)
    {
        CameraHubDirectory = cameraHubDirectory;
        BackupRootDirectory = backupRootDirectory;
        BackupRetention = backupRetention;
        _isCameraHubRunning = isCameraHubRunning ?? ProcessGuard.IsCameraHubRunning;
    }

    private readonly Func<bool> _isCameraHubRunning;

    public string CameraHubDirectory { get; }
    public string BackupRootDirectory { get; }
    public int BackupRetention { get; }

    public string AppSettingsPath => Path.Combine(CameraHubDirectory, "AppSettings.json");
    public string TextsDirectory => Path.Combine(CameraHubDirectory, "Texts");
    public string TextPath(Guid id) => Path.Combine(TextsDirectory, id.ToString() + ".json");

    /// <summary>
    /// Scans <c>AppSettings.json</c>'s <c>libraryList</c> and the corresponding Texts files.
    /// Never writes. Individual malformed/missing text files are reported per-entry; only a
    /// corrupt <c>AppSettings.json</c> (unparsable, or <c>libraryList</c> itself has drifted)
    /// is fatal, since there is then no reliable list of ids to walk.
    /// </summary>
    public CameraHubReadResult ReadAll()
    {
        if (!Directory.Exists(CameraHubDirectory))
        {
            return new CameraHubReadResult(false, false, [], null);
        }

        if (!File.Exists(AppSettingsPath))
        {
            return new CameraHubReadResult(true, false, [], null);
        }

        JsonNode? root;
        try
        {
            root = JsonNode.Parse(File.ReadAllText(AppSettingsPath));
        }
        catch (JsonException ex)
        {
            return new CameraHubReadResult(true, true, [], $"AppSettings.json is not valid JSON: {ex.Message}");
        }

        var validation = CameraHubSchema.ValidateLibraryList(root);
        if (!validation.IsValid)
        {
            return new CameraHubReadResult(true, true, [], validation.Error);
        }

        var entries = new List<CameraHubEntry>();
        for (var position = 0; position < validation.Ids.Count; position++)
        {
            var id = validation.Ids[position];
            var (text, error) = ReadText(id);
            entries.Add(new CameraHubEntry(id, position, text, error));
        }

        return new CameraHubReadResult(true, true, entries, null);
    }

    private (CameraHubTextRecord? Text, string? Error) ReadText(Guid id)
    {
        var path = TextPath(id);
        if (!File.Exists(path))
        {
            return (null, $"Texts/{id}.json is referenced by libraryList but does not exist.");
        }

        JsonNode? node;
        try
        {
            node = JsonNode.Parse(File.ReadAllText(path));
        }
        catch (JsonException ex)
        {
            return (null, $"Texts/{id}.json is not valid JSON: {ex.Message}");
        }

        var validation = CameraHubSchema.ValidateText(node);
        if (!validation.IsValid)
        {
            return (null, $"Texts/{id}.json: {validation.Error}");
        }

        return (CameraHubTextMapper.Read((JsonObject)node!), null);
    }

    /// <summary>
    /// Pushes one script to Camera Hub: creates or updates its Texts/&lt;GUID&gt;.json and
    /// ensures its id is present in <c>libraryList</c> (appended if new). Refuses if Camera
    /// Hub is running, its directory is missing, or the existing schema has drifted from
    /// what prompter understands.
    /// </summary>
    public CameraHubWriteResult PushOne(Guid id, string friendlyName, IReadOnlyList<string> chapters)
    {
        if (_isCameraHubRunning())
        {
            return new CameraHubWriteResult(false, "Camera Hub is currently running. Close it before pushing to avoid corrupting its data, then try again.", null);
        }

        if (!Directory.Exists(CameraHubDirectory))
        {
            return new CameraHubWriteResult(false, $"Camera Hub data directory not found at '{CameraHubDirectory}'. Is Camera Hub installed?", null);
        }

        JsonNode? root;
        var appSettingsExisted = File.Exists(AppSettingsPath);
        try
        {
            root = appSettingsExisted ? JsonNode.Parse(File.ReadAllText(AppSettingsPath)) : new JsonObject();
        }
        catch (JsonException ex)
        {
            return new CameraHubWriteResult(false, $"Refusing to write: AppSettings.json is not valid JSON: {ex.Message}", null);
        }

        if (root is not JsonObject rootObject)
        {
            return new CameraHubWriteResult(false, "Refusing to write: AppSettings.json root is not a JSON object.", null);
        }

        var validation = CameraHubSchema.ValidateLibraryList(rootObject);
        if (!validation.IsValid)
        {
            return new CameraHubWriteResult(false, $"Refusing to write: {validation.Error}", null);
        }

        var ids = validation.Ids.ToList();
        var position = ids.IndexOf(id);
        var isNew = position < 0;
        if (isNew)
        {
            ids.Add(id);
            position = ids.Count - 1;
        }

        CameraHubSchema.SetLibraryList(rootObject, ids);
        var textRecord = new CameraHubTextRecord(id, chapters, friendlyName, position);
        var textJson = CameraHubTextMapper.Write(textRecord);

        var backupDirectory = CreateBackup(id, appSettingsExisted);

        try
        {
            AtomicWriteJson(TextPath(id), textJson);
            AtomicWriteJson(AppSettingsPath, rootObject);

            var verify = ReadAll();
            var verifiedEntry = verify.Entries.FirstOrDefault(e => e.Id == id);
            if (!verify.Success || verifiedEntry is null || verifiedEntry.Text is null || verifiedEntry.Text.FriendlyName != friendlyName)
            {
                RestoreBackup(backupDirectory, id, appSettingsExisted);
                return new CameraHubWriteResult(false, "Push failed verification after write; rolled back to the pre-write backup.", backupDirectory);
            }

            PruneBackups();
            return new CameraHubWriteResult(true, null, backupDirectory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            RestoreBackup(backupDirectory, id, appSettingsExisted);
            return new CameraHubWriteResult(false, $"Push failed ({ex.Message}); rolled back to the pre-write backup.", backupDirectory);
        }
    }

    private string CreateBackup(Guid id, bool appSettingsExisted)
    {
        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss-fffffff");
        var backupDirectory = Path.Combine(BackupRootDirectory, timestamp);
        Directory.CreateDirectory(backupDirectory);

        if (appSettingsExisted)
        {
            File.Copy(AppSettingsPath, Path.Combine(backupDirectory, "AppSettings.json"), overwrite: true);
        }

        var textPath = TextPath(id);
        if (File.Exists(textPath))
        {
            Directory.CreateDirectory(Path.Combine(backupDirectory, "Texts"));
            File.Copy(textPath, Path.Combine(backupDirectory, "Texts", id + ".json"), overwrite: true);
        }

        return backupDirectory;
    }

    private void RestoreBackup(string backupDirectory, Guid id, bool appSettingsExisted)
    {
        var backedUpAppSettings = Path.Combine(backupDirectory, "AppSettings.json");
        if (appSettingsExisted && File.Exists(backedUpAppSettings))
        {
            File.Copy(backedUpAppSettings, AppSettingsPath, overwrite: true);
        }
        else if (!appSettingsExisted && File.Exists(AppSettingsPath))
        {
            // The file did not exist before this push; remove the one we created.
            File.Delete(AppSettingsPath);
        }

        var backedUpText = Path.Combine(backupDirectory, "Texts", id + ".json");
        var textPath = TextPath(id);
        if (File.Exists(backedUpText))
        {
            File.Copy(backedUpText, textPath, overwrite: true);
        }
        else if (File.Exists(textPath))
        {
            // The text file did not exist before this push; remove the one we created.
            File.Delete(textPath);
        }
    }

    /// <summary>Deletes all but the newest <see cref="BackupRetention"/> backup folders.</summary>
    public void PruneBackups()
    {
        if (!Directory.Exists(BackupRootDirectory)) return;

        var backups = Directory.GetDirectories(BackupRootDirectory)
            .OrderByDescending(d => d, StringComparer.OrdinalIgnoreCase)
            .Skip(Math.Max(0, BackupRetention))
            .ToList();

        foreach (var stale in backups)
        {
            try { Directory.Delete(stale, recursive: true); }
            catch (IOException) { /* best-effort cleanup; a stuck backup is not fatal */ }
        }
    }

    private static void AtomicWriteJson(string path, JsonNode node)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, node.ToJsonString(IndentedJson));

        if (File.Exists(path))
        {
            File.Replace(tempPath, path, null);
        }
        else
        {
            File.Move(tempPath, path);
        }
    }
}
