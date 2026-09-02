using Prompter.Core;
using Xunit;

namespace Prompter.Core.Tests;

public class FileNamingTests
{
    [Theory]
    [InlineData("My Script", "My-Script")]
    [InlineData("Weird/Name:With*Bad?Chars", "Weird-Name-With-Bad-Chars")]
    [InlineData("   leading and trailing   ", "leading-and-trailing")]
    [InlineData("multiple   spaces", "multiple-spaces")]
    [InlineData("trailing.dots...", "trailing.dots")]
    [InlineData("", "script")]
    [InlineData("   ", "script")]
    [InlineData("....", "script")]
    public void Sanitize_ProducesSafeSlug(string input, string expected)
    {
        Assert.Equal(expected, FileNaming.Sanitize(input));
    }

    [Theory]
    [InlineData("CON")]
    [InlineData("con")]
    [InlineData("NUL")]
    [InlineData("COM1")]
    [InlineData("LPT9")]
    public void Sanitize_ReservedWindowsNames_GetsSuffixed(string reserved)
    {
        var result = FileNaming.Sanitize(reserved);
        Assert.NotEqual(reserved, result, StringComparer.OrdinalIgnoreCase);
        Assert.EndsWith("-script", result, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Sanitize_VeryLongName_IsTruncated()
    {
        var longName = new string('a', 500);
        var result = FileNaming.Sanitize(longName);
        Assert.True(result.Length <= 100);
    }

    [Fact]
    public void ResolveUniquePath_NoCollision_ReturnsBaseSlug()
    {
        var path = FileNaming.ResolveUniquePath("/lib", "My Script", Guid.NewGuid(), _ => true);
        Assert.Equal(Path.Combine("/lib", "My-Script.md"), path);
    }

    [Fact]
    public void ResolveUniquePath_Collision_AppendsDeterministicSuffix()
    {
        // First candidate ("My-Script.md") is taken; "-2" is also taken; "-3" is free.
        var taken = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            Path.Combine("/lib", "My-Script.md"),
            Path.Combine("/lib", "My-Script-2.md"),
        };

        var path = FileNaming.ResolveUniquePath("/lib", "My Script", Guid.NewGuid(), candidate => !taken.Contains(candidate));

        Assert.Equal(Path.Combine("/lib", "My-Script-3.md"), path);
    }

    [Fact]
    public void ResolveUniquePath_IsDeterministic_SameInputsSameOutput()
    {
        var id = Guid.NewGuid();
        var pathA = FileNaming.ResolveUniquePath("/lib", "Repeatable", id, _ => true);
        var pathB = FileNaming.ResolveUniquePath("/lib", "Repeatable", id, _ => true);
        Assert.Equal(pathA, pathB);
    }
}
