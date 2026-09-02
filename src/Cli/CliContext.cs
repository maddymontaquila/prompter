using System;
using System.IO;
using System.Linq;
using Prompter.Core;
using Prompter.Core.CameraHub;

namespace Prompter.Cli;

/// <summary>Everything a CLI command needs: resolved storage locations plus I/O.</summary>
public sealed class CliContext
{
    public required LocalLibrary Library { get; init; }
    public required string HomeDirectory { get; init; }
    public required string CameraHubDirectory { get; init; }
    public required CameraHubStore Hub { get; init; }
    public required IConsole Console { get; init; }

    public static CliContext Create(string? homeOverride, string? cameraHubDirOverride, IConsole console)
    {
        var home = PathsConfig.ResolveHome(homeOverride);
        var cameraHubDir = PathsConfig.ResolveCameraHubDirectory(cameraHubDirOverride);
        var library = new LocalLibrary(home);
        var backupsRoot = Path.Combine(home, "backups", "camera-hub");
        var hub = new CameraHubStore(cameraHubDir, backupsRoot);
        return new CliContext
        {
            Library = library,
            HomeDirectory = home,
            CameraHubDirectory = cameraHubDir,
            Hub = hub,
            Console = console,
        };
    }

    /// <summary>
    /// Resolves a script from <c>--id</c> or <c>--name</c>. Writes a diagnostic and returns
    /// null (with an exit code) if the arguments are unusable, the script cannot be found,
    /// or a name matches more than one script.
    /// </summary>
    public (ScriptRecord? Script, int? ExitCode) ResolveScript(ParsedArgs args)
    {
        var idText = args.Get("id");
        var name = args.Get("name");

        if (idText is null && name is null)
        {
            Console.Error.WriteLine("Provide --id <guid> or --name <script name> to select a script.");
            return (null, ExitCodes.UsageError);
        }

        if (idText is not null && name is not null)
        {
            Console.Error.WriteLine("Provide only one of --id or --name, not both.");
            return (null, ExitCodes.UsageError);
        }

        if (idText is not null)
        {
            if (!Guid.TryParse(idText, out var id))
            {
                Console.Error.WriteLine($"'{idText}' is not a valid script id (expected a GUID).");
                return (null, ExitCodes.UsageError);
            }

            var byId = Library.Get(id);
            if (byId is null)
            {
                Console.Error.WriteLine($"No script found with id '{id}'.");
                return (null, ExitCodes.NotFound);
            }

            return (byId, null);
        }

        var matches = Library.FindByName(name!);
        if (matches.Count == 0)
        {
            Console.Error.WriteLine($"No script found with name '{name}'.");
            return (null, ExitCodes.NotFound);
        }

        if (matches.Count > 1)
        {
            Console.Error.WriteLine(
                $"Multiple scripts are named '{name}'. Re-run with --id to disambiguate: " +
                string.Join(", ", matches.Select(m => m.Id.ToString())));
            return (null, ExitCodes.Conflict);
        }

        return (matches[0], null);
    }
}
