# Contributing

Before making a change, read the [development policy](docs/policies/development.md) and open the
[repository policy index](docs/policies/README.md) for the one policy that covers the area you touch.
The [repository guide](docs/repository.md) is a reference: read the section for that area, not the whole file.

Branches follow the [branching policy](docs/policies/branching.md): `main` holds
the released state, `dev` integrates the next release, and every change arrives
through a short-lived `feature/`, `fix/`, `chore/`, `release/` or `hotfix/` branch and a
pull request. The [pull request policy](docs/policies/pull-requests.md) defines what a pull
request must contain.

Commit messages follow the [commit message policy](docs/policies/commits.md).

Tests follow the [testing policy](docs/policies/testing.md): `*.Tests` projects are headless
executables that run in CI, and a change that affects the window or a dialog needs the manual
smoke check from that policy.

Keep changes focused. A change carries its changelog entry as one fragment in
`changelog.d/`, in the same branch as the change; regular development leaves
`## [Unreleased]` in [CHANGELOG.md](CHANGELOG.md) empty, and the release section is composed
when a release is prepared. See the [changelog policy](docs/policies/changelog.md).

Before submitting a pull request, check the items that apply to the change:

- Add or update tests for behaviour changes. Ask first when existing test expectations
  change, when a test needs real disk/OS side effects, or when coverage scope is
  genuinely ambiguous. UI changes still need the manual smoke check from the policy.
- Build the affected projects and solutions, including `build/Revit.AddinManager.Build.slnx`,
  the installer generator and `sandbox/AddinManager.Sandbox.slnx` when they are touched.
  Resolve new compiler and analyzer warnings in touched code.
- Review compatibility with saved settings, `.addin` files and upgrade paths when those
  are affected.
- Update user documentation and localized strings (`*.resx` plus the `.ru.resx` pair)
  when user-visible behaviour or text changes.
- Review logging for significant operations, refusals and failures when behaviour
  changes; follow the [logging policy](docs/policies/logging.md).
- Update the user guide and its repository links when user documentation changes, as the
  [wiki policy](docs/policies/wiki.md) requires.
- Add the changelog fragment when the change is user-visible.
- Confirm relevant CI checks pass before merging.

## Running checks

Build and run the headless tests locally:

```powershell
dotnet build Revit.AddinManager.slnx --configuration Release /nologo -warnaserror
./scripts/Invoke-HeadlessTests.ps1 -Configuration Release
```

The test projects are xUnit v3 executables that target Microsoft.Testing.Platform, so `dotnet test`
also works; the script is the path CI takes, because it selects each built `*.Tests.exe`, writes a
TRX report per project and fails on a run without tests.

Check the release tooling too when it changed:

```powershell
dotnet build build/Revit.AddinManager.Build.slnx --configuration Release /nologo -warnaserror
dotnet build installer/Revit.AddinManager.Installer/Revit.AddinManager.Installer.csproj --configuration Release /nologo -warnaserror
dotnet build sandbox/AddinManager.Sandbox.slnx --configuration Release /nologo -warnaserror
```

Run the repository checks after changing repository-level files, documentation, the solution, a
workflow or the changelog:

```powershell
./scripts/Validate-Repository.ps1
./scripts/Update-Changelog.ps1 -Verify
./scripts/Verify-Release.ps1
```

## Before publishing a release

- Verify the release tag and version against the release commit.
- Compose the pending fragments with `./scripts/Update-Changelog.ps1 -Compose`, edit the collected
  entries and close the section with `-Finalize`, as described in the
  [changelog policy](docs/policies/changelog.md).
- Run `./scripts/Verify-Release.ps1 -FromHeadTag -AncestorOf main` on the tagged commit; the publish
  workflow runs the same check and refuses to build otherwise.
- Confirm CI succeeds and inspect the release build, installer and published
  artifacts when the release pipeline completes.
