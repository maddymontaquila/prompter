using System;
using System.Text.Json.Nodes;
using Prompter.Core.CameraHub;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter pull (--id &lt;camera-hub-guid&gt; | --all) [--conflict skip|overwrite]</c>
/// Pulls Camera Hub scripts into the local library. Defaults to <c>--conflict skip</c>: if a
/// local script already exists with the same id, it is left untouched unless the caller
/// explicitly asks to overwrite it. This is a Camera Hub synchronization operation, distinct
/// from local export/import.
/// </summary>
public static class PullCommand
{
    public const string Usage = "pull (--id <camera-hub-guid> | --all) [--conflict skip|overwrite]";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var all = args.Flag("all");
        var idText = args.Get("id");

        if (all == (idText is not null))
        {
            ctx.Console.Error.WriteLine("Provide exactly one of --all or --id <camera-hub-guid>.");
            return ExitCodes.UsageError;
        }

        Guid? onlyId = null;
        if (idText is not null)
        {
            if (!Guid.TryParse(idText, out var parsed))
            {
                ctx.Console.Error.WriteLine($"'{idText}' is not a valid Camera Hub id (expected a GUID).");
                return ExitCodes.UsageError;
            }
            onlyId = parsed;
        }

        var conflictText = args.Get("conflict", "skip");
        if (conflictText is not ("skip" or "overwrite"))
        {
            ctx.Console.Error.WriteLine($"Unsupported --conflict '{conflictText}'. Use 'skip' or 'overwrite'.");
            return ExitCodes.UsageError;
        }

        var policy = conflictText == "overwrite" ? PullConflictPolicy.Overwrite : PullConflictPolicy.Skip;
        var summary = CameraHubSync.Pull(ctx.Hub, ctx.Library, policy, onlyId);

        if (!summary.CameraHubFound)
        {
            ctx.Console.Error.WriteLine($"Camera Hub data directory not found at '{ctx.CameraHubDirectory}'. Is Camera Hub installed?");
            return ExitCodes.NotFound;
        }

        if (!summary.Success)
        {
            ctx.Console.Error.WriteLine($"Pull failed: {summary.FatalError}");
            return ExitCodes.RefusedForSafety;
        }

        if (args.Flag("json"))
        {
            var array = new JsonArray();
            foreach (var outcome in summary.Outcomes)
            {
                array.Add((JsonNode?)new JsonObject
                {
                    ["id"] = outcome.Id.ToString(),
                    ["action"] = outcome.Action,
                    ["detail"] = outcome.Detail,
                });
            }
            JsonOutput.Write(ctx.Console.Out, new JsonObject { ["results"] = array });
            return ExitCodes.Success;
        }

        if (summary.Outcomes.Count == 0)
        {
            ctx.Console.Out.WriteLine("Nothing to pull.");
            return ExitCodes.Success;
        }

        foreach (var outcome in summary.Outcomes)
        {
            ctx.Console.Out.WriteLine($"{outcome.Id}: {outcome.Action}" + (outcome.Detail is null ? "" : $" ({outcome.Detail})"));
        }

        return ExitCodes.Success;
    }
}
