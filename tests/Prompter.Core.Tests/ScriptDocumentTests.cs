using Prompter.Core;
using Xunit;

namespace Prompter.Core.Tests;

public class ScriptDocumentTests
{
    [Fact]
    public void RoundTrip_PreservesAllFields()
    {
        var script = ScriptRecord.Create("My Demo Script", ["Chapter one.\nSecond line.", "Chapter two."], order: 3);

        var text = ScriptDocument.ToFileText(script);
        var parsed = ScriptDocument.Parse(text);

        Assert.Equal(script.Id, parsed.Id);
        Assert.Equal(script.Name, parsed.Name);
        Assert.Equal(script.Order, parsed.Order);
        Assert.Equal(script.Chapters, parsed.Chapters);
        Assert.Equal(script.CreatedUtc, parsed.CreatedUtc);
        Assert.Equal(script.UpdatedUtc, parsed.UpdatedUtc);
    }

    [Fact]
    public void RoundTrip_NameWithColonAndNewlineEscapesCorrectly()
    {
        var script = ScriptRecord.Create("Title: with a colon", ["Body."], order: 0);
        var withNewline = script.WithName("Multi\nLine Name");

        var text = ScriptDocument.ToFileText(withNewline);
        var parsed = ScriptDocument.Parse(text);

        Assert.Equal("Multi\nLine Name", parsed.Name);
    }

    [Theory]
    [InlineData("Chapter one.\n\nChapter two.", new[] { "Chapter one.", "Chapter two." })]
    [InlineData("Chapter one.\n\n\nChapter two.", new[] { "Chapter one.", "Chapter two." })]
    [InlineData("Line one.\nLine two still chapter one.\n\nChapter two.", new[] { "Line one.\nLine two still chapter one.", "Chapter two." })]
    public void BodyToChapters_SplitsOnBlankLinesOnly(string body, string[] expected)
    {
        var chapters = ScriptDocument.BodyToChapters(body);
        Assert.Equal(expected, chapters);
    }

    [Fact]
    public void ChaptersToBody_JoinsWithBlankLine()
    {
        var body = ScriptDocument.ChaptersToBody(["First.", "Second."]);
        Assert.Equal("First.\n\nSecond.", body);
    }

    [Fact]
    public void Parse_MissingFrontmatterDelimiter_Throws()
    {
        Assert.Throws<FormatException>(() => ScriptDocument.Parse("not a script file"));
    }

    [Fact]
    public void Parse_MissingId_Throws()
    {
        var text = "---\nname: Foo\n---\n\nBody.";
        Assert.Throws<FormatException>(() => ScriptDocument.Parse(text));
    }

    [Fact]
    public void TryParse_ReturnsFalseWithReason_InsteadOfThrowing()
    {
        var ok = ScriptDocument.TryParse("garbage", out var script, out var error);

        Assert.False(ok);
        Assert.Null(script);
        Assert.NotNull(error);
    }

    [Fact]
    public void BodyToChapters_EmptyBody_ReturnsSingleEmptyChapter()
    {
        var chapters = ScriptDocument.BodyToChapters("");
        Assert.Single(chapters);
        Assert.Equal("", chapters[0]);
    }
}
