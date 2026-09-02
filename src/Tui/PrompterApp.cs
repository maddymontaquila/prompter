using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Hex1b;
using Hex1b.Events;
using Hex1b.Input;
using Hex1b.Widgets;
using Prompter.Cli;
using Prompter.Core;
using Prompter.Core.CameraHub;

namespace Prompter.Tui;

/// <summary>
/// The interactive Hex1b terminal UI. Deliberately keyboard-first and focus-stable: rather
/// than trying to programmatically move focus onto ephemeral prompts (Hex1b has no API for
/// that), all mutating operations are driven through a single always-present, always-focusable
/// command/filter <see cref="TextBoxWidget"/> using short colon-commands (":new name",
/// ":rename name", ":delete confirm", ...), plus a handful of portable global key bindings for
/// the most common actions. Non-colon text in the same box is a live name filter. This keeps
/// the whole interaction model deterministic and reasoned-about without needing a live terminal
/// to verify focus transfer at runtime.
///
/// Tab order (verified with the Hex1b.Tool headless terminal harness): command box → script
/// list → the <c>HSplitter</c> pane divider (itself a focus stop that Hex1b uses for
/// arrow-key resizing) → the multiline script editor, then wraps. The divider stop is a Hex1b
/// framework behavior, not app-specific, and is called out in the in-app status hint and README
/// so it isn't mistaken for a dead Tab press.
/// </summary>
public static class PrompterApp
{
    public static async Task<int> RunAsync(string? homeOverride, string? cameraHubDirOverride)
    {
        var cliContext = CliContext.Create(homeOverride, cameraHubDirOverride, new SystemConsole());
        var library = cliContext.Library;
        var hub = cliContext.Hub;
        library.EnsureDirectoryExists();

        var scripts = new List<ScriptRecord>();
        Guid? selectedId = null;
        Guid? pendingDeleteId = null;
        var dirty = false;
        var status = "Tab: cycle focus (list → divider → editor)  •  type to filter  •  ':' for commands (':help' lists them)";

        var commandState = new TextBoxState("");
        var editorState = new TextBoxState("");

        void Refresh()
        {
            var result = library.Load();
            scripts = result.Scripts.ToList();
            if (result.Errors.Count > 0)
            {
                status = $"{result.Errors.Count} script file(s) failed to load - run 'prompter doctor' for details.";
            }
        }

        void LoadIntoEditor(ScriptRecord? script)
        {
            selectedId = script?.Id;
            editorState.Text = script is null ? "" : ScriptDocument.ChaptersToBody(script.Chapters);
            dirty = false;
            pendingDeleteId = null;
        }

        Refresh();
        LoadIntoEditor(scripts.FirstOrDefault());

        List<ScriptRecord> Filtered()
        {
            var query = commandState.Text;
            if (string.IsNullOrEmpty(query) || query.StartsWith(':')) return scripts;
            return scripts.Where(s => s.Name.Contains(query, StringComparison.OrdinalIgnoreCase)).ToList();
        }

        ScriptRecord? Selected() => selectedId is Guid id ? scripts.FirstOrDefault(s => s.Id == id) : null;

        void Save()
        {
            var script = Selected();
            if (script is null) { status = "No script selected."; return; }
            library.Save(script.WithChapters(ScriptDocument.BodyToChapters(editorState.Text)));
            Refresh();
            dirty = false;
            status = $"Saved '{script.Name}'.";
        }

        void MoveSelected(int direction)
        {
            var script = Selected();
            if (script is null) { status = "No script selected."; return; }
            var moved = library.Move(script.Id, direction);
            status = moved ? $"Moved '{script.Name}'." : $"'{script.Name}' can't move further.";
            if (moved) Refresh();
        }

        void ExportSelected()
        {
            var script = Selected();
            if (script is null) { status = "No script selected."; return; }
            var dir = Path.Combine(cliContext.HomeDirectory, "exports");
            Directory.CreateDirectory(dir);
            var path = Path.Combine(dir, FileNaming.Sanitize(script.Name) + ".md");
            File.WriteAllText(path, ScriptDocument.ChaptersToBody(script.Chapters));
            status = $"Exported '{script.Name}' to {path}";
        }

        void Push()
        {
            var script = Selected();
            if (script is null) { status = "No script selected."; return; }
            var result = CameraHubSync.Push(hub, script);
            status = result.Success ? $"Pushed '{script.Name}' to Camera Hub." : $"Push failed: {result.Error}";
        }

        void Pull()
        {
            var summary = CameraHubSync.Pull(hub, library, PullConflictPolicy.Skip);
            if (!summary.CameraHubFound) { status = "Camera Hub data directory not found."; return; }
            if (!summary.Success) { status = $"Pull failed: {summary.FatalError}"; return; }
            Refresh();
            if (Selected() is null)
            {
                // Keep the editor pane in sync with the list, which auto-focuses its first row
                // even when nothing was selected before an import into a previously-empty library.
                LoadIntoEditor(scripts.FirstOrDefault());
            }
            var imported = summary.Outcomes.Count(o => o.Action == "imported");
            var skipped = summary.Outcomes.Count(o => o.Action == "skipped-conflict");
            var malformed = summary.Outcomes.Count(o => o.Action == "skipped-malformed");
            status = $"Pull: {imported} imported, {skipped} conflict-skipped, {malformed} malformed. " +
                      "Use the CLI 'pull --conflict overwrite' to force conflicts.";
        }

        void DeleteSelected(bool confirmed)
        {
            var script = Selected();
            if (script is null) { status = "No script selected."; return; }

            if (!confirmed)
            {
                pendingDeleteId = script.Id;
                status = $"Type ':delete confirm' or press Ctrl+D to permanently delete '{script.Name}'.";
                return;
            }

            library.Delete(script.Id);
            Refresh();
            LoadIntoEditor(scripts.FirstOrDefault());
            status = $"Deleted '{script.Name}'.";
        }

        void HandleCommand(TextSubmittedEventArgs e)
        {
            var raw = e.Text;
            commandState.Text = "";
            var text = raw.Trim();
            if (text.Length == 0 || text[0] != ':') return;

            var body = text[1..].Trim();
            var spaceIndex = body.IndexOf(' ');
            var name = spaceIndex < 0 ? body : body[..spaceIndex];
            var arg = spaceIndex < 0 ? "" : body[(spaceIndex + 1)..].Trim();

            switch (name.ToLowerInvariant())
            {
                case "new":
                    if (arg.Length == 0) { status = "Usage: :new <name>"; break; }
                    var created = library.Create(arg, new[] { "" });
                    Refresh();
                    LoadIntoEditor(scripts.FirstOrDefault(s => s.Id == created.Id));
                    status = $"Created '{created.Name}'. Type the script on the right, then :save.";
                    break;

                case "rename":
                {
                    var toRename = Selected();
                    if (toRename is null) { status = "No script selected."; break; }
                    if (arg.Length == 0) { status = "Usage: :rename <new name>"; break; }
                    library.Save(toRename.WithName(arg));
                    Refresh();
                    LoadIntoEditor(scripts.FirstOrDefault(s => s.Id == toRename.Id));
                    status = $"Renamed to '{arg}'.";
                    break;
                }

                case "delete":
                    DeleteSelected(string.Equals(arg, "confirm", StringComparison.OrdinalIgnoreCase));
                    break;

                case "save":
                    Save();
                    break;

                case "push":
                    Push();
                    break;

                case "pull":
                    Pull();
                    break;

                case "export":
                    ExportSelected();
                    break;

                case "up":
                    MoveSelected(-1);
                    break;

                case "down":
                    MoveSelected(1);
                    break;

                case "quit":
                case "q":
                    e.Context.RequestStop();
                    break;

                case "help":
                    status = ":new <name>  :rename <name>  :delete [confirm]  :save  :push  :pull  :export  :up  :down  :quit";
                    break;

                default:
                    status = $"Unknown command ':{name}'. Try ':help'.";
                    break;
            }
        }

        Hex1bWidget Build(RootContext rootCtx)
        {
            var filtered = Filtered();
            var focusIndex = 0;
            if (selectedId is Guid sid)
            {
                var idx = filtered.FindIndex(s => s.Id == sid);
                if (idx >= 0) focusIndex = idx;
            }

            var selected = Selected();

            var root = rootCtx.VStack(v =>
            [
                v.Text(" prompter — local scripts + Camera Hub sync (no AI, no network)").ContentHeight(),
                v.HSplitter(
                    leftPane =>
                    [
                        leftPane.Text(" Scripts").ContentHeight(),
                        leftPane.TextBox()
                            .State(commandState)
                            .OnSubmit(HandleCommand)
                            .ContentHeight(),
                        leftPane.List(filtered)
                            .ItemKey(s => (object)s.Id)
                            .ItemHeight(1)
                            .ItemTemplate(item => item.Text(
                                (item.IsFocused ? "› " : "  ") +
                                item.Item.Name +
                                (pendingDeleteId == item.Item.Id ? "  [armed for delete]" : "")))
                            .FocusedIndex(filtered.Count == 0 ? 0 : focusIndex)
                            .OnFocusChanged(args => LoadIntoEditor(args.FocusedItem))
                            .Fill(),
                    ],
                    rightPane =>
                    [
                        rightPane.Text(selected is null
                                ? " (no script selected — try ':new <name>')"
                                : $" {selected.Name}{(dirty ? " *" : "")}  ({selected.Chapters.Count} chapter(s))")
                            .ContentHeight(),
                        rightPane.TextBox()
                            .State(editorState)
                            .Multiline()
                            .WordWrap()
                            .OnTextChanged(_ => dirty = true)
                            .FillWidth().FillHeight(),
                    ],
                    leftWidth: 36).FillWidth().FillHeight(),
                v.InfoBar([status]).ContentHeight(),
                v.InfoBar([
                    "Ctrl+S", "Save",
                    "Ctrl+Up/Dn", "Reorder",
                    "Ctrl+P", "Push",
                    "Ctrl+L", "Pull",
                    "Ctrl+E", "Export",
                    "Del", "Arm delete",
                    "Ctrl+D", "Confirm delete",
                    "Ctrl+Q", "Quit",
                    "Tab", "Switch focus",
                    ":", "Command",
                ]).ContentHeight(),
            ]);

            return root.InputBindings(b =>
            {
                b.Ctrl().Key(Hex1bKey.S).Global().Action(Save, "Save");
                b.Ctrl().Key(Hex1bKey.UpArrow).Global().Action(() => MoveSelected(-1), "Move script up");
                b.Ctrl().Key(Hex1bKey.DownArrow).Global().Action(() => MoveSelected(1), "Move script down");
                b.Ctrl().Key(Hex1bKey.P).Global().Action(Push, "Push to Camera Hub");
                b.Ctrl().Key(Hex1bKey.L).Global().Action(Pull, "Pull from Camera Hub");
                b.Ctrl().Key(Hex1bKey.E).Global().Action(ExportSelected, "Export selected script");
                b.Key(Hex1bKey.Delete).Global().Action(() => DeleteSelected(false), "Arm delete");
                b.Ctrl().Key(Hex1bKey.D).Global().Action(() => DeleteSelected(true), "Confirm delete");
                b.Ctrl().Key(Hex1bKey.Q).Global().Action(
                    (InputBindingActionContext actionCtx) => actionCtx.RequestStop(),
                    "Quit");
            });
        }

        await using var terminal = Hex1bTerminal.CreateBuilder()
            .WithHex1bApp(Build)
            .Build();

        await terminal.RunAsync();

        return ExitCodes.Success;
    }
}
