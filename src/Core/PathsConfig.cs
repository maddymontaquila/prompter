using System;
using System.IO;
using System.Runtime.InteropServices;

namespace Prompter.Core;

/// <summary>
/// Resolves the on-disk locations prompter uses: the local canonical library home, and the
/// Elgato Camera Hub data directory. Both can be overridden (env var and/or explicit value,
/// typically from a CLI option) so tests never touch a real user's data.
/// </summary>
public static class PathsConfig
{
    public const string HomeEnvironmentVariable = "PROMPTER_HOME";
    public const string CameraHubDirEnvironmentVariable = "PROMPTER_CAMERA_HUB_DIR";

    /// <summary>
    /// Resolves the prompter home directory (the local canonical library root).
    /// Precedence: <paramref name="explicitHome"/> &gt; <c>PROMPTER_HOME</c> env var &gt; OS default.
    /// The directory is not created by this method.
    /// </summary>
    public static string ResolveHome(string? explicitHome = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitHome))
        {
            return Path.GetFullPath(explicitHome);
        }

        var fromEnv = Environment.GetEnvironmentVariable(HomeEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        return Path.GetFullPath(Path.Combine(GetAppDataRoot(), "Prompter"));
    }

    /// <summary>
    /// Resolves the Elgato Camera Hub data directory. Precedence:
    /// <paramref name="explicitDir"/> &gt; <c>PROMPTER_CAMERA_HUB_DIR</c> env var &gt; the
    /// documented per-OS "Camera Hub" directory &gt; the legacy "CameraHub" directory (only
    /// used as a fallback when the normal directory does not exist).
    /// </summary>
    public static string ResolveCameraHubDirectory(string? explicitDir = null)
    {
        if (!string.IsNullOrWhiteSpace(explicitDir))
        {
            return Path.GetFullPath(explicitDir);
        }

        var fromEnv = Environment.GetEnvironmentVariable(CameraHubDirEnvironmentVariable);
        if (!string.IsNullOrWhiteSpace(fromEnv))
        {
            return Path.GetFullPath(fromEnv);
        }

        var appDataRoot = GetAppDataRoot();
        var standard = Path.Combine(appDataRoot, "Elgato", "Camera Hub");
        if (Directory.Exists(standard))
        {
            return standard;
        }

        var legacy = Path.Combine(appDataRoot, "Elgato", "CameraHub");
        if (Directory.Exists(legacy))
        {
            return legacy;
        }

        // Neither exists yet (fresh machine / fixture) - default to the documented standard
        // path; callers treat a missing directory as "Camera Hub not installed / never run".
        return standard;
    }

    private static string GetAppDataRoot()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            var home = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
            return Path.Combine(home, "Library", "Application Support");
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            return Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        }

        // Linux and other platforms: XDG data home, matching common .NET tool conventions.
        var xdgDataHome = Environment.GetEnvironmentVariable("XDG_DATA_HOME");
        if (!string.IsNullOrWhiteSpace(xdgDataHome))
        {
            return xdgDataHome;
        }

        var linuxHome = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
        return Path.Combine(linuxHome, ".local", "share");
    }
}
