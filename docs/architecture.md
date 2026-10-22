# Architecture decisions

This document records stable architectural rules. Implementation details belong in code and tests.

## MVVM

- Views live in `Views/`, view models in `ViewModels/`; both mirror the UI zones with subfolders once a folder stops being readable (`Views/Dialogs/`, `ViewModels/Dialogs/`, and later List/Entries/Editor). Namespaces follow the folders, and resource files travel with their view model.
- Zone view models are independent. Shared state is exposed through services, not root-model forwarding.
- Zone selection and the file catalog cross zone boundaries through narrow contracts, not concrete neighbors: `IFileSelection`/`IEntrySelection` (single slots with change notifications) and `IAddinFileCatalog` (live rows plus rescan), exposed by the owning zones and resolved from the container; the list zone's catalog is the `FileCatalog` service. Selection flows one way, list → entries; zones never reference each other back.
- Code-behind contains view mechanics only. Prefer bindings, converters and Blend behaviors for state and reusable mechanics.
- Filtering, sorting and grouping use one `ListCollectionView` over the source collection, owned by `FileListViewState` and driven by a `FileListQuery` snapshot; the zone builds the snapshot and keeps the chip labels/counters. Version chips are data-driven `FilterChipViewModel` collections built from `RevitVersionCatalog`, so the supported-version list is not copied into XAML.
- Codes and view models share one set of chips; the active chip is a style trigger on `IsActive` rather than a per-button opacity binding.
- View-model diagnostics are available through `ToString()`.

## Dependency injection

- `App.OnStartup` builds the host and delegates registration to `CompositionRoot.AddLauncherServices()`; `App` itself owns logging, the single-instance guard and window lifetime. There is no `StartupUri`.
- UI-independent contracts live in the owning project's `Abstractions/` tree and mirror implementation namespaces.
- Consumers depend on interfaces. Implementations are registered in DI; `new` for implementations is limited to composition and tests.
- Dependency-only types declare a primary constructor (`FileOperations(...)`, `UndoService(...)`); an explicit constructor is reserved for validation, computed or guarded defaults, overloads and `base(...)` calls. Source-generated constructors are not used, so the dependency graph stays visible in the type declaration.
- View and view-model registration is convention-based through `AddModule<TView>()`; `ViewModelLocator` resolves composed views from the container.
- Runtime views expose parameterless design-time constructors and `d:DataContext` over `Design*` stubs; design-time data lives in `AddinManager.Launcher.Design`, and every composed view with bindings carries it (see the development policy).
- Rows and option view models are created through DI factories because their data is runtime-specific.
- UI-specific contracts (dialogs, clipboard, folder opener, editor dirty state) live in the launcher's `Abstractions/Services` tree and may use Core types; Core stays WPF- and Windows-UI-free.
- Dialogs are transient and created through `IDialogService`; composition registers them with `AddDialog<TViewModel, TView>()` (transient view model and view plus a `Func<TView>` factory), and views use `DialogResultBehavior` and bindings instead of code-behind state.

## Themes

- `ThemeService` persists `System`, `Light` or `Dark` under `%APPDATA%`; `IThemeService` exposes the chosen `Theme` and the effective `IsDark`. `MainWindow` applies the matching dictionary and re-applies it when the window reports a system theme change.
- WPF dictionaries define the same keys for light and dark themes. Consumers use `DynamicResource`.
- Each theme composes shared chrome (`Themes/Chrome/`, без привязки к приложению) and app-specific parts (`Themes/App/`, подсветка AvalonEdit); key parity inside both pairs is enforced by `AddinManager.Theming.Wpf.Tests`.
- The system theme is read from the registry. Missing or invalid settings fall back to `System`.

## Styles

- Shared control styles live in `Styles/` (`Buttons.xaml`, `Toggles.xaml`, `Inputs.xaml`, `Tooltips.xaml`, `Menus.xaml`, `Notifications.xaml`, `Windows.xaml`, `Icons.xaml`, `Lists.xaml`, `ScrollBars.xaml`) and merge once in `App.xaml`; views must not duplicate them. View-specific behavior goes into a small local `BasedOn` style (`OptionToggleStyle`, `OptionButtonStyle`), never into a full copy. The monospace font is the shared `MonoFontFamily` resource.
- `WarningCardStyle` in `Styles/Notifications.xaml` is the only warning/error card geometry; views set only their local `Margin`/`Padding`/`CornerRadius`/`Visibility`. Colours come from the theme tokens (`ErrorBrush`, `WarningIconBrush`, `CaptionCloseHoverBrush`, `WarningBackgroundBrush`, `WarningBorderBrush`).

## Localization

- `LocalizationService` persists `System`, `Russian` or `English` and applies `CurrentUICulture` and `DefaultThreadCurrentUICulture`.
- UI strings come from `IStringLocalizer<T>` resources next to their owning types. Neutral resources are English; Russian resources are satellites.
- Language changes are live. View models refresh their displayed values; list rows are recreated when necessary.
- Manifest field and tag names remain English in both languages.

## Plugin list and file changes

- `FileCatalog` owns the scanned file rows: it scans every supported version, recreates rows through the factory, preserves bulk marks by identity and recalculates cross-file warnings. `ListViewModel` owns the selection, the view state and the row actions, and exposes `IAddinFileCatalog` to other zones. A file's identity is `AddinFileIdentity` (`FileName`, `Scope`, `Version`) in Core: selection restore, bulk marks, undo records and cross-file duplicate rules all use it instead of ad-hoc tuples or paths.
- `IAddinChangeWatcher` debounces file-system notifications and triggers a refresh. Refreshes preserve selection by file identity. A missing add-in folder at startup is not final: the watcher keeps retrying until the folder appears (fresh machine, Revit installed later).
- Disk operations go through `IAddinStore`. Writes are atomic and keep a `.bak` copy where applicable. The `.addin` suffix/trim rule is one Core helper (`AddinFileNames.Normalize`), shared by the real store and test doubles. `Delete` delegates to `IFileRecycler` in the launcher (Windows Recycle Bin); the permanent-delete branch exists only for Core tests, while production composition always supplies a recycler. Undo is single-level and owned by `IUndoService`: enable/disable (single and bulk), deletion, copy, move and rename are undoable; the service keeps the last action and performs its store mutations, while `ListViewModel` only refreshes the list and restores selection.
- `Move`/`Copy` in `IAddinStore` create missing target folders, validate collisions and optionally replace the target via the recycler. `MoveCopyDialogViewModel` shows the target path and compatibility warnings; `IEditorDirtyState` lets the list save or explicitly discard unsaved editor changes before moving. The same guard runs when the user selects another file: unsaved changes are saved, explicitly discarded or the switch is cancelled and the previous selection stays.
- File mutations are one `IFileOperations` service (`FileOperations`): it owns the confirmation/target dialogs, the `IAddinStore` mutations and the undo records, and returns a `FileOperationOutcome` (refresh, selection, reveal, banner error, toast). The list zone applies the outcome to the view and never touches the store for these commands.
- File-safety rules: deleting and replacing target files goes through `IFileRecycler` (Windows Recycle Bin); an overwritten target stays recoverable; undo is single-level and restores file content and replaced targets through `IAddinStore.Restore` (atomic write, blocked while Revit runs, never written straight from a view model); all disk mutations stay blocked while Revit runs.

## Editor

- `EditorView` exposes independent Form, Markup and Settings modes.
- The editor zone renders one shared header for every mode, including the combined entries+form and entries+markup modes: file name, meta line, unsaved badge, Save/Discard routed to the visible pane and that pane's error. Sub-panes do not repeat the header; `EditorViewModel` receives the pane state through `ISavablePane` and the file through `IFileSelection`.
- The Markup mode uses AvalonEdit and reparses text for diagnostics. Saves go through the same parser and atomic markup service as structured editing.
- AvalonEdit mechanics live in attached behaviors (`AvalonEditMarkupBehavior` for text/selection/diagnostic sync and the syntax palette, `AvalonEditSearchBehavior` for the find bar and its keyboard bindings); the view's code-behind only composes view and model.
- Markup diagnostic spans (empty required fields, unknown entry type, duplicate `AddInId` inside the file or across files of one version, the selected entry's block) come from the headless `MarkupDiagnostics` in Core (`MarkupSpan`); the view model only hands them to AvalonEdit rendering.
- The Markup mode's persistent find bar searches the whole file through the headless `TextSearchEngine` (`AddinManager.Core.Search`). `MarkupSearchViewModel` owns the query, options, match list and current position; the view only selects and highlights the matches and never changes the document or the save path.
- The Form mode edits one entry. The Settings mode edits file-level `ManifestSettings` and is available only for supported Revit versions.
- Entry creation and deletion are whole-manifest saves; cancelled dialogs leave the file unchanged.

## Revit process guard

- `IRevitProcessGuard` polls `Revit.exe`, reports changes in the set of running versions, and exposes `CheckNow()` for activation checks.
- Editing commands and disk writes are disabled or rejected while Revit is running. Read-only navigation remains available.
- Guard events may arrive off the UI thread; consumers marshal updates through `IUiDispatcher`.

## Manifest parser

- `IAddinManifestParser` maps XML to one model for all entry types and back.
- Parsing preserves the declaration, root child order (entries, settings and unknown nodes/comments may interleave), entry-level element order, comments, unknown nodes and unknown attributes on `RevitAddIns`, `AddIn` and `ManifestSettings`. Entries added after parsing are appended at the end; replaced entries keep their position.
- Unknown entry types are represented as `Unknown` and are not rejected solely for that reason.
- Structural validation is owned by one Core rule set, `AddinManifestValidator`, and shared by the parser consumers, the live diagnostics and the save path.

## Diagnostics

- Empty required values are an unfinished draft: they are warned about everywhere and block only the form's own save, so a new entry can still be created.
- Unknown entry types and duplicate `AddInId` values are warnings: the file stays readable and editable so the user can fix it. File rows, entry rows and the Markup editor report the same issues.
- Cross-file duplicates are scoped to a Revit version.

## Logging

- Serilog is configured once in `App.OnStartup`; application code uses `ILogger<T>`.
- Log lifecycle events, user-visible disk mutations and failures once. Use structured placeholders and keep benign probes silent.

## Installer and release

- `installer/` builds one per-machine WixSharp MSI for the launcher with Start Menu and Desktop shortcuts; only runtime files are installed (symbols and XML documentation are filtered out).
- `build/` resolves GitVersion, builds the launcher without symbol/documentation files, packs the MSI and publishes releases. The pipeline mode is an explicit first argument (`compile` default, `pack`, `publish`, `version`); an unknown mode fails instead of silently compiling.
- A release is described by its `CHANGELOG.md` version section: the tagged commit carries that
  section with an empty `Unreleased`, and the pipeline publishes it as the GitHub release notes.
- Installer and build areas keep their own solutions and package configuration.
