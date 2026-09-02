---
name: prompter
description: "Turn a raw voice-memo transcript into a polished teleprompter script and manage it with the `prompter` CLI. Use whenever the user wants to draft, update, or organize a script for Elgato Camera Hub's teleprompter, or mentions 'voice memo', 'transcript', 'teleprompter script', 'camera hub script', or the `prompter` tool. prompter itself does no transcription, rewriting, AI, or network calls - it is a deterministic local script manager; the agent supplies the polished text."
---

# /prompter — Voice Memo to Teleprompter Script

`prompter` is a deterministic, offline CLI/TUI for managing local teleprompter scripts
and syncing them with Elgato Camera Hub. **It does not transcribe audio and it does not
rewrite text.** Both of those jobs belong to you (the agent), using whatever
transcription capability you have available (e.g. an attached audio-transcription tool,
a transcript the user pastes in, or a file the user points you to). Never imply that
`prompter` itself performed transcription or rewriting - it only stores and syncs
whatever text you give it.

## Core principles

- **Preserve facts, cut noise.** Keep every concrete claim, number, product name, and
  decision from the source transcript. Remove filler words ("um", "uh", "like", "you
  know"), false starts, and verbatim repetition - but never remove or alter substantive
  content to make the script "sound better".
- **Ask, don't guess, when material is genuinely ambiguous.** If the transcript is
  self-contradictory, missing a clearly-referenced step, or the speaker trails off
  mid-thought in a way that changes meaning, ask the user for clarification instead of
  inventing content. Small clean-up (removing "um", joining a sentence that was
  interrupted and resumed) does not need confirmation.
- **The local library is canonical.** `prompter create`/`update` only ever touch the
  local library. Camera Hub is a separate, explicit sync target - never push to it, and
  never run `delete` or `restore`, without the user explicitly asking you to in this
  turn. Read-only Camera Hub operations (`pull`, `doctor`) are fine to run to *inspect*
  state, but importing/overwriting local content from a pull still needs the same care
  as any other overwrite (see "Preview before overwriting" below).
- **Always preview before overwriting.** Before updating an existing script, run
  `prompter show` to see its current content and diff it (mentally or literally) against
  your proposed new version. Show the user (or at least yourself, in your own
  reasoning) what will change before you write it.
- **Use the deterministic CLI, never assume the TUI.** All of the operations below work
  headlessly and return real exit codes - drive `prompter` the same way you'd drive any
  other CLI tool.

## Workflow

### 1. Obtain the transcript

Get the raw transcript using whatever capability you have available: a transcription
tool/skill, a file the user provides, or text pasted directly into the conversation.
`prompter` plays no part in this step.

### 2. Rewrite it into a script

Turn the raw transcript into a polished script, then structure it for the teleprompter:

- **One chapter per blank-line-separated block.** `prompter` (and Camera Hub) split a
  script body into chapters on blank lines; a single newline within a block is just a
  soft line break, not a new chapter. Chapter breaks are a good place for a natural
  pause, topic change, or "beat" in a demo.
- **Call out stage directions/demo beats explicitly** in brackets on their own line, so
  the presenter can tell talking points apart from actions, e.g.:

  ```
  Today I want to show you how fast our search really is.

  [Switch to the browser and open the search page]

  Watch what happens when I type a query with a typo...

  [Type "reactjs hoosk" and pause]

  See how it still finds the right result? That's the fuzzy matching
  I mentioned earlier.
  ```

- Keep sentences short and speakable out loud - written-for-reading prose often reads
  awkwardly off a teleprompter. Read it back mentally as if speaking it.
- Preserve the speaker's own phrasing and voice where it's already clear; only smooth
  out grammar, filler, and repetition.

### 3. Discover existing scripts before creating a duplicate

```powershell
prompter list --json
```

Use `--json` for reliable machine parsing - it's the stable, scriptable form. Match by
name (or ask the user which script they mean) before deciding whether this is a new
script or an update to an existing one.

### 4. Create or update through the CLI - never hand-edit library files directly

Create a new script, piping the polished text via stdin (preferred - no temp file
needed):

```powershell
"<polished script text>" | prompter create --name "Product Demo - Q3 Launch"
```

Or from a file, if the text is already saved somewhere:

```powershell
prompter create --name "Product Demo - Q3 Launch" --input ./demo-script.txt
```

To update an existing script, first preview its current content:

```powershell
prompter show --name "Product Demo - Q3 Launch"
```

Then decide whether the new text is different enough to warrant an overwrite. If so,
tell the user (or summarize for yourself) what will change, then replace the body via
stdin or a file, same as `create`:

```powershell
"<revised script text>" | prompter update --name "Product Demo - Q3 Launch"
```

`update` preserves the script's id, name, and display order - only the chapters (and
`updatedUtc`) change. **Never run `update` without having shown a preview of the
change first** - if the diff is substantial, summarize it for the user before writing.

Other useful read-only/organizational commands:

```powershell
prompter show --name "Product Demo - Q3 Launch" --json   # structured detail incl. chapters
prompter export --all --output ./exported-scripts         # Markdown export for review/diffing
prompter rename --name "Old Name" --to "New Name"
prompter move --name "Product Demo - Q3 Launch" --direction up
prompter doctor                                            # sanity-check storage locations
```

### 5. Camera Hub sync - only with explicit approval

`push`, `pull`, `delete`, and `restore` all mutate state outside the current script
you're drafting (Camera Hub's own files, or the whole local library). **Only run these
when the user has explicitly asked for that specific action in the current
conversation turn** - never as an automatic follow-up to creating or editing a script.

```powershell
# Only after the user explicitly asks to sync to Camera Hub:
prompter push --name "Product Demo - Q3 Launch"

# Only after the user explicitly asks to pull Camera Hub scripts in:
prompter pull --all --conflict skip       # default: never overwrites local content
prompter pull --all --conflict overwrite  # only if the user explicitly wants Camera Hub's version to win

# Only after the user explicitly asks to delete or restore:
prompter delete --name "Old Draft" --yes
prompter restore --file ./backups/prompter-library-20250101-120000.zip --yes
```

If `push` refuses because Camera Hub is running, tell the user to close Camera Hub
themselves - do not attempt to close or kill it for them.

## Quick reference

| Goal | Command |
|---|---|
| Find scripts (scriptable) | `prompter list --json` |
| Read a script before editing | `prompter show --name "<name>" --json` |
| Create from stdin | `<text> \| prompter create --name "<name>"` |
| Create from a file | `prompter create --name "<name>" --input <file>` |
| Update an existing script's body (preview first) | `<text> \| prompter update --name "<name>"` |
| Export for review/version control | `prompter export --all --output <dir>` |
| Rename (keeps identity) | `prompter rename --name "<old>" --to "<new>"` |
| Reorder | `prompter move --name "<name>" --direction up\|down` |
| Push to Camera Hub (explicit approval only) | `prompter push --name "<name>"` |
| Pull from Camera Hub (explicit approval only) | `prompter pull --all --conflict skip` |
| Delete (explicit approval only) | `prompter delete --name "<name>" --yes` |
| Restore a backup (explicit approval only) | `prompter restore --file <zip> --yes` |
| Diagnose storage locations | `prompter doctor` |
