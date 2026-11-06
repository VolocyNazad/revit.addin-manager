# Testing policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: adding tests, choosing a test kind, running a suite, or changing a user-interface
behaviour that needs a manual check.

Tests are the evidence that a change works. The repository separates them by what they need
to run: a normal process, or a person in front of the product.

## Test kinds

| Kind | Project suffix | Runs in | May touch |
| --- | --- | --- | --- |
| Headless | `*.Tests` | a normal process, including a hosted runner | pure code, temporary files, in-memory state |
| Manual smoke | - | the product, by a person | window, dialog, command line, file system |

The suffix is a contract: CI selects tests by it, and a project without `*.Tests` is not a
test project. Prefer the product prefix as well (`<Product>.Tests`), but the suffix is what
matters.

## What needs a test

- A bug fix comes with a regression test that fails without the fix.
- A behaviour change comes with a test of the new behaviour, not only proof that nothing
  crashes.
- MSBuild and SDK logic is tested on a temporary project.
- Analyzers and generators are tested through the diagnostic or the generated code.
- Code that compiles differently per build configuration is tested on each of those
  configurations.

Documentation, formatting, dependency updates and purely visual changes do not need a test,
but the build must pass.

## Isolation and determinism

- Tests do not use the network and do not write into the repository; temporary data goes to a
  temporary directory.
- No dialogs and no waiting for input in any test.
- A test does not depend on execution order, locale, registry state or the machine it runs on.

## Running the tests

A test project is an executable xUnit v3 runner that targets Microsoft.Testing.Platform, so both
`dotnet test` and running the built executable work. CI takes the script path, because it is the same
locally and on the runner and it guarantees the report and the failure rules:

```powershell
dotnet build Revit.AddinManager.slnx --configuration Release /nologo -warnaserror
./scripts/Invoke-HeadlessTests.ps1 -Configuration Release
```

`dotnet test Revit.AddinManager.slnx --no-build` is a convenience for a local run; CI uses the script
because it also enforces the report and the failure rules the same way on every machine.

The script selects the executable built for the configuration, writes one TRX report per project
under `TestResults/<Configuration>/`, and fails when a project has no executable, when an executable
exits with a non-zero code, or when a run discovers no tests. A single project can be run directly
from its build output with the same runner arguments (`-noLogo -noColor -result-trx <path>`).

## Test infrastructure

- A test project is built as an executable (`OutputType: Exe` is required) and targets
  Microsoft.Testing.Platform, selected by the `test` section of `global.json`; that is how
  `dotnet test` discovers it. Do not reference `Microsoft.NET.Test.Sdk` or the VSTest adapter
  (`xunit.runner.visualstudio`): with the Microsoft.Testing.Platform runner they are dead weight.
- CI builds the solution and runs `scripts/Invoke-HeadlessTests.ps1`; a test project that is not
  executed by that script is not covered by CI.
- Shared test helpers live in `tests/Shared/` and are linked into the test projects that need them,
  so a helper has one source.
- The test selection is a contract: a project whose name does not end in `*.Tests` is invisible to CI.

## Manual smoke check

A UI change is not done because the XAML compiles. For a change that affects the user interface,
start the product, run the main scenario and repeat the file operation that the change touches:
open the affected menu or dialog, check focus and keyboard paths (`Enter`, `Esc`, `F2`, `Ctrl+Z`),
switch theme and language, and confirm the empty, disabled and conflict states. Confirm that nothing
is left in a broken state. Automatic startup update checks must not block local checks: Debug builds
skip the startup update dialog, while the manual `Ctrl+U` check still works. A library without a UI
has no manual step.

## Flaky and skipped tests

A test that fails intermittently is fixed or quarantined with an issue and a date. A skipped
test has a reason and an issue; an unexplained skip is a defect.

## Coverage

Every behaviour a consumer of the library or tool can observe has a test. No numeric threshold
is enforced: a percentage that moves without a behaviour change is not a goal.

The workflow around tests - branches, commit messages and the changelog - is defined by the
[development policy](development.md).
