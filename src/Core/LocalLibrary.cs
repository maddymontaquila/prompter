using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace Prompter.Core;

/// <summary>One script file that failed to parse, surfaced instead of being silently dropped.</summary>
public sealed record LibraryLoadError(string Path, string Reason);

/// <summary>Result of scanning the library directory: the valid scripts plus any parse failures.</summary>
public sealed record LibraryLoadResult(IReadOnlyList<ScriptRecord> Scripts, IReadOnlyList<LibraryLoadError> Errors);

/// <summary>
/// The local canonical script library: a directory of plain-text <c>.md</c> files, one per
/// script, addressed by a stable <see cref="Guid"/> stored in each file's frontmatter.
/// Renames change the file name (best-effort, collision-safe) but never the id, so identity
/// survives renames and Camera Hub push/pull mapping stays correct.
/// </summary>
public sealed class LocalLibrary
{
    private readonly string _libraryDirectory;

    public LocalLibrary(string homeDirectory)
    {
        HomeDirectory = homeDirectory;
        _libraryDirectory = Path.Combine(homeDirectory, "library");
    }

    public string HomeDirectory { get; }

    public string LibraryDirectory => _libraryDirectory;

    public void EnsureDirectoryExists() => Directory.CreateDirectory(_libraryDirectory);

    /// <summary>Scans the library directory and parses every script file.</summary>
    public LibraryLoadResult Load()
    {
        var scripts = new List<ScriptRecord>();
        var errors = new List<LibraryLoadError>();

        if (!Directory.Exists(_libraryDirectory))
        {
            return new LibraryLoadResult(scripts, errors);
        }

        foreach (var path in Directory.EnumerateFiles(_libraryDirectory, "*" + FileNaming.ScriptExtension).OrderBy(p => p, StringComparer.OrdinalIgnoreCase))
        {
            string text;
            try
            {
                text = File.ReadAllText(path);
            }
            catch (IOException ex)
            {
                errors.Add(new LibraryLoadError(path, ex.Message));
                continue;
            }

            if (ScriptDocument.TryParse(text, out var script, out var error))
            {
                scripts.Add(script!);
            }
            else
            {
                errors.Add(new LibraryLoadError(path, error ?? "Unknown parse error."));
            }
        }

        var duplicateIds = scripts.GroupBy(s => s.Id).Where(g => g.Count() > 1).ToList();
        foreach (var group in duplicateIds)
        {
            errors.Add(new LibraryLoadError(
                _libraryDirectory,
                $"Duplicate script id {group.Key:D} appears in {group.Count()} files; only the first will be used."));
        }

        var deduped = scripts
            .GroupBy(s => s.Id)
            .Select(g => g.First())
            .OrderBy(s => s.Order)
            .ThenBy(s => s.CreatedUtc)
            .ToList();

        return new LibraryLoadResult(deduped, errors);
    }

    /// <summary>Finds the on-disk path for a given script id, or null if not present.</summary>
    public string? FindPath(Guid id)
    {
        if (!Directory.Exists(_libraryDirectory)) return null;

        foreach (var path in Directory.EnumerateFiles(_libraryDirectory, "*" + FileNaming.ScriptExtension))
        {
            string text;
            try { text = File.ReadAllText(path); }
            catch (IOException) { continue; }

            if (ScriptDocument.TryParse(text, out var script, out _) && script!.Id == id)
            {
                return path;
            }
        }

        return null;
    }

    public ScriptRecord? Get(Guid id)
    {
        var path = FindPath(id);
        if (path is null) return null;
        return ScriptDocument.TryParse(File.ReadAllText(path), out var script, out _) ? script : null;
    }

    /// <summary>Finds scripts whose name matches exactly (case-insensitive). May return more than one.</summary>
    public IReadOnlyList<ScriptRecord> FindByName(string name)
        => Load().Scripts.Where(s => string.Equals(s.Name, name, StringComparison.OrdinalIgnoreCase)).ToList();

    public ScriptRecord Create(string name, IReadOnlyList<string> chapters)
    {
        EnsureDirectoryExists();
        var existing = Load().Scripts;
        var nextOrder = existing.Count == 0 ? 0 : existing.Max(s => s.Order) + 1;
        var script = ScriptRecord.Create(name, chapters, nextOrder);
        WriteNew(script);
        return script;
    }

    /// <summary>
    /// Creates a new local script that preserves a caller-supplied id (used when pulling a
    /// script from Camera Hub, where the id must match Camera Hub's GUID for identity to
    /// stay stable on future push/pull cycles).
    /// </summary>
    public ScriptRecord Import(Guid id, string name, IReadOnlyList<string> chapters, int order)
    {
        EnsureDirectoryExists();
        var now = DateTimeOffset.UtcNow;
        var script = new ScriptRecord(id, name, chapters, order, now, now);
        WriteNew(script);
        return script;
    }

    /// <summary>Persists changes to an existing script, renaming its file if the name changed.</summary>
    public void Save(ScriptRecord script)
    {
        EnsureDirectoryExists();
        var currentPath = FindPath(script.Id);
        var desiredPath = FileNaming.ResolveUniquePath(
            _libraryDirectory,
            script.Name,
            script.Id,
            candidate => !File.Exists(candidate) || string.Equals(candidate, currentPath, StringComparison.OrdinalIgnoreCase));

        var text = ScriptDocument.ToFileText(script);

        if (currentPath is not null && !string.Equals(currentPath, desiredPath, StringComparison.OrdinalIgnoreCase))
        {
            AtomicWrite(desiredPath, text);
            File.Delete(currentPath);
        }
        else
        {
            AtomicWrite(currentPath ?? desiredPath, text);
        }
    }

    public bool Delete(Guid id)
    {
        var path = FindPath(id);
        if (path is null) return false;
        File.Delete(path);
        return true;
    }

    /// <summary>Swaps display order with the adjacent script in the given direction.</summary>
    public bool Move(Guid id, int direction)
    {
        var scripts = Load().Scripts;
        var index = scripts.ToList().FindIndex(s => s.Id == id);
        if (index < 0) return false;

        var targetIndex = index + Math.Sign(direction);
        if (targetIndex < 0 || targetIndex >= scripts.Count) return false;

        var current = scripts[index];
        var target = scripts[targetIndex];

        Save(current.WithOrder(target.Order));
        Save(target.WithOrder(current.Order));
        return true;
    }

    private void WriteNew(ScriptRecord script)
    {
        var path = FileNaming.ResolveUniquePath(_libraryDirectory, script.Name, script.Id, candidate => !File.Exists(candidate));
        AtomicWrite(path, ScriptDocument.ToFileText(script));
    }

    private static void AtomicWrite(string path, string content)
    {
        var directory = Path.GetDirectoryName(path)!;
        Directory.CreateDirectory(directory);
        var tempPath = Path.Combine(directory, $".{Path.GetFileName(path)}.{Guid.NewGuid():N}.tmp");
        File.WriteAllText(tempPath, content);

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
