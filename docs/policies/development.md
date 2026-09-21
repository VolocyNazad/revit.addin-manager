# Development policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

The stack described in [Repository guide](../repository.md) is used by default and takes priority over
whatever the agent might choose on its own. Prefer whatever is already used
in the affected part of the repository over an alternative. If an alternative
seems better — don't substitute it silently: ask the user,
explain the reason, and wait for confirmation.

If a change affects the folder structure or the technology stack (a new/removed project, a new dependency, a version bump worth recording, a new convention) — update the relevant policy and [repository guide](../repository.md) as part of the same change, not as a separate follow-up later.

Before changing existing tests or writing new ones, ask the user first — clarify what and how it should be covered (or that the change is trivial enough that no clarification is needed), rather than deciding this on your own.

Commit messages follow Conventional Commits (`<type>(<scope>): <description>`, e.g. `feat(manifest): ...`, `fix(...): ...`, `docs(agents): ...`, `test(...): ...`, `chore(...): ...`, `refactor(...): ...`) — the scope is optional but preferred when it clarifies what exactly changed.

Do not `git push` — commit locally and leave the push to the user, unless they explicitly ask you to push.

Keep `CHANGELOG.md` up to date, in [Keep a Changelog](https://keepachangelog.com/en/1.1.0/) format — add an entry under `## [Unreleased]` (in the appropriate category: Added/Changed/Fixed/Removed/...) as part of the same change, not as a separate follow-up later.

All services live behind abstractions owned by Core: consumers depend only on interfaces, implementations are resolved through DI and are swappable without touching consumers. `new` on an implementation is allowed only in the composition root and in tests.

Abstractions live in a top-level `Abstractions/` folder mirroring the implementation layout (`Abstractions/Storage/IAddinStore.cs` for `Storage/FileSystemAddinStore.cs`, namespaces follow folders) — the same mirroring as localized resources follow their owning types. Folder and namespace both carry the `Abstractions` segment a separate `*.Abstractions` project would use (`AddinManager.Core.Abstractions`, ...), so extracting them later is a pure file move with no renames, no using churn, and no doc-`cref` churn.

Avoid code-behind in WPF views: prefer bindings, converters and DynamicResource over C# in `.xaml.cs`. Code-behind is for view mechanics with no declarative equivalent (window chrome, splitter drag, reading `ActualWidth`, and similar) — never for state. If it's unclear whether a declarative way exists, ask the user before writing code-behind instead of defaulting to it.

Do not repeat the XAML-declared base type in code-behind: the `x:Class` root element already fixes the base (`Window`, `UserControl`, `Application`), so `public partial class Foo` suffices — `: Window` in `.xaml.cs` is redundant.

Prefer Blend behaviors (`Microsoft.Xaml.Behaviors.Wpf`) over code-behind for view mechanics repeated across views (borderless-window drag, dialog-result mirroring): one behavior declared in XAML instead of copy-pasted handlers. Single-use mechanics intimate to one view (splitter sync, editor caret timing, caption buttons) stay in code-behind.

XML documentation is mandatory for public APIs (`CS1591` is an error); test projects are exempt (`NoWarn`).

Do not abbreviate words in identifiers, file names, or documentation — write `ViewModel`, not `VM`; `Configuration`, not `Config`; and so on for any other term, unless the abbreviation is itself the established, unambiguous full name of a real-world thing (e.g. `Xml`, `Guid`, `Id`, `Uri`) rather than a shortening the author chose for brevity.

Use `global using` sparingly and rationally: a namespace belongs in a project-level `GlobalUsings.cs` only when most files of that project need it (the test framework, shared test doubles, pipeline module plumbing) — a couple of entries per project at most. Everything else stays a local `using`, and namespaces already covered by that project's `ImplicitUsings` stay out of both. Note the implicit set is smaller for WPF projects (`UseWPF` drops `System.IO`/`System.Net.Http`, among others — verify via `obj/*/*.GlobalUsings.g.cs`), so those stay explicit there.

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

## Warnings

Treat compiler and analyzer warnings as errors-in-waiting: do not leave new warnings in code you touch. Fix them as part of the same change; when a warning is a deliberate false positive, suppress it explicitly with a nearby comment explaining why rather than letting it accumulate silently.

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
