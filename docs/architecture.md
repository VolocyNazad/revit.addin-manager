# Architecture decisions

This document records stable architectural rules. Implementation details belong in code and tests.

## MVVM

- Views live in `Views/`; view models live in `ViewModels/`.
- Zone view models are independent. Shared state is exposed through services, not root-model forwarding.
- Zone selection and the file catalog cross zone boundaries through narrow contracts, not concrete neighbors: `IFileSelection`/`IEntrySelection` (single slots with change notifications) and `IAddinFileCatalog` (live rows plus rescan), all implemented by the owning zones themselves and resolved from the container. Selection flows one way, list → entries; zones never reference each other back.
- Code-behind contains view mechanics only. Prefer bindings, converters and Blend behaviors for state and reusable mechanics.
- Filtering, sorting and grouping use one `ListCollectionView` over the source collection.
- View-model diagnostics are available through `ToString()`.

## Dependency injection

- `App.OnStartup` builds the host and composition root. There is no `StartupUri`.
- UI-independent contracts live in the owning project's `Abstractions/` tree and mirror implementation namespaces.
- Consumers depend on interfaces. Implementations are registered in DI; `new` for implementations is limited to composition and tests.
- View and view-model registration is convention-based through `AddModule<TView>()`; `ViewModelLocator` resolves composed views from the container.
- Runtime views may expose parameterless design-time constructors. Design-time data lives in `AddinManager.Launcher.Design`.
- Rows and option view models are created through DI factories because their data is runtime-specific.
- Dialogs are transient and created through `IDialogService`; shared window behaviors are declared in XAML.

## Themes

- `ThemeService` persists `System`, `Light` or `Dark` under `%APPDATA%` and raises `ThemeChanged`.
- WPF dictionaries define the same keys for light and dark themes. Consumers use `DynamicResource`.
- The system theme is read from the registry. Missing or invalid settings fall back to `System`.

## Localization

- `LocalizationService` persists `System`, `Russian` or `English` and applies `CurrentUICulture` and `DefaultThreadCurrentUICulture`.
- UI strings come from `IStringLocalizer<T>` resources next to their owning types. Neutral resources are English; Russian resources are satellites.
- Language changes are live. View models refresh their displayed values; list rows are recreated when necessary.
- Manifest field and tag names remain English in both languages.

## Plugin list and file changes

- `ListViewModel` owns the scanned file list and its `ICollectionView`; rows expose display state and commands.
- `IAddinChangeWatcher` debounces file-system notifications and triggers a refresh. Refreshes preserve selection by file identity.
- Disk operations go through `IAddinStore`. Writes are atomic and keep a `.bak` copy where applicable.

## Editor

- `EditorView` exposes independent Form, Markup and Settings modes.
- The Markup mode uses AvalonEdit and reparses text for diagnostics. Saves go through the same parser and atomic markup service as structured editing.
- The Form mode edits one entry. The Settings mode edits file-level `ManifestSettings` and is available only for supported Revit versions.
- Entry creation and deletion are whole-manifest saves; cancelled dialogs leave the file unchanged.

## Revit process guard

- `IRevitProcessGuard` polls `Revit.exe`, reports changes in the set of running versions, and exposes `CheckNow()` for activation checks.
- Editing commands and disk writes are disabled or rejected while Revit is running. Read-only navigation remains available.
- Guard events may arrive off the UI thread; consumers marshal updates through `IUiDispatcher`.

## Manifest parser

- `IAddinManifestParser` maps XML to one model for all entry types and back.
- Parsing preserves the declaration, element order, comments and unknown nodes for honest round trips.
- Unknown entry types are represented as `Unknown` and are not rejected solely for that reason.
- Structural validation is shared by live diagnostics and save operations.

## Diagnostics

- File and entry rows warn about missing required data, unknown types and duplicate `AddInId` values.
- Cross-file duplicates are scoped to a Revit version. The Markup editor reports the same duplicates.

## Logging

- Serilog is configured once in `App.OnStartup`; application code uses `ILogger<T>`.
- Log lifecycle events, user-visible disk mutations and failures once. Use structured placeholders and keep benign probes silent.

## Installer and release

- `installer/` builds one per-machine WixSharp MSI for the launcher.
- `build/` resolves GitVersion, builds the launcher, packs the MSI and publishes releases.
- Installer and build areas keep their own solutions and package configuration.
