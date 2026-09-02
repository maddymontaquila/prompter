# prompter

A streamlined, deterministic, local-only teleprompter script manager for [Elgato Camera
Hub](https://www.elgato.com/camera-hub), built as a .NET 10 file-based app with the
[Hex1b](https://github.com/mitchdenny/hex1b) TUI framework.

`prompter` is a spiritual alternative to
[snapsynapse/prompter-kit](https://github.com/snapsynapse/prompter-kit) (MIT licensed).
It reimplements the same idea - a friendlier way to author and manage Camera Hub
teleprompter scripts - against the documented Camera Hub on-disk schema, but as a small,
dependency-light .NET tool instead of a Python app. Behavior was reimplemented from
prompter-kit's documented data model, not ported line-for-line.

## What prompter is (and is not)

- **No AI. No speech recognition. No network calls.** Everything prompter does is
  local, deterministic, and offline: reading/writing files on disk.
- prompter does **not** transcribe voice memos or rewrite scripts for you. It expects
  already-transcribed, already-polished script text - typically produced by a coding
  agent or by you, by hand. See [`.github/skills/prompter/SKILL.md`](.github/skills/prompter/SKILL.md)
  for how a coding agent is expected to turn a raw transcript into a script and drive
  this CLI safely.
- The **local library is canonical**. Camera Hub push/pull is an explicit synchronization
  step, never an implicit side effect of exporting or editing.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download) (10.0.300 or later - needed for
  file-based app `#:include` support).

## Running it

`prompter.cs` is a genuine **file-based app** - there is no `.csproj`. The entry point
is the single file `prompter.cs`, which pulls in the rest of the source under `src/`
via `#:include` directives.

```powershell
# Run directly, no build/install step
dotnet run prompter.cs

# Pass CLI arguments after --
dotnet run prompter.cs -- list --json

# Or build it once
dotnet build prompter.cs
```

Running `prompter` (or `dotnet run prompter.cs`) with **no arguments** opens the
interactive Hex1b TUI. Any recognized subcommand runs deterministically and exits
without ever starting the TUI, so it's safe for scripts, CI, and coding agents.

## Installing as a global tool

```powershell
# Package it - PackAsTool is on by default for file-based apps
dotnet pack prompter.cs -o ./nupkg

# Install from the local package
dotnet tool install --global prompter --add-source ./nupkg

# Now available anywhere
prompter list
```

To try it without touching your global tool list, install into an isolated tool path:

```powershell
dotnet tool install --tool-path ./tools-tmp prompter --add-source ./nupkg
./tools-tmp/prompter --help
```

## The interactive TUI

Launch with no arguments. A single always-focusable command box doubles as a live
filter (type to filter the script list) and a colon-command line (`:` followed by a
command name). The keybinding hints in the status bar are also shown in-app:

| Action                       | Keys                                  |
|-------------------------------|----------------------------------------|
| Cycle focus (command box → list → pane divider → editor) | `Tab` / `Shift+Tab` |
| Resize the list/editor split (while the divider has focus) | `Left`/`Right` arrows |
| Filter the script list         | Type in the command box               |
| Save the selected script       | `Ctrl+S`                              |
| Reorder selected script        | `Ctrl+Up` / `Ctrl+Down`                |
| Push selected script to Camera Hub | `Ctrl+P`                          |
| Pull all Camera Hub scripts    | `Ctrl+L`                              |
| Export selected script         | `Ctrl+E`                              |
| Arm delete for selected script | `Delete`                               |
| Confirm delete                 | `Ctrl+D`                              |
| Quit                            | `Ctrl+Q` or `:q` / `:quit`            |

Colon commands (`:new <name>`, `:rename <name>`, `:delete`, `:push`, `:pull`, `:export`,
`:quit`) are available from the command box for anything not bound to a key. The
terminal size is handled gracefully - narrow terminals collapse to a single-column
layout instead of clipping content.

## Deterministic CLI commands

All commands are safe for scripts, CI, and coding agents: they never require the TUI,
always return a meaningful [exit code](src/Cli/ExitCodes.cs), and print errors to
stderr.

```
prompter create --name <name> [--input <file>]   # reads stdin if --input is omitted
prompter update (--id <id> | --name <name>) [--input <file>]  # replaces body; reads stdin if --input is omitted
prompter list [--json]
prompter show (--id <id> | --name <name>) [--json]
prompter export (--id <id> | --all) [--output <dir>] [--format md|txt]
prompter push (--id <id> | --name <name>)
prompter pull (--id <camera-hub-guid> | --all) [--conflict skip|overwrite]
prompter rename (--id <id> | --name <name>) --to <new-name>
prompter delete (--id <id> | --name <name>) --yes
prompter move (--id <id> | --name <name>) --direction up|down
prompter doctor
prompter backup [--output <dir>]
prompter restore --file <backup.zip> --yes
```

Global options (after the subcommand): `--home <dir>`, `--camera-hub-dir <dir>`,
`--json` (where supported).

Examples:

```powershell
# Create a script from stdin (e.g. piped from a coding agent)
Get-Content transcript.txt | prompter create --name "Product Demo"

# Or from a file
prompter create --name "Product Demo" --input ./demo-script.txt

# Machine-readable listing
prompter list --json

# Export everything to Markdown for review/version control
prompter export --all --output ./exported-scripts
```

## Storage locations

- **Local library** (canonical): an OS app-data directory, by default:
  - Windows: `%LOCALAPPDATA%\Prompter\library\`
  - macOS: `~/Library/Application Support/Prompter/library/`
  - Linux: `$XDG_DATA_HOME/Prompter/library/` (falls back to `~/.local/share/Prompter/library/`)

  Override the whole home directory with the `PROMPTER_HOME` environment variable or
  `--home <dir>` (useful for tests and for keeping multiple libraries). Each script is
  one plain-text `.md` file with a small frontmatter block (`id`, `name`, `order`,
  timestamps) followed by the script body, so the library is easy to diff, grep,
  hand-edit, and put under version control. The **id** in the frontmatter is the
  stable identity used for renames and Camera Hub push/pull - renaming a script never
  changes its id, only its file name.

- **Camera Hub data** (synced via `push`/`pull`, never treated as canonical):
  - Windows: `%APPDATA%\Elgato\Camera Hub\`
  - macOS: `~/Library/Application Support/Elgato/Camera Hub\`
  - A legacy `CameraHub` (no space) directory is used as a fallback only when the
    normal directory doesn't exist.

  Override with `PROMPTER_CAMERA_HUB_DIR` or `--camera-hub-dir <dir>`.

## Camera Hub push/pull caveats

Camera Hub push writes are deliberately conservative:

- **Refuses if Camera Hub is running.** prompter does not try to close or kill Camera
  Hub for you - close it yourself, then push.
- **Refuses on schema drift.** If `AppSettings.json`'s `applogic.prompter.libraryList`
  or a `Texts/<GUID>.json` file doesn't match the shape prompter understands, it
  refuses to write rather than guessing or clobbering unknown data.
- **Takes a timestamped backup before every write**, with retention of the most recent
  backups, verifies the write by re-reading it, and rolls back every touched file if
  anything fails partway through.
- Read-only operations (`list`, `pull`, `export`, `backup`) continue past individual
  malformed Camera Hub records instead of aborting the whole operation - but malformed
  or missing records are always reported, never silently dropped.

Pulling from Camera Hub is **not** the same as exporting. The local library is
canonical for authored scripts; pulling a Camera Hub script that already exists
locally defaults to `--conflict skip` (refuses to overwrite) unless you explicitly pass
`--conflict overwrite`.

## Agent workflow

See [`.github/skills/prompter/SKILL.md`](.github/skills/prompter/SKILL.md) for the full
coding-agent skill: how an agent should turn a raw voice-memo transcript into a script,
and how to drive this CLI safely (preview before overwrite, never push/delete/restore
without explicit approval).

## Development

```powershell
# Run the deterministic CLI help path
dotnet run prompter.cs -- --help

# Run the pure-logic test suite (never touches real Camera Hub data)
dotnet test tests/Prompter.Core.Tests

# Pack as a tool
dotnet pack prompter.cs -o ./nupkg
```

Tests live in `tests/Prompter.Core.Tests` and compile the pure `src/Core` domain/storage
layer directly (it has no dependency on Hex1b or the CLI/TUI layers), using disposable
temp-directory fixtures - they never read or write a real Camera Hub installation or a
real `PROMPTER_HOME`.

### Native AOT

`prompter.cs` keeps the file-based app default of `PublishAot=true`. Camera Hub JSON
handling uses manually-built `JsonObject`/`JsonArray` trees (never reflection-based
`JsonSerializer.Serialize<T>`) specifically to stay AOT/trim friendly.

## License

MIT - see [LICENSE](LICENSE). Reimplements ideas from
[snapsynapse/prompter-kit](https://github.com/snapsynapse/prompter-kit) (also MIT) based
on its documented Camera Hub schema; no code was copied from that project.
