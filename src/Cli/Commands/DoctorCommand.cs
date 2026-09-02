using System.IO;
using Prompter.Core.CameraHub;

namespace Prompter.Cli.Commands;

/// <summary>
/// <c>prompter doctor</c>
/// Prints a diagnostic summary of prompter's environment: local library health, and Camera
/// Hub discovery/schema/process status. Exits non-zero if anything looks unsafe.
/// </summary>
public static class DoctorCommand
{
    public const string Usage = "doctor";

    public static int Run(CliContext ctx, ParsedArgs args)
    {
        var ok = true;
        var w = ctx.Console.Out;

        w.WriteLine("prompter doctor");
        w.WriteLine("----------------");
        w.WriteLine($"Home directory:       {ctx.HomeDirectory}");
        w.WriteLine($"  exists:             {Directory.Exists(ctx.HomeDirectory)}");

        var loadResult = ctx.Library.Load();
        w.WriteLine($"  scripts:            {loadResult.Scripts.Count}");
        if (loadResult.Errors.Count > 0)
        {
            ok = false;
            w.WriteLine($"  warnings:           {loadResult.Errors.Count}");
            foreach (var error in loadResult.Errors)
            {
                w.WriteLine($"    - {error.Path}: {error.Reason}");
            }
        }

        w.WriteLine();
        w.WriteLine($"Camera Hub directory: {ctx.CameraHubDirectory}");
        var hubExists = Directory.Exists(ctx.CameraHubDirectory);
        w.WriteLine($"  exists:             {hubExists}");

        var running = ProcessGuard.IsCameraHubRunning();
        w.WriteLine($"  process running:    {running}");
        if (running)
        {
            w.WriteLine("    note: close Camera Hub before running 'push'.");
        }

        if (hubExists)
        {
            var read = ctx.Hub.ReadAll();
            if (!read.Success)
            {
                ok = false;
                w.WriteLine($"  schema:             DRIFTED - {read.FatalError}");
                w.WriteLine("    push is refused until this is resolved; read-only commands remain available where safe.");
            }
            else
            {
                w.WriteLine($"  schema:             OK");
                w.WriteLine($"  entries:            {read.Entries.Count}");
                var malformed = 0;
                foreach (var entry in read.Entries)
                {
                    if (entry.Error is not null) malformed++;
                }
                if (malformed > 0)
                {
                    w.WriteLine($"  malformed entries:  {malformed}");
                }
            }
        }
        else
        {
            w.WriteLine("  schema:             n/a (directory not found; is Camera Hub installed?)");
        }

        w.WriteLine();
        w.WriteLine(ok ? "Overall: OK" : "Overall: ISSUES FOUND");
        return ok ? ExitCodes.Success : ExitCodes.GeneralError;
    }
}
