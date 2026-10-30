# Testing policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

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

Headless: `dotnet test tests/<Project>.Tests/<Project>.Tests.csproj --configuration Release`,
or the built executable, which is what CI may use.

## Manual smoke check

For a change that affects the user interface or the command line, start the product, run the
main scenario, repeat the file operation that the change touches and confirm that nothing is
left in a broken state. A library without a UI has no manual step.

## Flaky and skipped tests

A test that fails intermittently is fixed or quarantined with an issue and a date. A skipped
test has a reason and an issue; an unexplained skip is a defect.

## Coverage

Every behaviour a consumer of the library or tool can observe has a test. No numeric threshold
is enforced: a percentage that moves without a behaviour change is not a goal.

The workflow around tests - branches, commit messages and the changelog - is defined by the
[development policy](development.md).
