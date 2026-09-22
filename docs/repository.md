# Repository guide

Paths in this document are relative to the repository root.
Read and follow the [development policy](policies/development.md) alongside this guide.

## About the project

Revit.AddinManager is a pre-launch manager for Autodesk Revit `.addin` manifests (Revit 2021–2027):
browse every installed plugin across versions and scopes, toggle `.addin` files
on/off, and edit their manifests — all before Revit starts.
Detailed behavior is defined in [the plan](PLAN.md); UI explores live in [the mockup](ui-mockup.html).

## Solution and structure

- `Revit.AddinManager.slnx` — solution (`src/` + `tests/`; `sandbox/`, `installer/` and
  `build/` each keep their own solution, like the gallery does)
- `src/` — projects (`AddinManager.Core`: manifest parsing, disk scanning and Revit process guard, UI-framework-agnostic; `AddinManager.Localization`: interface language choice (`System`/`Russian`/`English`) persistence and culture application, UI-framework-agnostic; `AddinManager.Theming`: theme choice persistence, UI-framework-agnostic; `AddinManager.Theming.Wpf`: WPF theme resource dictionaries and the dictionary-switching helper; `AddinManager.Launcher`: WPF shell on the built-in Fluent theme)
- `tests/` — headless tests (`*.Tests`)
- `installer/` — separate solution (`Revit.AddinManager.Installer.slnx`) with the
  WixSharp MSI generator: builds exactly one MSI for the launcher
- `build/` — separate solution (`Revit.AddinManager.Build.slnx`) with the ModularPipelines
  release pipeline (compile the launcher, pack the MSI, publish the GitHub release)
- `docs/` — contributor documentation, policies and the plan
- `scripts/` — maintenance scripts
- `.github/` — CI workflows
- `sandbox/` — отдельный solution (`AddinManager.Sandbox.slnx`) с песочницей разметки
  (`AddinManager.Playground`: вставка XAML-фрагмента и живой предпросмотр через
  `XamlReader`); не часть основной сборки
- `Abstractions/` внутри каждого проекта в `src/` — интерфейсы, зеркалящие расположение
  реализаций (см. [development policy](policies/development.md))

## Technology stack

- .NET 10 (`net10.0` libraries; WPF launcher on `net10.0-windows`)
- `Microsoft.Extensions.*` (DependencyInjection, Hosting, Localization, Logging.Abstractions) for composition
- `CommunityToolkit.Mvvm` for view models (`MainViewModel`: layout state plus commands)
- `Microsoft.Xaml.Behaviors.Wpf` for view mechanics shared across views (window drag, dialog-result mirroring)
- Serilog (`Serilog.Extensions.Hosting`, `Serilog.Sinks.File`) for structured logging to a rolling
  daily file under `%APPDATA%\Volocy\Revit.AddinManager\logs\`
- AvalonEdit for the Markup zone's raw-XML editor (line numbers, undo/redo, XML syntax
  highlighting)
- Tests: **xunit.v3** via Microsoft.Testing.Platform (test projects are executables)
- `SonarAnalyzer.CSharp` wired globally; Central Package Management via `Directory.Packages.props`
  (`installer/` and `build/` manage their own package versions separately, mirroring `revit.linter`)
- `GenerateDocumentationFile` with `CS1591` as error (tests exempt)
- Installer: **WixSharp** (`WixSharp.Core`/`WixSharp.Msi.Core`, `WixToolset.UI.wixext`)
  over the WiX 6 toolset (`dotnet tool install --global wix`); one MSI per product version
- Release pipeline: **ModularPipelines** (+`ModularPipelines.DotNet/Git`, `Shouldly`);
  details in `build/README.md`

## Documentation layout

- `AGENTS.md` links to the required repository guidance.
- `docs/policies/development.md` contains the development policy.
- `docs/repository.md` describes the project, repository structure, and technology stack.
- `docs/PLAN.md` is the product plan; `docs/ui-mockup.html` is the clickable UI mockup.
- `docs/FEATURES.md` describes the implemented user-facing features and current scope.
- `docs/WIKI.md` is the Russian end-user guide.
- `docs/architecture.md` records the fundamental decisions (MVVM, DI, themes, parser).

The root solution exposes contributor documentation under `docs` and maintenance files under
matching subfolders, preserving their directory hierarchy. When adding documentation files,
also add them as solution items; solution folders do not automatically include new files.

The root `global.json` selects stable .NET SDK 10.0 (minimum `10.0.100`, `rollForward: latestFeature`)
and the Microsoft.Testing.Platform test runner. See the [SDK selection policy](policies/development.md#net-sdk-selection).

## Solution items

The root solution exposes repository-level documents and configuration under `solutionItems`, GitHub files and maintenance scripts in matching subfolders, and documentation under `docs/`. The list is explicit, not a filesystem glob; keep links up to date when files change. See the [solution items policy](policies/development.md#solution-items).

## Repository validation

`scripts/Validate-Repository.ps1` enforces the required repository documents,
their navigation links, and complete, valid Solution Items. The
`.github/workflows/repository-policy.yml` workflow runs it for pushes and pull
requests. See the [development policy](policies/development.md#repository-validation).

## Formatting

The root `.editorconfig` defines the portable formatting baseline. See the [development policy](policies/development.md#formatting-baseline).

## Testing

Headless test projects (`*.Tests`) run as MTP executables; CI executes each one explicitly.
See the [development policy](policies/development.md#test-execution).

## Continuous integration

The CI workflow builds Debug and Release on push to `main` and on pull requests, then runs
each headless test executable. The repository-policy workflow validates documents, links
and Solution Items. The publish-release workflow (manual dispatch) verifies the stable
tag, then delegates the whole release to the `build/` pipeline.

## Wiki publication

`.github/workflows/publish-wiki.yml` synchronizes `docs/WIKI.md` to `Home.md` in
the repository's GitHub Wiki after changes reach `main`, or on manual dispatch
from `main`. It reads the latest `main`, fixes the guide's repository links,
preserves other wiki pages, and commits only when the content changes.

One-time setup: enable Wiki in repository settings and save an initial page in
the GitHub interface so the `.wiki.git` repository exists. Add an Actions secret
named `WIKI_TOKEN` containing a personal access token with wiki write access
(classic token: `public_repo` for a public repository, `repo` for a private one).
The token owner must have write access; authorize organization SSO if required.
Renew the secret when its token expires. The checkout uses the built-in token
with read permissions; publication uses only `WIKI_TOKEN`.

Edit `docs/WIKI.md` rather than the generated Wiki Home page. Commit and push
the workflow and source, then run **Publish wiki** from Actions for the initial
publication. If cloning fails, check initial page creation and token access.

## Versioning and release tags

GitVersion in Continuous Delivery mode with patch increments. Stable releases are tagged
manually; binaries and installers use the corresponding version. The release flow is:
tag the commit (`v1.4.0`), push the commit and tag, run the `Publish release` workflow —
GitVersion resolves the version from the tag, the pipeline builds the launcher with
`-p:Version`, packs the single `RevitAddinManager-<version>.msi` and publishes it as
the release asset (see `build/README.md`).
