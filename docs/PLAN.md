# revit.addin-manager plan (v1)

## 1. What and how

A Windows 11-style WPF launcher (built-in Fluent theme; WPF-UI evaluated in `sandbox/`),
`net10-windows`, `CommunityToolkit.Mvvm`, `Microsoft.Extensions.Hosting` (DI),
MSI installer, `requireAdministrator` (we touch `ProgramData`).

Management happens only before Revit starts. Apply on click, a single `Launch Revit` button.
`Win+W` Widgets Board rejected (MSIX + Adaptive Cards don't fit).

## 2. Data

The on-disk catalog is `.addin` files, each holding 1..N `<AddIn>` entries
(each entry has its own `AddInId/Name/Assembly`). The v1 activation unit is the **file**:
moving `root <-> disabled/` enables/disables all its entries at once.
Entries render as an expandable child list inside the file row, entries have no toggles.
Each entry gets a `Type` badge (`Application/DBApplication/Command`); the file row
gets a summary (`Application`, `Command`, or a list like `Application + Command` —
never the word `mixed`).
`Type` does not affect activation (the whole file moves) but matters for meaning:
`Application`s slow startup most, `Command`s load on demand.
Splitting one `.addin` into per-entry files is not v1.

We scan per version, all seven `2021–2027`:

- `%APPDATA%\Autodesk\Revit\Addins\<version>\`
- `%PROGRAMDATA%\Autodesk\Revit\Addins\<version>\`

Enabled = file in root, disabled = file in `disabled/` (lowercase, canonical).

`groups.json` (`%APPDATA%\Volocy\Revit.AddinManager\groups.json`) is, for now, just the group
catalog: which groups exist and which files are in each — nothing about what's turned on.
Keys are versions `2021`…`2027`, each split into `User`/`Machine` (matching `AddinScope`,
`AddinManager.Core.Storage`). A group is entirely inside one `(version, scope)` node — it
can't mix `User` and `Machine` members, and can't reuse membership across Revit versions.
Membership is by file name.

```json
{
  "2025": {
    "User": [
      { "id": "g1", "name": "Group 1", "members": ["Structural.addin", "MEP.addin"] }
    ],
    "Machine": []
  },
  "2021": {
    "User": [],
    "Machine": []
  }
}
```

How a group's selection turns into enabled/disabled files (activation/selection state,
persistence, single-click select-these-deselect-the-rest UX) isn't decided yet — this plan
changes as that lands, don't over-read the current shape.
Missing version folders/installs are an empty `Revit NNNN not found` state, not an error.

Configuration writes are atomic (temp + move) after every click.
Configuration is the truth for known plugins, disk is the truth for existence.

## 3. Activation — OR

Rough idea, not final: a file is active if it's manually turned on, or covered by a selected
group, or its whole scope is selected as a unit. How that's actually stored/persisted is not
decided (see section 2) — this section will be rewritten once that lands.

- Groups live inside one `(version, scope)` node; nothing merges across scopes or versions —
  a group can't mix `User` and `Machine` members, and can't reuse membership from another
  Revit version (see `groups.json`, section 2).
- One file can belong to N groups within its own scope node; file identity is `FileName`
  within `(version, scope)`.
- A row with N>1 entries gets an `N entries` badge + a `toggle together` hint.
- `User`/`Machine` chips are the scope partition itself, selectable like a group — turning
  one on is a mass operation requiring a confirm (`N will be enabled/disabled`). Coverage is
  shown by the scope badge selected-style (no `from: Machine` duplicate); `from: ...` is
  written only for user groups.
- No exclusions (`manualOff`).
- Version and scope together are the window context; all recomputes stay inside that pair.

## 4. Two-way sync

App → disk: recompute the union → `File.Move` only the delta
(`root <-> disabled/`), block UI during the operation, error = toast + toggle rollback.

Disk → app, rescan (startup, version switch, window focus, manual `Refresh` button, `F5` shortcut):

| Event | Action |
|---|---|
| New `.addin` in root | Into catalog; covered by a selected group (incl. system) → write nothing, else `+manualOn`; `new` badge |
| New `.addin` in `disabled/` | Into catalog disabled, `new` badge |
| File deleted | Remove from catalog and `manualOn`, keep a grey `not installed` stub in groups |
| Known file moved by hand | Move back to match configuration + `synchronized` toast |

~~No `FileSystemWatcher` in v1, event-driven rescan only.~~ Implemented: `FileSystemAddinChangeWatcher` with debounce (see docs/architecture.md, "Disk change watcher").

External configuration change: check hash on startup/focus/before sync;
valid → load, recompute, catch disk up; corrupt → error,
stay on last good state + `.bak`.

## 5. Guard and launch

Background `Revit.exe` monitoring (`IRevitProcessGuard`): poll every ~3s +
checks on window focus and before every write. No manual button:
the toast appears/disappears on its own as the process comes and goes. While it lives —
toggles and editor locked. No `.addin` moves while live.

The `Launch Revit` button finds `Revit.exe` of the selected version via `IRevitLocator`
(all seven, `2021–2027`) and starts it, applying nothing (everything is already applied by clicks).

## 6. UI

- Top: version select + search + `Refresh` + `+ Group` + `Launch Revit`.
- `Revit is running` banner: not a full-width strip but a floating top-center toast
  (Win11 InfoBar style): translucent blurred background, thin outline, shadow, close button; doesn't shift layout.
- Flat list: each `.addin` file is one row (identity `(FileName, Scope)`),
  no duplicates or sections; file entries are an expandable child list without toggles; membership — group badges, `out of group` — a separate badge
  (direct enablement via the row toggle);
  state hides nothing. Chips above the list: `User / Machine` (filter +
  predicate select), user groups (filter + select), `No group` —
  filters rows without groups. Click — filter, separate checkbox — select.
- Row: toggle = file union; header — file name + first entry;
  subheader `Vendor • VendorDescription` of the first entry
  (ellipsized); N>1 — `N entries` badge;
  sources — `out of group` and `from: <group>` badges; system-group coverage
  shows the scope badge selected-style, no duplicate.
- Group header: select checkbox + counter, actions
  `rename / members / delete`.
- Layout switcher in the toolbar (3 modes): `List` | `List + Editor` | `Editor`.
- `List + Editor` mode: vertical `60/40` split with resizer,
  splitter position remembered; selecting a row opens its `.addin`
  on the right. `Esc` or repeated click collapses back to `List`.
- Editor pane: header `FileName + [User|Machine] + Enabled/Disabled + path`
  (`root/disabled/` only in the path hint, not a badge). Group membership
  is edited only via the `Members` pane, never in the manifest editor.
  With N>1, an entry switcher on top (tabs `Entry 1..N` by `Name`);
  the active entry form covers the full `.addin` schema (required `*`):
  `Type* (Application/DBApplication/Command)`, `Name`, `Text` (Command),
  `Description`, `LongDescription` (Command), `Assembly*`, `AddInId*` (GUID
  + generate button), `FullClassName*`, `AvailabilityClassName` (Command),
  `VendorId`, `VendorDescription`, `VisibilityMode` (Command, multi),
  `Discipline` (Command, multi), `LargeImage/SmallImage/ToolTipImage`.
  Foreign-`Type` fields are fully hidden (the form rebuilds per `Type`,
  no disabling). `Application/Command` entries share one `AddinEntry` record
  (decision: no split entities, differences live in `IManifestSchema`,
  display name is `Name ?? Text`).
- Versioned schema (table in Core, data — not code, `IManifestSchema`/`RevitManifestSchema`
  — implemented, see docs/architecture.md, "Form zone"): `Type` availability and fields
  depend on the selected version, in principle. Verified against Autodesk's own docs
  (Revit API Developers Guide, "Add-in Registration") and API history: `Application`/
  `Command`/`DBApplication` and every entry field (`VisibilityMode`, `Discipline`,
  `AvailabilityClassName`, `LargeImage`/`SmallImage`/`ToolTipImage` included) have all
  been in the API since well before 2021 — the original "`DBApplication` — since `2022`"
  guess above was wrong; `IExternalDBApplication` shipped in Revit **2012**. So within our
  2021-2027 range, every type and field is available in every version; the `<ManifestSettings>`
  block (`UseRevitContext`, `ContextName`) is the one real version boundary — since `2026`
  (documented for `2026`; shipping `ManifestSettings` to older versions may crash Revit —
  offer to strip on save).
  Unsupported content for a version gets a warning, save is not blocked,
  offer `remove unsupported`. Schema validation is per-version too. The raw XML tab shows the whole file.
  Saving writes the file atomically + `.bak`, touches only the active entry's
  nodes (the rest byte-for-byte), validates `AddInId` uniqueness
  within the file. Raw XML is freely editable, but structural errors
  and `AddInId` duplicates block saving. Saving only with Revit closed.
- Load context (file level, not entry): an `Isolation` section in the editor
  above the entry tabs — an `Isolated context (UseRevitContext=False)` toggle +
  `ContextName` (empty = folder context; one name merges files from different
  folders into one context). Effective since `2026`, hidden on older versions
  (on read show `2026+ only`, never silently edit).
  ~~The list shows a context badge `shared / own(:name)`.~~ Implemented as its
  own `Editor` mode/ViewModel (`ManifestSettingsViewModel`/`EditorMode.Settings`),
  see docs/architecture.md, "Settings zone" — the list-row context badge is not
  part of this iteration, only the file-level editor.
- Style system (reusable, built first):
- Tokens (single `Styles/Tokens.xaml` dictionary): accent = Windows system,
  themes `Light / Dark / System` (default — system), choice persisted
  in settings; dark is mandatory for all dictionaries — list, badges,
  toolbar, split, editor (including XML highlighting) with no light leaks;
  icons/illustrations in both variants. Radii `4/8`,
  spacing `4/8/12/16`, typography `Caption/Body/Title` — all controls
  only via tokens, no hex in markup.
- Dictionaries (each reused by all windows/dialogs):
  `Tokens.xaml` → `Controls.xaml` (Button, ToggleSwitch, Badge, TextBox,
  plugin row ListViewItem, group header) → `Layouts.xaml` (toolbar,
  split panes) → `Editor.xaml` (manifest form).
- Rules: `disabled` state (Revit running) — one dimming style;
  badges `User/Machine/new/not installed`, `out of group`, `from: <group>` — one
  `Badge` component (+scope badge selected-style); row toggle = OR union,
  on/off style from tokens. No plain checkboxes anywhere: multi-options
  (`VisibilityMode`, `Discipline`, group members) are rows with toggles.
- A new window is assembled from ready styles; new paddings/colors only
  via token updates, never locally in markup.

## 7. Groups

- `+ Group`: name unique per `(version, scope)`, created unselected.
- The `Members` button opens the group editor in the right split pane
  (same split, `Group` mode: version catalog search + checklist);
  selecting a plugin row switches the pane to `Manifest` mode.
- `Group from enabled`: members = current union.
- Deleting a selected group turns off its exclusive members
  (a `will turn off N` warning). Empty names/duplicates forbidden.

## 8. DI

`Microsoft.Extensions.Hosting`:

- All services hide behind abstractions, Core owns the interfaces, implementations
  are swappable without touching consumers: `IAddinManifestParser`
  (`LinqToXmlAddinManifestParser`), `IThemeService` (`ThemeService`),
  `IAddinStore`, `IGroupStore`,
  `IRevitLocator`, `IRevitProcessGuard`, `IManifestSchema` (version table).
  Resolve concretes only through the DI container; `new` on an implementation
  is forbidden outside tests and the composition root.
- Build quality: `SonarAnalyzer.CSharp` globally, CPM
  (`Directory.Packages.props`), mandatory XML documentation (`CS1591`
  as error; `NoWarn` in tests, tests are not documented).

- `Singleton`: `IThemeService`, zone view models, `MainViewModel`, `MainWindow`.
- No `new Store()` in Views, only constructor injection.

## 9. TDD (Core only, no UI)

`xunit.v3`, headless. FS — temp folders / `System.IO.Abstractions`,
processes and registry — mocks.

Order:

1. Scan: sees `*.addin` in root, ignores `disabled/`, parses `AddInId`.
2. Move `root <-> disabled/` idempotent + rollback on error.
3. OR recompute with overlapping groups.
4. Delta sync app → disk.
5. Refresh matrix disk → app (new/deleted/moved).
6. External configuration (valid/corrupt).
7. Guard blocking.
8. Versioned schema: `Type/field × version` matrix (incl. `DBApplication`
   before `2022` — warning), saving with unsupported tags — warning.

## 10. Installer

One MSI with a launcher shortcut. No MSIX.
Implemented: single per-machine MSI via WixSharp plus a ModularPipelines release flow (see docs/architecture.md, "Installer and release build", and build/README.md).

## 11. Not v1

Profiles over groups, `manualOff`,
splitting one `.addin` into entries, disabling via the `AddInItemSettings.Disabled`
flag without moving files (research separately).

## 12. Open questions

- Windows 11 only or +Windows 10 (no Mica there)?
- ~~Which `.addin` fields are editable in v1 (minimum: `Description`, `Assembly` path)?~~
  Resolved: all fields `IManifestSchema` lists for the entry's type (see docs/architecture.md,
  "Form zone"), plus the file-level `ManifestSettings` isolation section, its own `Editor`
  mode (see docs/architecture.md, "Settings zone").
