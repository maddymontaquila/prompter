using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace Prompter.Core;

/// <summary>
/// Reads and writes the on-disk representation of a <see cref="ScriptRecord"/>: a small
/// plain-text frontmatter block followed by the script body. The body uses the same
/// convention as Elgato Camera Hub's <c>Texts/&lt;GUID&gt;.json</c> chapters: a blank line
/// separates chapters, and single newlines inside a chapter are soft line breaks. Keeping
/// the on-disk format textual (rather than an opaque database) makes it easy to diff,
/// grep, and hand-edit scripts, and keeps local &lt;-&gt; Camera Hub conversion a straight
/// round trip.
/// </summary>
public static class ScriptDocument
{
    private const string FrontmatterDelimiter = "---";

    /// <summary>Serializes a script to the on-disk text format.</summary>
    public static string ToFileText(ScriptRecord script)
    {
        var sb = new StringBuilder();
        sb.Append(FrontmatterDelimiter).Append('\n');
        sb.Append("id: ").Append(script.Id.ToString()).Append('\n');
        sb.Append("name: ").Append(EscapeFrontmatterValue(script.Name)).Append('\n');
        sb.Append("order: ").Append(script.Order.ToString(CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("createdUtc: ").Append(script.CreatedUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append("updatedUtc: ").Append(script.UpdatedUtc.ToString("O", CultureInfo.InvariantCulture)).Append('\n');
        sb.Append(FrontmatterDelimiter).Append('\n');
        sb.Append('\n');
        sb.Append(ChaptersToBody(script.Chapters));
        return sb.ToString();
    }

    /// <summary>Joins chapters into the shared blank-line-separated body text.</summary>
    public static string ChaptersToBody(IReadOnlyList<string> chapters)
        => string.Join("\n\n", chapters.Select(c => c.Replace("\r\n", "\n").TrimEnd('\n')));

    /// <summary>Splits body text back into chapters using blank-line separation.</summary>
    public static IReadOnlyList<string> BodyToChapters(string body)
    {
        var normalized = body.Replace("\r\n", "\n");
        // Split on one-or-more blank lines while preserving intentional soft breaks
        // within a chapter (a single '\n').
        var rawChapters = System.Text.RegularExpressions.Regex.Split(normalized, "\n{2,}");
        var chapters = rawChapters
            .Select(c => c.Trim('\n'))
            .Where(c => c.Length > 0)
            .ToList();
        return chapters.Count == 0 ? [""] : chapters;
    }

    /// <summary>Parses the on-disk text format back into a script. Throws <see cref="FormatException"/> on malformed input.</summary>
    public static ScriptRecord Parse(string fileText)
    {
        var text = fileText.Replace("\r\n", "\n");
        if (!text.StartsWith(FrontmatterDelimiter + "\n", StringComparison.Ordinal))
        {
            throw new FormatException("Script file is missing the leading '---' frontmatter delimiter.");
        }

        var afterFirstDelimiter = text[(FrontmatterDelimiter.Length + 1)..];
        var endIndex = afterFirstDelimiter.IndexOf("\n" + FrontmatterDelimiter + "\n", StringComparison.Ordinal);
        if (endIndex < 0)
        {
            throw new FormatException("Script file is missing the closing '---' frontmatter delimiter.");
        }

        var frontmatterBlock = afterFirstDelimiter[..endIndex];
        var body = afterFirstDelimiter[(endIndex + FrontmatterDelimiter.Length + 2)..].TrimStart('\n');

        var fields = ParseFrontmatterFields(frontmatterBlock);

        if (!fields.TryGetValue("id", out var idText) || !Guid.TryParse(idText, out var id))
        {
            throw new FormatException("Script file frontmatter is missing a valid 'id' field.");
        }

        if (!fields.TryGetValue("name", out var name) || string.IsNullOrWhiteSpace(name))
        {
            throw new FormatException("Script file frontmatter is missing a 'name' field.");
        }

        var order = 0;
        if (fields.TryGetValue("order", out var orderText))
        {
            int.TryParse(orderText, NumberStyles.Integer, CultureInfo.InvariantCulture, out order);
        }

        var createdUtc = DateTimeOffset.UtcNow;
        if (fields.TryGetValue("createdUtc", out var createdText))
        {
            DateTimeOffset.TryParse(createdText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out createdUtc);
        }

        var updatedUtc = createdUtc;
        if (fields.TryGetValue("updatedUtc", out var updatedText))
        {
            DateTimeOffset.TryParse(updatedText, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out updatedUtc);
        }

        var chapters = BodyToChapters(body);
        return new ScriptRecord(id, UnescapeFrontmatterValue(name), chapters, order, createdUtc, updatedUtc);
    }

    /// <summary>Attempts to parse the on-disk text format, returning false with a reason on failure instead of throwing.</summary>
    public static bool TryParse(string fileText, out ScriptRecord? script, out string? error)
    {
        try
        {
            script = Parse(fileText);
            error = null;
            return true;
        }
        catch (FormatException ex)
        {
            script = null;
            error = ex.Message;
            return false;
        }
    }

    private static Dictionary<string, string> ParseFrontmatterFields(string frontmatterBlock)
    {
        var fields = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var line in frontmatterBlock.Split('\n'))
        {
            if (string.IsNullOrWhiteSpace(line)) continue;
            var separatorIndex = line.IndexOf(':');
            if (separatorIndex < 0) continue;
            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            fields[key] = value;
        }
        return fields;
    }

    private static string EscapeFrontmatterValue(string value)
        => value.Replace("\\", "\\\\").Replace("\n", "\\n");

    private static string UnescapeFrontmatterValue(string value)
        => value.Replace("\\n", "\n").Replace("\\\\", "\\");
}
