using System;
using System.Text.Json;
using System.Text.Json.Nodes;
using Prompter.Core;

namespace Prompter.Cli;

/// <summary>
/// Builds JSON output for CLI commands using <see cref="JsonNode"/> directly rather than a
/// reflection-based serializer, so JSON output stays Native-AOT/trimming friendly.
/// </summary>
public static class JsonOutput
{
    private static readonly JsonSerializerOptions Indented = new() { WriteIndented = true };

    public static void Write(System.IO.TextWriter writer, JsonNode node)
        => writer.WriteLine(node.ToJsonString(Indented));

    public static JsonObject ScriptSummary(ScriptRecord script) => new()
    {
        ["id"] = script.Id.ToString(),
        ["name"] = script.Name,
        ["order"] = script.Order,
        ["chapterCount"] = script.Chapters.Count,
        ["createdUtc"] = script.CreatedUtc.ToString("O"),
        ["updatedUtc"] = script.UpdatedUtc.ToString("O"),
    };

    public static JsonObject ScriptDetail(ScriptRecord script)
    {
        var obj = ScriptSummary(script);
        var chapters = new JsonArray();
        foreach (var chapter in script.Chapters)
        {
            chapters.Add((JsonNode?)JsonValue.Create(chapter));
        }
        obj["chapters"] = chapters;
        return obj;
    }
}
