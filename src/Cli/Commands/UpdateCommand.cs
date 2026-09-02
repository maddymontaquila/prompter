using System.IO;
using Prompter.Core;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter update (--id &lt;id&gt; | --name &lt;name&gt;) [--input &lt;file&gt;]</c>
/// Replaces an existing script's body from <c>--input</c> or stdin (mirrors <c>create</c>'s
/// input handling). The id, name, and display order are preserved - only the chapters and
/// <c>updatedUtc</c> change. This is the deterministic, non-interactive way to edit a
/// script's text without going through the TUI; callers are expected to <c>show</c> the
/// script first if they want to preview/diff before overwriting.
/// </summary>
public static class UpdateCommand
{
    public const string Usage = "update (--id <id> | --name <name>) [--input <file>]  (reads stdin if --input is omitted)";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var (script, exitCode) = ctx.ResolveScript(args);
        if (script is null) return exitCode!.Value;

        var inputPath = args.Get("input");
        string text;

        if (inputPath is not null)
        {
            if (!File.Exists(inputPath))
            {
                ctx.Console.Error.WriteLine($"Input file not found: {inputPath}");
                return ExitCodes.NotFound;
            }

            text = File.ReadAllText(inputPath);
        }
        else
        {
            if (!ctx.Console.IsInputRedirected)
            {
                ctx.Console.Error.WriteLine("No --input file was given and stdin is not redirected.");
                ctx.Console.Error.WriteLine("Pipe script text into this command, or pass --input <file>.");
                ctx.Console.Error.WriteLine("Usage: " + Usage);
                return ExitCodes.UsageError;
            }

            text = ctx.Console.In.ReadToEnd();
        }

        if (string.IsNullOrWhiteSpace(text))
        {
            ctx.Console.Error.WriteLine("Script text is empty; refusing to replace the script body with nothing.");
            return ExitCodes.UsageError;
        }

        var chapters = ScriptDocument.BodyToChapters(text);
        var updated = script.WithChapters(chapters);
        ctx.Library.Save(updated);

        if (args.Flag("json"))
        {
            JsonOutput.Write(ctx.Console.Out, JsonOutput.ScriptDetail(updated));
        }
        else
        {
            ctx.Console.Out.WriteLine($"Updated script '{updated.Name}' ({updated.Id}) - now {updated.Chapters.Count} chapter(s).");
        }

        return ExitCodes.Success;
    }
}
