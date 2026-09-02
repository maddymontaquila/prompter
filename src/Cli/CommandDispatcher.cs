using System;
using System.Collections.Generic;
using Prompter.Cli.Commands;

namespace Prompter.Cli;

/// <summary>
/// Routes a parsed command line to the right command handler. Global options
/// (<c>--home</c>, <c>--camera-hub-dir</c>) are accepted before or after the command name and
/// are consumed here rather than duplicated in every command.
/// </summary>
public static class CommandDispatcher
{
    private static readonly IReadOnlyDictionary<string, (string Usage, Func<CliContext, ParsedArgs, int> Run)> Commands =
        new Dictionary<string, (string, Func<CliContext, ParsedArgs, int>)>(StringComparer.OrdinalIgnoreCase)
        {
            ["create"] = (CreateCommand.Usage, CreateCommand.Run),
            ["update"] = (UpdateCommand.Usage, UpdateCommand.Run),
            ["list"] = (ListCommand.Usage, ListCommand.Run),
            ["show"] = (ShowCommand.Usage, ShowCommand.Run),
            ["export"] = (ExportCommand.Usage, ExportCommand.Run),
            ["push"] = (PushCommand.Usage, PushCommand.Run),
            ["pull"] = (PullCommand.Usage, PullCommand.Run),
            ["rename"] = (RenameCommand.Usage, RenameCommand.Run),
            ["delete"] = (DeleteCommand.Usage, DeleteCommand.Run),
            ["move"] = (MoveCommand.Usage, MoveCommand.Run),
            ["doctor"] = (DoctorCommand.Usage, DoctorCommand.Run),
            ["backup"] = (BackupCommand.Usage, BackupCommand.Run),
            ["restore"] = (RestoreCommand.Usage, RestoreCommand.Run),
        };

    public static int Run(string[] rawArgs, IConsole console)
    {
        if (rawArgs.Length == 0)
        {
            return int.MinValue; // sentinel: caller should launch the TUI instead.
        }

        var commandName = rawArgs[0];

        if (commandName is "-h" or "--help" or "help")
        {
            PrintHelp(console);
            return ExitCodes.Success;
        }

        if (commandName is "--version")
        {
            console.Out.WriteLine(typeof(CommandDispatcher).Assembly.GetName().Version?.ToString() ?? "0.0.0");
            return ExitCodes.Success;
        }

        if (!Commands.TryGetValue(commandName, out var command))
        {
            console.Error.WriteLine($"Unknown command '{commandName}'.");
            PrintHelp(console);
            return ExitCodes.UsageError;
        }

        var args = ParsedArgs.Parse(rawArgs[1..]);

        if (args.Flag("help"))
        {
            console.Out.WriteLine("Usage: prompter " + command.Usage);
            return ExitCodes.Success;
        }

        var ctx = CliContext.Create(args.Get("home"), args.Get("camera-hub-dir"), console);

        try
        {
            return command.Run(ctx, args);
        }
        catch (Exception ex)
        {
            console.Error.WriteLine($"Unexpected error: {ex.Message}");
            return ExitCodes.GeneralError;
        }
    }

    public static void PrintHelp(IConsole console)
    {
        var w = console.Out;
        w.WriteLine("prompter - a deterministic, local-only teleprompter script manager for Elgato Camera Hub");
        w.WriteLine();
        w.WriteLine("Usage: prompter [command] [options]");
        w.WriteLine();
        w.WriteLine("Run with no command to open the interactive TUI.");
        w.WriteLine();
        w.WriteLine("Commands:");
        foreach (var (name, (usage, _)) in Commands)
        {
            w.WriteLine($"  {usage}");
        }
        w.WriteLine();
        w.WriteLine("Global options:");
        w.WriteLine("  --home <dir>            Override the local library home directory (or set PROMPTER_HOME).");
        w.WriteLine("  --camera-hub-dir <dir>  Override the Camera Hub data directory (or set PROMPTER_CAMERA_HUB_DIR).");
        w.WriteLine("  --json                  Where supported, emit machine-readable JSON instead of text.");
        w.WriteLine();
        w.WriteLine("Run 'prompter <command> --help' for command-specific usage.");
    }
}
