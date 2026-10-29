# Development policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

The stack described in [Repository guide](../repository.md) is used by default and takes priority over
whatever the agent might choose on its own. Prefer whatever is already used
in the affected part of the repository over an alternative. If an alternative
seems better — don't substitute it silently: ask the user,
explain the reason, and wait for confirmation.

If a change affects the folder structure or the technology stack (a new/removed project, a new dependency, a version bump worth recording, a new convention) — update the relevant policy and [repository guide](../repository.md) as part of the same change, not as a separate follow-up later.

Tests for behavior changes and bug fixes are expected by default. Ask the user first when existing test expectations change, when a test needs real disk/OS side effects (integration), or when coverage scope is genuinely ambiguous.

Commit messages follow Conventional Commits (`<type>(<scope>): <description>`, e.g. `feat(manifest): ...`, `fix(...): ...`, `docs(agents): ...`, `test(...): ...`, `chore(...): ...`, `refactor(...): ...`) — the scope is optional but preferred when it clarifies what exactly changed.

Do not `git push` — commit locally and leave the push to the user, unless they explicitly ask you to push.

Branching follows the [branching policy](branching.md): `main` holds the released state,
`dev` integrates the next release, and every change reaches them through a short-lived
branch and a pull request.

Do not update `CHANGELOG.md` with every change — leave `## [Unreleased]` empty during regular development. The release section is composed once when a release is prepared, from the changes since the previous stable tag (see the [release changelog](#release-changelog)). `CHANGELOG.md` keeps the [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format.

All services live behind abstractions: domain and headless contracts are owned by Core (`AddinManager.Core.Abstractions`); UI-specific contracts (dialogs, clipboard, folder opener, editor dirty state) live in the owning UI project's `Abstractions/` tree (`AddinManager.Launcher.Abstractions.Services`) and may use Core types. Core must not reference WPF or Windows-only UI APIs. Consumers depend only on interfaces, implementations are resolved through DI and are swappable without touching consumers. `new` on an implementation is allowed only in the composition root and in tests.

Abstractions live in a top-level `Abstractions/` folder mirroring the implementation layout (`Abstractions/Storage/IAddinStore.cs` for `Storage/FileSystemAddinStore.cs`, namespaces follow folders) — the same mirroring as localized resources follow their owning types. Folder and namespace both carry the `Abstractions` segment a separate `*.Abstractions` project would use (`AddinManager.Core.Abstractions`, ...), so extracting them later is a pure file move with no renames, no using churn, and no doc-`cref` churn.

### Constructors

- Use a primary constructor when the type only stores its dependencies (`public sealed class FileOperations(...)`); it keeps the dependency graph visible in the type declaration and avoids boilerplate that hides it.
- Keep an explicit constructor when it does real work beyond assignment: argument validation, computed fields, optional parameters with guarded/derived defaults, multiple overloads, or calling `base(...)` with transformed arguments. WPF views keep their parameterless constructor for `InitializeComponent` and may add a DI constructor next to it.
- Do not expose constructor parameters as public mutable properties; capture them in private readonly fields or private properties.
- Do not generate constructors with source generators (`AutoConstructor`-style attributes): the project already writes primary constructors directly, generated constructors would hide the graph from review, and a generated public constructor would still have to satisfy `CS1591`.

Optional dependencies are a test seam, not a production policy: the composition root must always supply the real implementation. A fallback that silently changes user-visible safety (for example, permanent delete instead of the Recycle Bin) is forbidden; if a fallback is unavoidable, it must be explicit, documented and logged.

Avoid code-behind in WPF views: prefer bindings, converters and DynamicResource over C# in `.xaml.cs`. Code-behind is for view mechanics with no declarative equivalent (window chrome, splitter drag, reading `ActualWidth`, and similar) — never for state. If it's unclear whether a declarative way exists, ask the user before writing code-behind instead of defaulting to it.

### Design-time data

- Every composed view with bindings (main window, zone views, dialogs) exposes a parameterless constructor and sets `d:DataContext` to a `Design*` stub from `AddinManager.Launcher.Design`; `d:`/`mc:` namespaces are declared on the view and `mc:Ignorable="d"` is present.
- A design stub shows a realistic populated state (several rows, an active chip, a warning or a long text), not an empty screen; its property names mirror the view's bindings so the designer highlights drift.
- Design stubs are only for `d:DesignInstance` and must never be referenced from runtime code. They may suppress `CS1591` only inside the design-data file, because their members exist for the designer rather than the application API.
- A trivial pass-through view without its own bindings may stay without design data until it grows bindings. Split the design-data file by zone (`Design/List/`, `Design/Dialogs/`, ...) once one file stops being readable.

### Observable properties

- `[ObservableProperty]` fields stay together with the rest of the state; the matching `partial void On…Changed` handlers live in one block after the commands and regular methods, in the same order as their fields. Do not interleave handlers with fields — the source generator already places the generated property half next to the field, and a block of handlers is easier to scan than field/method alternation.

Do not repeat the XAML-declared base type in code-behind: the `x:Class` root element already fixes the base (`Window`, `UserControl`, `Application`), so `public partial class Foo` suffices — `: Window` in `.xaml.cs` is redundant.

Prefer Blend behaviors (`Microsoft.Xaml.Behaviors.Wpf`) over code-behind for view mechanics repeated across views (borderless-window drag, dialog-result mirroring): one behavior declared in XAML instead of copy-pasted handlers. Single-use mechanics intimate to one view (splitter sync, editor caret timing, caption buttons) stay in code-behind.

XML documentation is mandatory for public APIs (`CS1591` is an error); test projects are exempt (`NoWarn`).

Do not abbreviate words in identifiers, file names, or documentation — write `ViewModel`, not `VM`; `Configuration`, not `Config`; and so on for any other term, unless the abbreviation is itself the established, unambiguous full name of a real-world thing (e.g. `Xml`, `Guid`, `Id`, `Uri`) rather than a shortening the author chose for brevity.

Use `global using` sparingly and rationally: a namespace belongs in a project-level `GlobalUsings.cs` only when most files of that project need it (the test framework, shared test doubles, pipeline module plumbing) — a couple of entries per project at most. Everything else stays a local `using`, and namespaces already covered by that project's `ImplicitUsings` stay out of both. Note the implicit set is smaller for WPF projects (`UseWPF` drops `System.IO`/`System.Net.Http`, among others — verify via `obj/*/*.GlobalUsings.g.cs`), so those stay explicit there.

## Release changelog

Before creating a release tag `vMAJOR.MINOR.PATCH`, compose a new `## [MAJOR.MINOR.PATCH] - YYYY-MM-DD` section directly below the (empty) `## [Unreleased]`, from the changes since the previous stable tag (for example `git log <previous-tag>..HEAD`, using the Conventional Commit types and scopes). Commit that change and create the tag on that commit, so the changelog at the tagged commit names the released version and carries no unreleased entries. While composing the section, keep changes a user of that release can observe, merge related entries, and drop fixes for behavior that was never released. The publish-release workflow enforces this before building: it refuses to publish when `## [Unreleased]` contains any entry or when the changelog has no section for the tagged version. Empty category headings under `Unreleased` are tolerated. The version section is published verbatim as the GitHub release description, so write it for users.

## .NET SDK selection

Use the repository-root `global.json` for local builds and CI: SDK `10.0.100` or a later stable SDK in the `10.0` major/minor line (`rollForward: latestFeature`, `allowPrerelease: false`). Do not roll forward to another major/minor line without updating this policy and `global.json` together. GitHub Actions setup steps must read `global-json-file: global.json` after checkout. The `test` section of `global.json` selects the Microsoft.Testing.Platform runner for `dotnet test`.

## Solution items

Keep the root solution in sync with existing repository-level documentation, license, Git/editor settings, SDK/NuGet/versioning and shared MSBuild configuration, and root maintenance scripts. Expose root files under `solutionItems`, `.github/` and `scripts/` under matching subfolders, and documentation under `docs/` with its directory hierarchy. Add, rename or remove solution links together with the corresponding files. Include only existing files, once each; exclude local settings, secrets and generated output.

## Repository validation

The published Wiki Home page is generated from `docs/WIKI.md`. Edit its source in
the repository; manual edits to Wiki Home are replaced by the publishing workflow.
Other wiki pages are not managed by this workflow.

Run `./scripts/Validate-Repository.ps1` after changing repository-level files,
documentation or the solution. CI runs the same validator. It checks the required
files and navigation links and verifies that managed repository files are present
exactly once in Solution Items. Update the solution together with added, renamed or
removed managed files.

## Formatting baseline

Keep the root `.editorconfig` in the solution and apply its repository-wide encoding, line-ending and trailing-whitespace rules. Update the file deliberately when formatting conventions change; do not replace specialized rules with the shared minimum.

Private fields follow one grouping rule: fields of the same logical group (constants, injected dependencies, mutable state, caches) stay contiguous with no blank lines between them, while exactly one blank line separates the groups and precedes a field's XML-doc/attribute block; never leave a blank line immediately after `{` or before `}`. The Roslyn formatter does not enforce this, so treat it as a review convention and follow the existing classes (`MainViewModel`, `FileSystemAddinStore`, `FileSystemAddinChangeWatcher`) as the reference.

## Warnings

Treat compiler and analyzer warnings as errors-in-waiting: do not leave new warnings in code you touch. Fix them as part of the same change; when a warning is a deliberate false positive, suppress it explicitly with a nearby comment explaining why rather than letting it accumulate silently. CI builds with `-warnaserror`, so a new warning blocks the build; treat a local warning the same way.

## Test execution

Projects using xUnit v3 run through Microsoft.Testing.Platform as test executables (`OutputType: Exe` is required; do not reference `Microsoft.NET.Test.Sdk` — it conflicts with the MTP runner on .NET 10 SDK). CI builds the solution and runs each headless `*.Tests` executable explicitly, treating a run with no discovered tests as a failure.

## Logging

Log significant lifecycle steps and every caught exception, exactly once:
- Lifecycle: application start (with version) and shutdown at Information; every
  user-meaningful disk mutation (scan results, move/delete/create/save, with file
  identity) at Information, routine per-file scan details at Debug; refusals
  (Revit running, already exists, validation) at Warning.
- Exceptions: log where the failure is first known — user-visible failures at Warning,
  disk/data failures at Error with the exception instance. Callers up the chain surface
  only the message (UI banners) and stay silent; never log the same instance twice
  (wrap-and-rethrow with the original as `InnerException`, don't `throw;` an
  already-logged one).
- Benign documented fallbacks stay silent with an explanatory comment: `Try*` probes,
  corrupt-settings → default, per-process read failures, designer fallbacks, and
  cancellation-as-control-flow.
- Structured logging with named placeholders, never interpolation into the template.
- Adapters, decorators and wrappers do not log an exception they rethrow; log once where the failure is first understood and handled, then surface only the message.
- User cancellation is control flow, not a failure: log it at Debug or stay silent, never Warning/Error.
- Every user-visible exception message comes from `IStringLocalizer` resources; the technical `InnerException` may stay in its original language and is never shown as the primary message.

## Manual UI checks

XAML compiling is not evidence that a UI change works. A UI change must be smoke-checked manually before it is reported done: open the affected menu/dialog, check focus and keyboard paths (`Enter`, `Esc`, `F2`, `Ctrl+Z`), switch theme and language, and confirm the empty/disabled/conflict states. Automatic startup update checks must not block local checks: Debug builds skip the startup update dialog, while the manual `Ctrl+U` check still works.
