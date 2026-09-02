using Prompter.Core;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter show (--id &lt;id&gt; | --name &lt;name&gt;) [--json]</c>
/// Prints one script's full body text (or full JSON record with chapters).
/// </summary>
public static class ShowCommand
{
    public const string Usage = "show (--id <id> | --name <name>) [--json]";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var (script, exitCode) = ctx.ResolveScript(args);
        if (script is null)
        {
            return exitCode!.Value;
        }

        if (args.Flag("json"))
        {
            JsonOutput.Write(ctx.Console.Out, JsonOutput.ScriptDetail(script));
        }
        else
        {
            ctx.Console.Out.WriteLine($"# {script.Name}");
            ctx.Console.Out.WriteLine($"id: {script.Id}");
            ctx.Console.Out.WriteLine();
            ctx.Console.Out.WriteLine(ScriptDocument.ChaptersToBody(script.Chapters));
        }

        return ExitCodes.Success;
    }
}
