using Prompter.Core.Backup;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter backup [--output &lt;dir&gt;]</c>
/// Creates a timestamped zip backup of the local library. Independent from the internal
/// pre-write backups prompter takes before every Camera Hub push.
/// </summary>
public static class BackupCommand
{
    public const string Usage = "backup [--output <dir>]";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var output = args.Get("output");
        var path = LibraryBackup.Create(ctx.HomeDirectory, output);
        ctx.Console.Out.WriteLine($"Backed up local library to {path}");
        return ExitCodes.Success;
    }
}
