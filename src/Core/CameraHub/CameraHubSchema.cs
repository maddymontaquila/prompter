using System;
using System.Collections.Generic;
using System.Text.Json.Nodes;

namespace Prompter.Core.CameraHub;

/// <summary>
/// Structural validation for the slice of Elgato Camera Hub's on-disk schema that prompter
/// touches. Deliberately conservative: prompter only understands
/// <c>applogic.prompter.libraryList</c> inside <c>AppSettings.json</c> and the
/// <c>Texts/&lt;GUID&gt;.json</c> shape, so anything that doesn't match those exact shapes is
/// reported as schema drift rather than guessed at or overwritten.
/// </summary>
public static class CameraHubSchema
{
    /// <summary>Dotted path prompter reads/writes inside AppSettings.json.</summary>
    public const string LibraryListPropertyPath = "applogic.prompter.libraryList";

    public sealed record LibraryListValidation(bool IsValid, IReadOnlyList<Guid> Ids, string? Error)
    {
        public static LibraryListValidation Ok(IReadOnlyList<Guid> ids) => new(true, ids, null);
        public static LibraryListValidation Fail(string error) => new(false, [], error);
    }

    /// <summary>
    /// Validates and extracts <c>applogic.prompter.libraryList</c> from a parsed
    /// AppSettings.json document. A missing key at any level is treated as "no prompter
    /// data yet" (valid, empty list) rather than drift, since that's the expected state on
    /// a fresh Camera Hub install. A key present with an unexpected shape is drift.
    /// </summary>
    public static LibraryListValidation ValidateLibraryList(JsonNode? root)
    {
        if (root is null)
        {
            return LibraryListValidation.Fail("AppSettings.json is empty.");
        }

        if (root is not JsonObject rootObject)
        {
            return LibraryListValidation.Fail("AppSettings.json root is not a JSON object.");
        }

        if (!TryGetChild(rootObject, "applogic", out var applogicNode))
        {
            return LibraryListValidation.Ok([]);
        }

        if (applogicNode is not JsonObject applogicObject)
        {
            return LibraryListValidation.Fail("'applogic' is present in AppSettings.json but is not a JSON object (possible schema drift).");
        }

        if (!TryGetChild(applogicObject, "prompter", out var prompterNode))
        {
            return LibraryListValidation.Ok([]);
        }

        if (prompterNode is not JsonObject prompterObject)
        {
            return LibraryListValidation.Fail("'applogic.prompter' is present in AppSettings.json but is not a JSON object (possible schema drift).");
        }

        if (!TryGetChild(prompterObject, "libraryList", out var listNode))
        {
            return LibraryListValidation.Ok([]);
        }

        if (listNode is not JsonArray array)
        {
            return LibraryListValidation.Fail("'applogic.prompter.libraryList' is present but is not a JSON array (possible schema drift).");
        }

        var ids = new List<Guid>();
        foreach (var item in array)
        {
            if (item is not JsonValue value || !value.TryGetValue<string>(out var text) || !Guid.TryParse(text, out var id))
            {
                return LibraryListValidation.Fail("'applogic.prompter.libraryList' contains an entry that is not a GUID string (possible schema drift).");
            }

            ids.Add(id);
        }

        return LibraryListValidation.Ok(ids);
    }

    /// <summary>
    /// Writes <paramref name="ids"/> back into <paramref name="root"/> at
    /// <c>applogic.prompter.libraryList</c>, creating intermediate objects as needed.
    /// Caller must have already validated the existing shape with
    /// <see cref="ValidateLibraryList"/> to avoid clobbering unexpected structures.
    /// </summary>
    public static void SetLibraryList(JsonObject root, IReadOnlyList<Guid> ids)
    {
        if (!TryGetChild(root, "applogic", out var applogicNode) || applogicNode is not JsonObject applogicObject)
        {
            applogicObject = new JsonObject();
            root["applogic"] = applogicObject;
        }

        if (!TryGetChild(applogicObject, "prompter", out var prompterNode) || prompterNode is not JsonObject prompterObject)
        {
            prompterObject = new JsonObject();
            applogicObject["prompter"] = prompterObject;
        }

        var array = new JsonArray();
        foreach (var id in ids)
        {
            array.Add((JsonNode?)JsonValue.Create(id.ToString()));
        }

        prompterObject["libraryList"] = array;
    }

    public sealed record TextValidation(bool IsValid, string? Error);

    /// <summary>Validates the shape of a parsed <c>Texts/&lt;GUID&gt;.json</c> document.</summary>
    public static TextValidation ValidateText(JsonNode? root)
    {
        if (root is not JsonObject obj)
        {
            return new TextValidation(false, "Text file root is not a JSON object.");
        }

        if (!TryGetChild(obj, "GUID", out var guidNode) ||
            guidNode is not JsonValue guidValue ||
            !guidValue.TryGetValue<string>(out var guidText) ||
            !Guid.TryParse(guidText, out _))
        {
            return new TextValidation(false, "Text file is missing a valid 'GUID' string field.");
        }

        if (!TryGetChild(obj, "chapters", out var chaptersNode) || chaptersNode is not JsonArray chaptersArray)
        {
            return new TextValidation(false, "Text file is missing a 'chapters' array.");
        }

        foreach (var chapter in chaptersArray)
        {
            if (chapter is not JsonValue chapterValue || !chapterValue.TryGetValue<string>(out _))
            {
                return new TextValidation(false, "Text file 'chapters' array contains a non-string entry.");
            }
        }

        if (!TryGetChild(obj, "friendlyName", out var nameNode) ||
            nameNode is not JsonValue nameValue ||
            !nameValue.TryGetValue<string>(out _))
        {
            return new TextValidation(false, "Text file is missing a 'friendlyName' string field.");
        }

        if (!TryGetChild(obj, "index", out var indexNode) ||
            indexNode is not JsonValue indexValue ||
            !indexValue.TryGetValue<int>(out _))
        {
            return new TextValidation(false, "Text file is missing an 'index' number field.");
        }

        return new TextValidation(true, null);
    }

    private static bool TryGetChild(JsonObject obj, string propertyName, out JsonNode? value)
    {
        if (obj.TryGetPropertyValue(propertyName, out value) && value is not null)
        {
            return true;
        }

        value = null;
        return false;
    }
}
