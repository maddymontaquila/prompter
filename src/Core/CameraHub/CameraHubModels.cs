using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json.Nodes;

namespace Prompter.Core.CameraHub;

/// <summary>In-memory representation of one <c>Texts/&lt;GUID&gt;.json</c> file's contents.</summary>
public sealed record CameraHubTextRecord(Guid Guid, IReadOnlyList<string> Chapters, string FriendlyName, int Index);

/// <summary>Converts between <see cref="CameraHubTextRecord"/> and the raw JSON shape Camera Hub uses.</summary>
public static class CameraHubTextMapper
{
    /// <summary>
    /// Reads a validated Texts/&lt;GUID&gt;.json document into a <see cref="CameraHubTextRecord"/>.
    /// Callers must validate with <see cref="CameraHubSchema.ValidateText"/> first.
    /// </summary>
    public static CameraHubTextRecord Read(JsonObject obj)
    {
        var guid = Guid.Parse(obj["GUID"]!.GetValue<string>());
        var chapters = obj["chapters"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();
        var friendlyName = obj["friendlyName"]!.GetValue<string>();
        var index = obj["index"]!.GetValue<int>();
        return new CameraHubTextRecord(guid, chapters, friendlyName, index);
    }

    /// <summary>Serializes a <see cref="CameraHubTextRecord"/> into the raw JSON shape Camera Hub expects.</summary>
    public static JsonObject Write(CameraHubTextRecord record)
    {
        var chapters = new JsonArray();
        foreach (var chapter in record.Chapters)
        {
            chapters.Add((JsonNode?)JsonValue.Create(chapter));
        }

        return new JsonObject
        {
            ["GUID"] = record.Guid.ToString(),
            ["chapters"] = chapters,
            ["friendlyName"] = record.FriendlyName,
            ["index"] = record.Index,
        };
    }
}
