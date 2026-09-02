#:package Hex1b@0.165.0
#:property PublishAot=true
#:property PackAsTool=true
#:property ToolCommandName=prompter
#:property Version=0.1.0
#:property Authors=Prompter Contributors
#:property Description=A deterministic, local-only teleprompter script manager for Elgato Camera Hub.
#:property PackageLicenseExpression=MIT
#:property NoWarn=CA2266
#:include src/Core/**/*.cs
#:include src/Cli/**/*.cs
#:include src/Tui/**/*.cs

using Prompter.Cli;
using Prompter.Tui;

var console = new SystemConsole();
var result = CommandDispatcher.Run(args, console);

if (result == int.MinValue)
{
    // No command was given: launch the interactive TUI instead of the deterministic CLI.
    // The TUI resolves storage locations the same way the CLI does (PROMPTER_HOME /
    // PROMPTER_CAMERA_HUB_DIR env vars, OS default app-data paths); there is no
    // interactive-only override since the TUI is only reachable with zero arguments.
    return await PrompterApp.RunAsync(homeOverride: null, cameraHubDirOverride: null);
}

return result;
