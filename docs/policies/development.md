# Development policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: making any change to this repository. This is the entry point; the policy index routes
the rest.

The stack described in the [repository guide](../repository.md) is used by default and takes priority over
whatever the agent might choose on its own. Prefer whatever is already used
in the affected part of the repository over an alternative. If an alternative
seems better — don't substitute it silently: ask the user,
explain the reason, and wait for confirmation.

If a change affects the folder structure or the technology stack (a new/removed project, a new dependency, a version bump worth recording, a new convention) — update the relevant policy and [repository guide](../repository.md) as part of the same change, not as a separate follow-up later.

Tests for behavior changes and bug fixes are expected by default, as the [testing policy](testing.md)
requires. Ask the user first when existing test expectations change, when a test needs real disk/OS
side effects (integration), or when coverage scope is genuinely ambiguous.

Commit messages follow the [commit message policy](commits.md).

Agents must not `git push`: commit locally and leave the push to the user, unless they explicitly ask
you to push. A human contributor pushes the branch as the [branching policy](branching.md) describes.

Branching follows the [branching policy](branching.md); a change reaches `dev` and `main` through a
pull request as the [pull request policy](pull-requests.md) requires.

A user-visible change carries its changelog entry as one fragment file in
`changelog.d/<category>/<slug>.md`, as the [changelog policy](changelog.md) requires; `## [Unreleased]`
stays empty during regular development.

## Abstractions and composition

All services live behind abstractions: domain and headless contracts are owned by Core (`AddinManager.Core.Abstractions`); UI-specific contracts (dialogs, clipboard, folder opener, editor dirty state) live in the owning UI project's `Abstractions/` tree (`AddinManager.Launcher.Abstractions.Services`) and may use Core types. Core must not reference WPF or Windows-only UI APIs. Consumers depend only on interfaces, implementations are resolved through DI and are swappable without touching consumers. `new` on an implementation is allowed only in the composition root and in tests.

Abstractions live in a top-level `Abstractions/` folder mirroring the implementation layout (`Abstractions/Storage/IAddinStore.cs` for `Storage/FileSystemAddinStore.cs`, namespaces follow folders) — the same mirroring as localized resources follow their owning types. Folder and namespace both carry the `Abstractions` segment a separate `*.Abstractions` project would use (`AddinManager.Core.Abstractions`, ...), so extracting them later is a pure file move with no renames, no using churn, and no doc-`cref` churn.

### Constructors

- Use a primary constructor when the type only stores its dependencies (`public sealed class FileOperations(...)`); it keeps the dependency graph visible in the type declaration and avoids boilerplate that hides it.
- Keep an explicit constructor when it does real work beyond assignment: argument validation, computed fields, optional parameters with guarded/derived defaults, multiple overloads, or calling `base(...)` with transformed arguments. WPF views keep their parameterless constructor for `InitializeComponent` and may add a DI constructor next to it.
- Do not expose constructor parameters as public mutable properties; capture them in private readonly fields or private properties.
- Do not generate constructors with source generators (`AutoConstructor`-style attributes): the project already writes primary constructors directly, generated constructors would hide the graph from review, and a generated public constructor would still have to satisfy `CS1591`.

Optional dependencies are a test seam, not a production policy: the composition root must always supply the real implementation. A fallback that silently changes user-visible safety (for example, permanent delete instead of the Recycle Bin) is forbidden; if a fallback is unavoidable, it must be explicit, documented and logged, as the [logging policy](logging.md) requires.

## WPF composing rules

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

Declare a WPF dependency property through `VolocyNazad.Wpf.DependencyPropertyGenerator` (`[GenerateDependencyProperty<T>]` on a control or behavior, `[GenerateAttachedDependencyProperty<T>]` on a static class) instead of a hand-written `DependencyProperty.Register` call: the generator emits the registration, the CLR property or the `Get`/`Set` accessors, the XML documentation and the callback wiring, and a callback with no supported signature fails the build. Keep hand-written what the generator cannot express: `AddOwner`, `OverrideMetadata`, private properties that only hold state for another property, and a type's own `OnPropertyChanged` override.

## Naming and usings

Do not abbreviate words in identifiers, file names, or documentation — write `ViewModel`, not `VM`; `Configuration`, not `Config`; and so on for any other term, unless the abbreviation is itself the established, unambiguous full name of a real-world thing (e.g. `Xml`, `Guid`, `Id`, `Uri`) rather than a shortening the author chose for brevity.

Use `global using` sparingly and rationally: a namespace belongs in a project-level `GlobalUsings.cs` only when most files of that project need it (the test framework, shared test doubles, pipeline module plumbing) — a couple of entries per project at most. Everything else stays a local `using`, and namespaces already covered by that project's `ImplicitUsings` stay out of both. Note the implicit set is smaller for WPF projects (`UseWPF` drops `System.IO`/`System.Net.Http`, among others — verify via `obj/*/*.GlobalUsings.g.cs`), so those stay explicit there.

## Related policies

- [Documentation policy](documentation.md) — where a fact, rule or procedure belongs, and XML documentation.
- [Repository maintenance policy](repository-maintenance.md) — solution items, required files, the validator, formatting.
- [Build and toolchain policy](build.md) — SDK selection, build units, warnings.
- [Testing policy](testing.md) — what needs a test, how tests run, manual smoke checks; [benchmark policy](benchmarks.md) for performance work.
- [Logging policy](logging.md) — what to log, at which level, once.
- [Changelog policy](changelog.md), [release policy](releases.md) — entries and releases.
- [Wiki policy](wiki.md) — user documentation publication.
- [Pull request policy](pull-requests.md) — what a pull request must contain.
