using System;
using System.IO;
using System.Text.Json.Nodes;
using Prompter.Core;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter create --name &lt;name&gt; [--input &lt;file&gt;]</c>
/// Reads script text from <c>--input</c> or, when omitted, from stdin. Exactly one of the
/// two must be usable - this command never prompts interactively, so it stays safe for
/// coding agents and shell pipelines.
/// </summary>
public static class CreateCommand
{
    public const string Usage = "create --name <name> [--input <file>]  (reads stdin if --input is omitted)";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var name = args.Get("name");
        if (string.IsNullOrWhiteSpace(name))
        {
            ctx.Console.Error.WriteLine("Missing required option --name.");
            ctx.Console.Error.WriteLine("Usage: " + Usage);
            return ExitCodes.UsageError;
        }

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
            ctx.Console.Error.WriteLine("Script text is empty; refusing to create an empty script.");
            return ExitCodes.UsageError;
        }

        var chapters = ScriptDocument.BodyToChapters(text);
        var script = ctx.Library.Create(name, chapters);

        if (args.Flag("json"))
        {
            JsonOutput.Write(ctx.Console.Out, JsonOutput.ScriptDetail(script));
        }
        else
        {
            ctx.Console.Out.WriteLine($"Created script '{script.Name}' ({script.Id}) with {script.Chapters.Count} chapter(s).");
        }

        return ExitCodes.Success;
    }
}
