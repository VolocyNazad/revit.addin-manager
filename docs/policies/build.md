# Build and toolchain policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: changing the SDK, a build unit, a package version or a warning rule.

A build is reproducible from a clean checkout with the SDK selected by the repository and the package
versions declared in the repository. This policy states rules, not build recipes: commands and stack
facts belong to the [repository guide](../repository.md).

## SDK selection

- Take the .NET SDK from the repository-root `global.json`; it is the only source of the SDK for local
  builds, CI and release publishing.
- Keep `global.json` on a stable SDK `10.0.100` or later in the `10.0` major/minor line
  (`rollForward: latestFeature`, `allowPrerelease: false`). Do not move to another major/minor line
  without changing this policy and `global.json` in the same change.
- Read the SDK in every GitHub Actions setup step with `global-json-file: global.json` after
  checkout. Do not install a floating or explicitly versioned SDK in the workflow instead.
- This policy does not change project target frameworks; a target-framework change updates the
  [repository guide](../repository.md) with the stack facts.

## Build units

- The root solution `Revit.AddinManager.slnx` builds the launcher and its tests. `installer/` and
  `build/` are independent build units with their own solutions and their own package versions; build
  each of them separately and do not expect the root build to cover them. CI compiles both in the
  `build-tooling` job.
- `sandbox/` is a separate solution that is not part of the product. CI compiles it so the playground
  cannot rot, but it is never packaged or published.
- A new build unit arrives with its own solution, its own `Directory.Build.props` and
  `Directory.Packages.props`, and its own CI job. A project that needs another build unit's files
  takes a source link or a package, never a reference across units.
- Share product identity by compiling the `ProductIdentity` source file through a source link in
  independent build units instead of adding a project reference.

## Package versions

- Pin every package version in the nearest `Directory.Packages.props`. The root file serves the root
  solution and `sandbox/`; `installer/` and `build/` have their own and use only those.
- Keep central package management enabled (`ManagePackageVersionsCentrally=true`); a
  `PackageReference` in a project carries no `Version`.
- Add repository-wide analyzer packages through `GlobalPackageReference` with private assets. Do not
  repeat them per project.
- Change a package version in one place only. When a project resolves a version you did not expect,
  look for a nearer props file before editing the project.
- A package that no project references and a test runner package that the chosen test runner does not
  use are dead entries: remove them instead of leaving them for a future reader to trust.

## Warnings

- Treat compiler and analyzer warnings as delayed errors. Do not leave new warnings in code you touch;
  fix them in the same change.
- Suppress a deliberate false positive as narrowly as possible and add a nearby comment explaining
  why.
- The pull request builds the root solution with `-warnaserror`, so a warning fails the change.
- The release pipeline compiles the launcher without that switch (`build/Modules/CompileProjectModule.cs`
  passes `-p:Version` and disables symbols and documentation, not warnings), so a warning is caught by
  the pull request build, not by the release. Do not assume the publish run will surface one.
- Build the configuration you touch locally before opening the pull request; do not rely on the release
  pipeline to report a warning.

The rest of the contributor workflow is defined by the [development policy](development.md).
