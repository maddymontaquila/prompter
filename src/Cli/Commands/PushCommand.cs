using Prompter.Core.CameraHub;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter push (--id &lt;id&gt; | --name &lt;name&gt;)</c>
/// Pushes exactly one local script to Camera Hub (create-or-update). Refuses if Camera Hub
/// is running, its data directory is missing, or its on-disk schema has drifted from what
/// prompter understands - see <see cref="Prompter.Core.CameraHub.CameraHubStore"/>.
/// </summary>
public static class PushCommand
{
    public const string Usage = "push (--id <id> | --name <name>)";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var (script, exitCode) = ctx.ResolveScript(args);
        if (script is null) return exitCode!.Value;

        var result = CameraHubSync.Push(ctx.Hub, script);
        if (!result.Success)
        {
            ctx.Console.Error.WriteLine($"Push failed: {result.Error}");
            return ExitCodes.RefusedForSafety;
        }

        ctx.Console.Out.WriteLine($"Pushed '{script.Name}' ({script.Id}) to Camera Hub.");
        if (result.BackupDirectory is not null)
        {
            ctx.Console.Out.WriteLine($"Pre-write backup: {result.BackupDirectory}");
        }

        return ExitCodes.Success;
    }
}
