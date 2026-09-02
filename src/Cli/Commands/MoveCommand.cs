namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter move (--id &lt;id&gt; | --name &lt;name&gt;) --direction up|down</c>
/// Swaps a script's local display order with its neighbor. Mirrors the reorder capability
/// available in the TUI so scripted/automated reordering is possible too.
/// </summary>
public static class MoveCommand
{
    public const string Usage = "move (--id <id> | --name <name>) --direction up|down";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var (script, exitCode) = ctx.ResolveScript(args);
        if (script is null) return exitCode!.Value;

        var direction = args.Get("direction");
        if (direction is not ("up" or "down"))
        {
            ctx.Console.Error.WriteLine("Missing or invalid --direction <up|down>.");
            return ExitCodes.UsageError;
        }

        var moved = ctx.Library.Move(script.Id, direction == "up" ? -1 : 1);
        if (!moved)
        {
            var edge = direction == "up" ? "top" : "bottom";
            ctx.Console.Out.WriteLine($"'{script.Name}' is already at the {edge} of the list.");
            return ExitCodes.Success;
        }

        ctx.Console.Out.WriteLine($"Moved '{script.Name}' {direction}.");
        return ExitCodes.Success;
    }
}
