using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Nodes;
using Prompter.Core;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter export (--id &lt;id&gt; | --all) [--output &lt;dir&gt;] [--format md|txt]</c>
/// Writes plain-text/Markdown copies of scripts for reading, diffing, or sharing outside
/// prompter. This is a one-way local export, distinct from Camera Hub <c>push</c>/<c>pull</c>
/// synchronization. Every export also refreshes an <c>index.json</c> manifest in the output
/// directory mapping script id -&gt; exported file name, so exported files stay easy to
/// cross-reference back to their script id.
/// </summary>
public static class ExportCommand
{
    public const string Usage = "export (--id <id> | --all) [--output <dir>] [--format md|txt]";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var all = args.Flag("all");
        var idText = args.Get("id");

        if (all == (idText is not null))
        {
            ctx.Console.Error.WriteLine("Provide exactly one of --all or --id <id>.");
            return ExitCodes.UsageError;
        }

        var format = args.Get("format", "md");
        if (format is not ("md" or "txt"))
        {
            ctx.Console.Error.WriteLine($"Unsupported --format '{format}'. Use 'md' or 'txt'.");
            return ExitCodes.UsageError;
        }

        var outputDir = args.Get("output") ?? Path.Combine(Directory.GetCurrentDirectory(), "prompter-export");
        Directory.CreateDirectory(outputDir);

        List<ScriptRecord> scripts;
        if (all)
        {
            scripts = [.. ctx.Library.Load().Scripts];
        }
        else
        {
            var (script, exitCode) = ctx.ResolveScript(args);
            if (script is null) return exitCode!.Value;
            scripts = [script];
        }

        var manifestPath = Path.Combine(outputDir, "index.json");
        var manifest = File.Exists(manifestPath)
            ? (JsonNode.Parse(File.ReadAllText(manifestPath)) as JsonObject) ?? new JsonObject()
            : new JsonObject();

        var extension = "." + format;
        foreach (var script in scripts)
        {
            var fileName = FileNaming.Sanitize(script.Name) + extension;
            var path = Path.Combine(outputDir, fileName);
            File.WriteAllText(path, ScriptDocument.ChaptersToBody(script.Chapters));
            manifest[script.Id.ToString()] = new JsonObject
            {
                ["name"] = script.Name,
                ["file"] = fileName,
                ["exportedUtc"] = DateTimeOffset.UtcNow.ToString("O"),
            };
            ctx.Console.Out.WriteLine($"Exported '{script.Name}' -> {path}");
        }

        File.WriteAllText(manifestPath, manifest.ToJsonString(new System.Text.Json.JsonSerializerOptions { WriteIndented = true }));
        return ExitCodes.Success;
    }
}
