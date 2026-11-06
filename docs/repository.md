# Repository guide

Paths in this document are relative to the repository root.
Read and follow the [development policy](policies/development.md) alongside this guide.

## About the project

Revit.AddinManager is a pre-launch manager for Autodesk Revit `.addin` manifests (Revit 2021–2027):
browse every installed plugin across versions and scopes, toggle `.addin` files
on/off, and edit their manifests — all before Revit starts.
Detailed behavior is defined in [FEATURES.md](FEATURES.md).

## Solution and structure

- `Revit.AddinManager.slnx` — solution (`src/` + `tests/`; `sandbox/`, `installer/` and
  `build/` each keep their own solution)
- `src/` — projects (`AddinManager.Core`: manifest parsing, disk scanning, the headless text-search engine and Revit process guard, UI-framework-agnostic, plus `Abstractions/ProductIdentity` with the vendor, product, GitHub, MSI and synchronization names shared by the launcher, build pipeline and installer via source link; `AddinManager.Localization`: interface language choice (`System`/`Russian`/`English`) persistence and culture application, UI-framework-agnostic; `AddinManager.Theming`: theme choice persistence, UI-framework-agnostic; `AddinManager.Theming.Wpf`: WPF theme resource dictionaries and the dictionary-switching helper; `AddinManager.Launcher`: WPF shell on the built-in Fluent theme, including the Markup editor and its find bar)
- `tests/` — headless tests (`*.Tests`); `tests/Shared/` holds the helpers linked into the
  test projects that need them (`TestCulture` pinning both UI cultures, `TestLocalization`,
  the structured `RecordingLogger`, `TempDirectorySet`, `TestManifestXml`), so a helper has
  one source
- `installer/` — separate solution (`Revit.AddinManager.Installer.slnx`) with the
  WixSharp MSI generator: builds exactly one per-machine MSI for the launcher
- `build/` — separate solution (`Revit.AddinManager.Build.slnx`) with the ModularPipelines
  release pipeline (compile the launcher, pack the MSI, publish the GitHub release)
- `docs/` — contributor documentation and policies
- `scripts/` — maintenance scripts (`Validate-Repository.ps1` checks the repository policy,
  `Invoke-HeadlessTests.ps1` runs the headless test executables, `Update-Changelog.ps1` verifies and
  composes changelog fragments, `Verify-Release.ps1` checks the changelog structure and the release
  tag, `Clear-BuildOutput.ps1` removes `bin`/`obj` directories without touching sources)
- `.github/` — CI workflows, Dependabot and the pull request template
- `changelog.d/` — one fragment per user-visible change, grouped by Keep a Changelog category; the
  release section is composed from it (see the [changelog policy](policies/changelog.md))
- `sandbox/` — a separate solution (`AddinManager.Sandbox.slnx`) with the markup playground
  (`AddinManager.Playground`: inserts a XAML fragment and previews it live through `XamlReader`);
  it is not part of the main build, but CI compiles it so the playground cannot rot
- `Abstractions/` inside every project in `src/` — interfaces mirroring the implementation layout
  (see the [development policy](policies/development.md))

## Technology stack

- .NET 10 (`net10.0` libraries; WPF launcher on `net10.0-windows`)
- `Microsoft.Extensions.*` (DependencyInjection, Hosting, Localization, Logging.Abstractions) for composition
- `CommunityToolkit.Mvvm` for view models (`MainViewModel`: layout, theme/language switching, toasts, update check and Revit-guard commands)
- `Microsoft.Xaml.Behaviors.Wpf` for view mechanics shared across views (window drag, dialog-result mirroring)
- `VolocyNazad.Wpf.DependencyPropertyGenerator` for the dependency properties of the launcher's Markup
  editor behaviors, `DialogResultBehavior` and `PackIcon`: the registration, the CLR wrapper or the
  `Get`/`Set` accessors, the XML documentation and the change callback wiring are generated from
  `[GenerateDependencyProperty<T>]` and `[GenerateAttachedDependencyProperty<T>]`; only the launcher
  references the package, since it is the only project that declares WPF dependency properties
- Serilog (`Serilog.Extensions.Hosting`, `Serilog.Sinks.File`) for structured logging to size- and
  day-rolled files under `%LOCALAPPDATA%\Volocy\Revit.AddinManager\logs\`, retaining at most
  14 files of up to 20 MB each
- AvalonEdit for the Markup zone's raw-XML editor (line numbers, undo/redo, XML syntax
  highlighting)
- Windows Recycle Bin deletion via the framework `Microsoft.VisualBasic.FileIO` API (no NuGet
  package)
- Tests: **xunit.v3** in-process runner (test projects are `OutputType: Exe` executables, run
  through `scripts/Invoke-HeadlessTests.ps1`)
- `SonarAnalyzer.CSharp` wired globally; Central Package Management via `Directory.Packages.props`
  (`installer/` and `build/` manage their own package versions separately)
- `GenerateDocumentationFile` with `CS1591` as error (tests exempt)
- Installer: **WixSharp** (`WixSharp.Core`/`WixSharp.Msi.Core`, `WixToolset.UI.wixext`)
  over the WiX 6 toolset (`dotnet tool install --global wix`); one MSI per product version
- Release pipeline: **ModularPipelines** (+`ModularPipelines.DotNet/Git`, `Shouldly`);
  details in `build/README.md`

## Documentation layout

- `AGENTS.md` routes a change to the documents it must read.
- `docs/policies/README.md` is the policy index: what each policy owns and what checks it.
- `docs/policies/development.md` is the entry point; the other policies own one subject each.
- `docs/repository.md` describes the project, repository structure, and technology stack.
- `docs/FEATURES.md` describes the implemented user-facing features and current scope.
- `docs/WIKI.md` is the Russian end-user guide.
- `docs/architecture.md` records the fundamental decisions (MVVM, DI, themes, parser).
- `GLOSSARY.md` fixes the shared product vocabulary.
- `TODO.md` lists agreed future work.
- `changelog.d/<category>/<slug>.md` holds one changelog fragment per user-visible change; the
  pending fragments are verified by `scripts/Update-Changelog.ps1 -Verify`.

The root solution exposes contributor documentation under `docs` and maintenance files under
matching subfolders, preserving their directory hierarchy. When adding documentation files,
also add them as solution items; solution folders do not automatically include new files.

The root `global.json` selects stable .NET SDK 10.0 (minimum `10.0.100`, `rollForward: latestFeature`).
See the [SDK selection policy](policies/build.md#sdk-selection).

## Solution items

The root solution exposes repository-level documents and configuration under `solutionItems`, GitHub files and maintenance scripts in matching subfolders, and documentation under `docs/`. The list is explicit, not a filesystem glob; keep links up to date when files change. See the [solution items policy](policies/repository-maintenance.md#solution-items).

## Repository validation

`scripts/Validate-Repository.ps1` enforces the required repository documents, the navigation links
between them, every relative Markdown link and its anchor, complete and valid Solution Items,
workflow actions pinned to a full version tag, no tracked `bin`/`obj` content, valid UTF-8 in tracked
text files, and neutral/Russian resource-key parity. The `.github/workflows/repository-policy.yml`
workflow runs it together with `scripts/Update-Changelog.ps1 -Verify` and `scripts/Verify-Release.ps1`
for pushes to `main` and `dev` and for pull requests. See the
[repository maintenance policy](policies/repository-maintenance.md#repository-validation).

## Formatting

The root `.editorconfig` defines the portable formatting baseline. See the
[repository maintenance policy](policies/repository-maintenance.md#formatting-baseline).

## Testing

Headless test projects (`*.Tests`) are xUnit v3 in-process runner executables. CI builds the
solution and runs them through `scripts/Invoke-HeadlessTests.ps1`, which writes a TRX report and
fails on a run without tests. See the [testing policy](policies/testing.md#test-infrastructure).

## Continuous integration

The CI workflow builds Debug and Release on push to `main` and `dev` and on pull requests with
`-warnaserror`, so compiler/analyzer warnings fail the build, then runs the headless tests through
the repository script. It also builds the release pipeline, the installer generator and the sandbox
solution in Release. A `CI gate` job aggregates those jobs and is the required check: a pull
request that only touches `docs/`, `changelog.d/` or Markdown files skips the Windows jobs, since it
cannot change the compiled product. The repository-policy workflow validates the documents, links,
Solution Items, action pins, tracked generated files, encoding and resource-key parity, verifies the
pending changelog fragments and checks the `CHANGELOG.md` structure. Dependabot opens grouped weekly
updates against `dev`; CodeQL (`build-mode: none`) and dependency review report findings but are not
required checks. The publish-release workflow (manual dispatch) verifies the stable tag, the changelog
and that `HEAD` is an ancestor of `main`, then delegates the whole release to the `build/` pipeline.

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
the release asset (see `build/README.md`). The tagged commit must already contain the
release section in `CHANGELOG.md` with an empty `Unreleased` section above it; the
release section is composed from the `changelog.d/` fragments with
`./scripts/Update-Changelog.ps1` and mirrors the changes since the previous stable tag.
`./scripts/Verify-Release.ps1 -FromHeadTag -AncestorOf main` stops the workflow otherwise,
and the pipeline publishes that section, followed by a link to the full changelog, as the GitHub
release description. See the
[changelog policy](policies/changelog.md).
