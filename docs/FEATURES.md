# Features

User-facing capabilities of Revit.AddinManager as implemented. This document describes
what the app does today; technical decisions live
in [architecture.md](architecture.md).

Revit.AddinManager is a pre-launch manager for Autodesk Revit `.addin` manifests
(Revit 2021–2027): it shows every installed plugin, lets you enable/disable `.addin`
files and edit their manifests — everything happens before Revit starts.

## Installation

- One MSI per product version (`RevitAddinManager-<version>.msi`, from the GitHub
  release): per-machine install with a Start Menu shortcut, in-place upgrades.
  See `build/README.md` for how the installer is produced.

## Plugin list

- Scans all seven Revit versions (2021–2027) in both scopes: `%APPDATA%\Autodesk\Revit\Addins\<version>\`
  (User) and `%PROGRAMDATA%\Autodesk\Revit\Addins\<version>\` (Machine).
- Each `.addin` file is one row with a toggle: on means the file sits in the version root,
  off means it sits in `disabled/`. Toggling moves the file immediately.
- Search by file name; filter chips for scope (`All`/`User`/`Machine`) and version;
  sorting (`Name`/`Scope`/`Version`); grouping by scope or version.
- Hovering a row shows a card with the full path, version, scope, entry types, vendor
  and one line per manifest entry.
- Refresh button re-reads the disk. External changes (another process, Explorer, a second
  copy of the manager) are also picked up automatically through a file-system watcher —
  no manual refresh needed. After every rescan the previous selection is restored by file
  identity, falling back to the first visible row.
- A toggle that fails (e.g. no administrator rights for a Machine-scope file, or a locked
  file) rolls the checkbox back and shows the reason right under the row instead of crashing.
- Rows with problems carry a yellow warning badge next to the name (a card tooltip with a title, a separator and the reasons explains why):
  a file with no entries, empty `Assembly`/`FullClassName`, unknown entry `Type`, or a
  duplicate `AddInId` within the file or in other files of the same Revit version.
  The same duplicates are underlined red in the raw XML editor even when the other
  occurrence lives in a different file.
- The × button on each row deletes its file from disk for good (after confirmation).
  Like moves and saves, deletion is refused while Revit runs.
- The + button creates a new `.addin` file: name, scope (`User`/`Machine`), version and
  the `disabled` flag are asked in a dialog; the file starts as an empty manifest.

## Editor zone

- Layout switcher in the title bar: list only, list + editor (resizable split), editor only.
- Four editor modes with toolbar icons: manifest entries, structured form, raw XML markup,
  file settings (`ManifestSettings`).

## Manifest entries

- Lists every `<AddIn>` record of the selected file (display name, type, vendor, `AddInId`)
  with a hover card carrying the full details. Read-only; selecting a row feeds the form.
- A search box in the footer, next to the add button, filters entries live by display name, type, vendor,
  `AddInId`, `Assembly` and `FullClassName`.
- The entry being edited stays selected across reloads (restored by `AddInId`).
- The × button on each row deletes the entry from the file for good (after confirmation);
  like moves and saves, deletion is refused while Revit runs.
- The + icon button in the footer adds a new entry: a dialog asks for its `Type`; a fresh
  `AddInId` is generated plus a default title (`Name` for Application/DBApplication, `Text`
  for Command), all other fields are empty. The entry is written to the file at once as a
  separate change and selected for editing; the form fills the rest, and saving is blocked
  until the required `Assembly` and `FullClassName` are set.

## Structured form

- Required fields (`Assembly`, `AddInId`, `FullClassName`) validate inline under the field and block saving until fixed; other failures surface in the panel banner.
- Edits one entry field by field: `Type` (read-only display), `Name`/`Text`, `Description`,
  `LongDescription`, `Assembly`, `AddInId` (with a generate button), `FullClassName`,
  `AvailabilityClassName`, vendor fields, `VisibilityMode`/`Discipline` chip toggles,
  image paths. Field labels always use the English tag names in both UI languages.
- Only fields valid for the entry's type are shown; anything else is hidden, not disabled.
- Saving validates the GUID and the whole file, writes atomically (temp file plus `.bak`
  backup) and refreshes the list, entries and markup views. Failures surface in the panel
  banner; the form keeps the edits.

## Raw XML markup

- Full-file editor with line numbers, undo/redo and XML highlighting that follows the
  Light/Dark theme. Live validation reuses the same parser as saving, so what you see is
  what blocks the save (structural errors, bad GUIDs, duplicate `AddInId` values).
  Empty required fields and duplicate `AddInId` values are additionally underlined red
  right in the text, as you type.
- Unsaved changes are badged; Discard restores the last saved text. Saving uses the same
  atomic-write-plus-`.bak` path as the form.

## File settings (`ManifestSettings`)

- Edits the file-level isolation block: `UseRevitContext` (Unset/Yes/No chips) and
  `ContextName`. Clearing both fields removes the block from the file instead of leaving
  an empty tag.
- Below Revit 2026 the panel explains that the block is unsupported and disables saving
  instead of writing something old Revit versions might not understand.

## Appearance

- Themes `System`/`Light`/`Dark` (toolbar button cycles them), persisted across restarts;
  the whole UI including the XML highlighting follows the choice.
- Interface languages `System`/`Russian`/`English` (toolbar globe button cycles them),
  switched live without restart, persisted across restarts. Field names stay English in
  both languages because they mirror manifest tag names; everything else translates.
- Short self-dismissing toasts confirm theme/language switches and manual list refreshes.
- A "Check for updates" toolbar button queries the latest GitHub release: if a newer
  version exists it offers to download it (opening the release page in the browser),
  otherwise it confirms "up to date" — and reports check failures as a toast.
- A "Support" toolbar button opens the repository's issue tracker in the browser for
  help and bug reports.
- A "Sponsor" toolbar button opens the GitHub Sponsors page to support the author.

## Revit process guard

- While any `Revit.exe` runs, a floating top-center banner lists the running versions
  (e.g. `2024, 2025`) and warns that `.addin` changes apply after restart. It can be
  dismissed until Revit exits; the next Revit session shows it again.
- Editing is locked while Revit lives: list toggles, form/markup/settings fields and all
  save buttons go inert (search, filters, refresh, selection and appearance switching
  keep working — they write nothing). The disk layer refuses on its own too: moves and
  saves throw a localized error before touching any file, so nothing can slip through
  past the UI.

## Diagnostics and settings storage

- Structured log: rolling daily file under `%APPDATA%\Volocy\Revit.AddinManager\logs\`.
- Choices persist under `%APPDATA%\Volocy\Revit.AddinManager\` (`theme.txt`, `language.txt`);
  a missing or corrupt file falls back to the system value.

## Not yet

Plugin groups and group-based activation are still under development; the `Launch Revit` button is deliberately out of scope for now.
