using System;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace Prompter.Core.Backup;

/// <summary>
/// Creates and restores zip backups of the local canonical library
/// (<c>&lt;home&gt;/library</c>). Independent from the Camera Hub pre-write safety
/// backups in <see cref="CameraHub.CameraHubStore"/> — this is about the user's authored
/// scripts, not Camera Hub's own files.
/// </summary>
public static class LibraryBackup
{
    /// <summary>Creates a timestamped zip backup of the library directory and returns its path.</summary>
    public static string Create(string homeDirectory, string? outputDirectory = null)
    {
        var libraryDirectory = Path.Combine(homeDirectory, "library");
        var backupsDirectory = outputDirectory ?? Path.Combine(homeDirectory, "backups");
        Directory.CreateDirectory(backupsDirectory);

        var timestamp = DateTimeOffset.UtcNow.ToString("yyyyMMdd-HHmmss");
        var zipPath = Path.Combine(backupsDirectory, $"prompter-library-{timestamp}.zip");

        // Guarantee a unique name even if two backups are requested within the same second.
        var suffix = 1;
        while (File.Exists(zipPath))
        {
            zipPath = Path.Combine(backupsDirectory, $"prompter-library-{timestamp}-{++suffix}.zip");
        }

        using (var archive = ZipFile.Open(zipPath, ZipArchiveMode.Create))
        {
            if (Directory.Exists(libraryDirectory))
            {
                foreach (var file in Directory.EnumerateFiles(libraryDirectory, "*", SearchOption.AllDirectories))
                {
                    var entryName = "library/" + Path.GetRelativePath(libraryDirectory, file).Replace('\\', '/');
                    archive.CreateEntryFromFile(file, entryName);
                }
            }
        }

        return zipPath;
    }

    /// <summary>
    /// Restores the library directory from a zip backup previously produced by
    /// <see cref="Create"/>. Overwrites the current library contents, so callers must gate
    /// this behind an explicit user confirmation.
    /// </summary>
    public static void Restore(string homeDirectory, string backupZipPath)
    {
        if (!File.Exists(backupZipPath))
        {
            throw new FileNotFoundException("Backup file not found.", backupZipPath);
        }

        using var archive = ZipFile.OpenRead(backupZipPath);
        var libraryEntries = archive.Entries.Where(e => e.FullName.StartsWith("library/", StringComparison.Ordinal)).ToList();
        if (libraryEntries.Count == 0)
        {
            throw new InvalidDataException("Backup archive does not contain a 'library/' folder; refusing to restore.");
        }

        var libraryDirectory = Path.Combine(homeDirectory, "library");
        var stagingDirectory = Path.Combine(homeDirectory, $".restore-staging-{Guid.NewGuid():N}");
        Directory.CreateDirectory(stagingDirectory);

        try
        {
            foreach (var entry in libraryEntries)
            {
                var relative = entry.FullName["library/".Length..];
                if (relative.Length == 0) continue;

                var destination = Path.Combine(stagingDirectory, relative.Replace('/', Path.DirectorySeparatorChar));
                Directory.CreateDirectory(Path.GetDirectoryName(destination)!);
                entry.ExtractToFile(destination, overwrite: true);
            }

            if (Directory.Exists(libraryDirectory))
            {
                Directory.Delete(libraryDirectory, recursive: true);
            }

            Directory.Move(stagingDirectory, libraryDirectory);
        }
        finally
        {
            if (Directory.Exists(stagingDirectory))
            {
                Directory.Delete(stagingDirectory, recursive: true);
            }
        }
    }
}
