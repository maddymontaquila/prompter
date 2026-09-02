using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace Prompter.Core;

/// <summary>
/// Produces safe, deterministic file names for scripts from their (untrusted, user-editable)
/// display name, and resolves collisions deterministically so two scripts never fight over
/// the same path.
/// </summary>
public static class FileNaming
{
    private static readonly char[] InvalidChars = Path.GetInvalidFileNameChars()
        .Concat(['/', '\\', ':', '*', '?', '"', '<', '>', '|'])
        .Distinct()
        .ToArray();

    private static readonly HashSet<string> ReservedWindowsNames = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    /// <summary>Extension used for canonical local script files.</summary>
    public const string ScriptExtension = ".md";

    /// <summary>
    /// Converts a display name into a safe file-name slug (no extension). Never returns an
    /// empty string; falls back to "script" if nothing usable survives sanitization.
    /// </summary>
    public static string Sanitize(string name)
    {
        var trimmed = name.Trim();
        var sb = new StringBuilder(trimmed.Length);
        foreach (var ch in trimmed)
        {
            if (Array.IndexOf(InvalidChars, ch) >= 0 || char.IsControl(ch))
            {
                sb.Append('-');
            }
            else
            {
                sb.Append(ch);
            }
        }

        var slug = sb.ToString();
        // Collapse whitespace/dash runs and trim leading/trailing separators and dots
        // (trailing dots/spaces are invalid on Windows).
        slug = System.Text.RegularExpressions.Regex.Replace(slug, @"[-\s]+", "-");
        slug = slug.Trim('-', '.', ' ');

        if (slug.Length == 0)
        {
            slug = "script";
        }

        if (ReservedWindowsNames.Contains(slug))
        {
            slug = $"{slug}-script";
        }

        const int maxLength = 100;
        if (slug.Length > maxLength)
        {
            slug = slug[..maxLength].TrimEnd('-', '.', ' ');
        }

        return slug;
    }

    /// <summary>
    /// Picks a file path for a script inside <paramref name="directory"/> that does not
    /// collide with any existing file. Deterministically appends "-2", "-3", ... to the
    /// slug when the base name is already taken by a different script id.
    /// </summary>
    public static string ResolveUniquePath(string directory, string name, Guid id, Func<string, bool> pathIsFree)
    {
        var slug = Sanitize(name);
        var candidate = Path.Combine(directory, slug + ScriptExtension);
        if (pathIsFree(candidate))
        {
            return candidate;
        }

        for (var suffix = 2; suffix < 10_000; suffix++)
        {
            candidate = Path.Combine(directory, $"{slug}-{suffix}{ScriptExtension}");
            if (pathIsFree(candidate))
            {
                return candidate;
            }
        }

        // Astronomically unlikely fallback: disambiguate with the id itself.
        return Path.Combine(directory, $"{slug}-{id:N}{ScriptExtension}");
    }
}
