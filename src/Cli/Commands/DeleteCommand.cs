namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter delete (--id &lt;id&gt; | --name &lt;name&gt;) --yes</c>
/// Deletes a script from the local library only. Never touches Camera Hub. Requires
/// <c>--yes</c> so it can never fire accidentally from a partially-formed command line.
/// </summary>
public static class DeleteCommand
{
    public const string Usage = "delete (--id <id> | --name <name>) --yes";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var (script, exitCode) = ctx.ResolveScript(args);
        if (script is null) return exitCode!.Value;

        if (!args.Flag("yes"))
        {
            ctx.Console.Error.WriteLine($"Refusing to delete '{script.Name}' ({script.Id}) without --yes.");
            return ExitCodes.RefusedForSafety;
        }

        ctx.Library.Delete(script.Id);
        ctx.Console.Out.WriteLine($"Deleted '{script.Name}' ({script.Id}).");
        return ExitCodes.Success;
    }
}
