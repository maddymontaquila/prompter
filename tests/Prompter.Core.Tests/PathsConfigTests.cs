using Prompter.Core;
using Xunit;

namespace Prompter.Core.Tests;

public class PathsConfigTests
{
    [Fact]
    public void ResolveHome_ExplicitValue_TakesPrecedenceOverEverything()
    {
        using var temp = new TempDirectory();
        var resolved = PathsConfig.ResolveHome(temp.Path);
        Assert.Equal(Path.GetFullPath(temp.Path), resolved);
    }

    [Fact]
    public void ResolveHome_EnvironmentVariable_UsedWhenNoExplicitValue()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable(PathsConfig.HomeEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(PathsConfig.HomeEnvironmentVariable, temp.Path);
            var resolved = PathsConfig.ResolveHome(null);
            Assert.Equal(Path.GetFullPath(temp.Path), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PathsConfig.HomeEnvironmentVariable, previous);
        }
    }

    [Fact]
    public void ResolveCameraHubDirectory_ExplicitValue_TakesPrecedence()
    {
        using var temp = new TempDirectory();
        var resolved = PathsConfig.ResolveCameraHubDirectory(temp.Path);
        Assert.Equal(Path.GetFullPath(temp.Path), resolved);
    }

    [Fact]
    public void ResolveCameraHubDirectory_EnvironmentVariable_UsedWhenNoExplicitValue()
    {
        using var temp = new TempDirectory();
        var previous = Environment.GetEnvironmentVariable(PathsConfig.CameraHubDirEnvironmentVariable);
        try
        {
            Environment.SetEnvironmentVariable(PathsConfig.CameraHubDirEnvironmentVariable, temp.Path);
            var resolved = PathsConfig.ResolveCameraHubDirectory(null);
            Assert.Equal(Path.GetFullPath(temp.Path), resolved);
        }
        finally
        {
            Environment.SetEnvironmentVariable(PathsConfig.CameraHubDirEnvironmentVariable, previous);
        }
    }
}
