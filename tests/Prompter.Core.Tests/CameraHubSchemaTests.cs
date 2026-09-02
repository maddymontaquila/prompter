using System.Text.Json.Nodes;
using Prompter.Core.CameraHub;
using Xunit;

namespace Prompter.Core.Tests;

public class CameraHubSchemaTests
{
    [Fact]
    public void ValidateLibraryList_MissingApplogic_IsValidEmpty()
    {
        var root = JsonNode.Parse("{}");
        var result = CameraHubSchema.ValidateLibraryList(root);

        Assert.True(result.IsValid);
        Assert.Empty(result.Ids);
    }

    [Fact]
    public void ValidateLibraryList_WellFormed_ExtractsIds()
    {
        var id1 = Guid.NewGuid();
        var id2 = Guid.NewGuid();
        var json = $$"""
        { "applogic": { "prompter": { "libraryList": ["{{id1}}", "{{id2}}"] } } }
        """;

        var result = CameraHubSchema.ValidateLibraryList(JsonNode.Parse(json));

        Assert.True(result.IsValid);
        Assert.Equal([id1, id2], result.Ids);
    }

    [Fact]
    public void ValidateLibraryList_ApplogicIsNotObject_IsSchemaDrift()
    {
        var root = JsonNode.Parse("""{ "applogic": "unexpected string" }""");
        var result = CameraHubSchema.ValidateLibraryList(root);

        Assert.False(result.IsValid);
        Assert.NotNull(result.Error);
    }

    [Fact]
    public void ValidateLibraryList_LibraryListIsNotArray_IsSchemaDrift()
    {
        var root = JsonNode.Parse("""{ "applogic": { "prompter": { "libraryList": "not-an-array" } } } """);
        var result = CameraHubSchema.ValidateLibraryList(root);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateLibraryList_NonGuidEntry_IsSchemaDrift()
    {
        var root = JsonNode.Parse("""{ "applogic": { "prompter": { "libraryList": ["not-a-guid"] } } } """);
        var result = CameraHubSchema.ValidateLibraryList(root);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void SetLibraryList_RoundTripsThroughValidate()
    {
        var root = new JsonObject();
        var ids = new[] { Guid.NewGuid(), Guid.NewGuid() };

        CameraHubSchema.SetLibraryList(root, ids);
        var result = CameraHubSchema.ValidateLibraryList(root);

        Assert.True(result.IsValid);
        Assert.Equal(ids, result.Ids);
    }

    [Fact]
    public void ValidateText_WellFormed_IsValid()
    {
        var record = new CameraHubTextRecord(Guid.NewGuid(), ["Chapter one.", "Chapter two."], "My Script", 0);
        var json = CameraHubTextMapper.Write(record);

        var result = CameraHubSchema.ValidateText(json);
        Assert.True(result.IsValid);
    }

    [Fact]
    public void ValidateText_MissingChapters_IsInvalid()
    {
        var json = JsonNode.Parse($$"""{ "GUID": "{{Guid.NewGuid()}}", "friendlyName": "x", "index": 0 }""");
        var result = CameraHubSchema.ValidateText(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void ValidateText_MissingGuid_IsInvalid()
    {
        var json = JsonNode.Parse("""{ "chapters": [], "friendlyName": "x", "index": 0 }""");
        var result = CameraHubSchema.ValidateText(json);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void TextMapper_RoundTrips()
    {
        var record = new CameraHubTextRecord(Guid.NewGuid(), ["A", "B", "C"], "Friendly", 2);
        var json = CameraHubTextMapper.Write(record);
        var read = CameraHubTextMapper.Read(json);

        Assert.Equal(record.Guid, read.Guid);
        Assert.Equal(record.Chapters, read.Chapters);
        Assert.Equal(record.FriendlyName, read.FriendlyName);
        Assert.Equal(record.Index, read.Index);
    }
}
