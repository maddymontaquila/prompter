using System;
using System.Diagnostics;
using System.Linq;

namespace Prompter.Core.CameraHub;

/// <summary>
/// Checks whether the Elgato Camera Hub application is currently running, so writes can
/// refuse to proceed instead of racing a live process that has its own files open. The MVP
/// deliberately does not attempt to close or kill Camera Hub automatically - the user is
/// asked to close it themselves.
/// </summary>
public static class ProcessGuard
{
    private static readonly string[] CameraHubProcessNames =
    [
        "Camera Hub",
        "CameraHub",
        "Elgato Camera Hub",
    ];

    /// <summary>Returns true if a process matching a known Camera Hub name is running.</summary>
    public static bool IsCameraHubRunning()
    {
        foreach (var name in CameraHubProcessNames)
        {
            try
            {
                if (Process.GetProcessesByName(name).Length > 0)
                {
                    return true;
                }
            }
            catch
            {
                // Process enumeration can fail in restricted environments (e.g. some
                // containers/CI sandboxes). Treat as "unknown" rather than crash the
                // caller; callers that need writes to be conservative should not rely on
                // this being a complete guarantee.
            }
        }

        return false;
    }
}
