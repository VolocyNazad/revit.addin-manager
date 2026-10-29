# Contributing

Before making a change, read the [development policy](docs/policies/development.md)
and the [repository guide](docs/repository.md). They define the repository-specific
stack, testing expectations and maintenance rules.

Branches follow the [branching policy](docs/policies/branching.md): `main` holds
the released state, `dev` integrates the next release, and every change arrives
through a short-lived `feature/`, `fix/`, `release/` or `hotfix/` branch and a
pull request.

Keep changes focused. Regular development leaves `## [Unreleased]` in
[CHANGELOG.md](CHANGELOG.md) empty: the release section is composed from the changes
since the previous stable tag when a release is prepared. See the
[release changelog policy](docs/policies/development.md#release-changelog).

Before submitting a pull request, check the items that apply to the change:

- Add or update tests for behaviour changes. Ask first when existing test expectations
  change, when a test needs real disk/OS side effects, or when coverage scope is
  genuinely ambiguous. UI changes still need the manual smoke check from the policy.
- Build the affected projects and solutions, including `build/Revit.AddinManager.Build.slnx`
  and the installer generator when they are touched. Resolve new compiler and analyzer
  warnings in touched code.
- Review compatibility with saved settings, `.addin` files and upgrade paths when those
  are affected.
- Update user documentation and localized strings (`*.resx` plus the `.ru.resx` pair)
  when user-visible behaviour or text changes.
- Review logging for significant operations, refusals and failures when behaviour
  changes; follow the [logging policy](docs/policies/development.md#logging).
- Update repository documentation when the development policy requires it, and compose
  the changelog section only when preparing a release.
- Confirm relevant CI checks pass before merging.

## Running checks

Build and run the headless tests locally:

```powershell
dotnet build Revit.AddinManager.slnx --configuration Release /nologo -warnaserror
```

The test projects use xUnit v3 through Microsoft.Testing.Platform and are executables,
so `dotnet test` is not used. Run each `*.Tests.exe` in its build output, or rely on the
same explicit discovery the `build-and-test` workflow uses.

Check the release tooling too when it changed:

```powershell
dotnet build build/Revit.AddinManager.Build.slnx --configuration Release /nologo -warnaserror
dotnet build installer/Revit.AddinManager.Installer/Revit.AddinManager.Installer.csproj --configuration Release /nologo -warnaserror
```

Run the repository policy validator after changing repository-level files,
documentation or the solution:

```powershell
./scripts/Validate-Repository.ps1
```

## Before publishing a release

- Verify the release tag and version against the release commit.
- Compose the new `MAJOR.MINOR.PATCH` section from the changes since the previous
  stable tag and leave `Unreleased` empty, as described in the
  [release changelog policy](docs/policies/development.md#release-changelog).
- Confirm CI succeeds and inspect the release build, installer and published
  artifacts when the release pipeline completes.
