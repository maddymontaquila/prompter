namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter rename (--id &lt;id&gt; | --name &lt;name&gt;) --to &lt;new-name&gt;</c>
/// Renames a script in place. The script's id (and therefore its Camera Hub identity, once
/// pushed) never changes.
/// </summary>
public static class RenameCommand
{
    public const string Usage = "rename (--id <id> | --name <name>) --to <new-name>";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var (script, exitCode) = ctx.ResolveScript(args);
        if (script is null) return exitCode!.Value;

        var newName = args.Get("to");
        if (string.IsNullOrWhiteSpace(newName))
        {
            ctx.Console.Error.WriteLine("Missing required option --to <new-name>.");
            return ExitCodes.UsageError;
        }

        var oldName = script.Name;
        ctx.Library.Save(script.WithName(newName));
        ctx.Console.Out.WriteLine($"Renamed '{oldName}' -> '{newName}' ({script.Id}).");
        return ExitCodes.Success;
    }
}
