using System.Linq;
using System.Text.Json.Nodes;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter list [--json]</c>
/// Lists local scripts in display order. Always machine-parseable with <c>--json</c>;
/// otherwise a simple aligned text table.
/// </summary>
public static class ListCommand
{
    public const string Usage = "list [--json]";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var result = ctx.Library.Load();

        foreach (var error in result.Errors)
        {
            ctx.Console.Error.WriteLine($"warning: {error.Path}: {error.Reason}");
        }

        if (args.Flag("json"))
        {
            var array = new JsonArray();
            foreach (var script in result.Scripts)
            {
                array.Add((JsonNode?)JsonOutput.ScriptSummary(script));
            }

            var root = new JsonObject
            {
                ["scripts"] = array,
                ["warningCount"] = result.Errors.Count,
            };
            JsonOutput.Write(ctx.Console.Out, root);
            return ExitCodes.Success;
        }

        if (result.Scripts.Count == 0)
        {
            ctx.Console.Out.WriteLine("No scripts yet. Create one with: prompter create --name <name>");
            return ExitCodes.Success;
        }

        var idWidth = result.Scripts.Max(s => s.Id.ToString().Length);
        foreach (var script in result.Scripts)
        {
            ctx.Console.Out.WriteLine($"{script.Id.ToString().PadRight(idWidth)}  order={script.Order,-4}  chapters={script.Chapters.Count,-3}  {script.Name}");
        }

        return ExitCodes.Success;
    }
}
