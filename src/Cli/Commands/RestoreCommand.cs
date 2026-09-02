using System;
using System.IO;
using Prompter.Core.Backup;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter restore --file &lt;backup.zip&gt; --yes</c>
/// Restores the local library from a zip previously produced by <c>backup</c>, replacing
/// the current library contents. Requires <c>--yes</c> since it is destructive to the
/// current local library state.
/// </summary>
public static class RestoreCommand
{
    public const string Usage = "restore --file <backup.zip> --yes";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var file = args.Get("file");
        if (string.IsNullOrWhiteSpace(file))
        {
            ctx.Console.Error.WriteLine("Missing required option --file <backup.zip>.");
            return ExitCodes.UsageError;
        }

        if (!File.Exists(file))
        {
            ctx.Console.Error.WriteLine($"Backup file not found: {file}");
            return ExitCodes.NotFound;
        }

        if (!args.Flag("yes"))
        {
            ctx.Console.Error.WriteLine("Refusing to restore (this replaces the current local library) without --yes.");
            return ExitCodes.RefusedForSafety;
        }

        try
        {
            LibraryBackup.Restore(ctx.HomeDirectory, file);
        }
        catch (InvalidDataException ex)
        {
            ctx.Console.Error.WriteLine($"Refusing to restore: {ex.Message}");
            return ExitCodes.RefusedForSafety;
        }

        ctx.Console.Out.WriteLine($"Restored local library from {file}");
        return ExitCodes.Success;
    }
}
