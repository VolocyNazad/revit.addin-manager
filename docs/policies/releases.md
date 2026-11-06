# Release policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: preparing a release tag, version or MSI.

A release turns a tagged commit into a GitHub release with an installer. The workflow is
`.github/workflows/publish-release.yml` and the publish modules are under `build/`; the pipeline
consumes the tag and the release section of `CHANGELOG.md`. The [changelog policy](changelog.md) owns
that section's format, the [branching policy](branching.md) owns release and hotfix branches, and the
[testing policy](testing.md) owns what must be green before the tag.

## Release channels

- A **stable release** is a `vMAJOR.MINOR.PATCH` tag and the only channel the launcher's update check
  offers.
- A **pre-release** would use a suffix such as `vMAJOR.MINOR.PATCH-beta.1`. Current automation
  publishes stable releases only; publishing a pre-release is a manual decision that extends the
  pipeline first, not a flag that sneaks into a stable release.
- Never publish a stable version as a GitHub pre-release and never give a pre-release a stable version
  number: the launcher reads releases through `/releases/latest`, which skips drafts and
  pre-releases.

## Versioning

- GitVersion runs in Continuous Delivery mode with patch increments. The tag is the version source: a
  release commit carries exactly one stable `vMAJOR.MINOR.PATCH` tag.
- The build uses the tagged version without the `v` prefix for the binaries and the MSI. The MSI
  product version is the numeric `MAJOR.MINOR.PATCH`; the assemblies keep the GitVersion semantic
  version.
- The repository publishes a GitHub release with an MSI instead of a NuGet package.

## Tags

- The tag is created only on a merge commit on `main`, after the release pull request is merged. The
  publish workflow requires exactly one stable tag on the selected commit and that the commit is an
  ancestor of `main`; it refuses otherwise.
- Never delete, move or force-push a pushed release tag. A pushed tag is immutable; publish the next
  version instead. If a mistake is found before the tag is pushed and no release was published, fix
  the commit, move the local tag and push it once.
- Only the stable release has a `CHANGELOG.md` version section; pre-release notes never create a
  permanent changelog section.

## Preparing a release

Before creating the tag:

- Compose and finalize the release section with `./scripts/Update-Changelog.ps1` and confirm that
  `changelog.d/` holds no fragment and `## [Unreleased]` is empty, as the [changelog policy](changelog.md)
  requires.
- Confirm the build is green for Debug and Release, the working tree is clean and the release
  changelog section is committed.
- Confirm `repository-policy` passed on the release pull request and run
  `./scripts/Verify-Release.ps1 -FromHeadTag -AncestorOf main` on the tagged commit.
- Verify the release tag and version against the release commit.

## Release artifacts and MSI

- Build and publish the launcher and its installer from the `build/` pipeline; do not hand-build the
  artifact that is published.
- Produce one per-machine MSI installed under `%ProgramFiles%\VolocyNazad\Revit.AddinManager`, whose
  asset name matches the product identity helper (`RevitAddinManager-<version>.msi`) and whose
  `UpgradeCode` stays stable across releases. The MSI accepts only a numeric `MAJOR.MINOR.PATCH`
  version, which is also the product version Windows shows.
- The installer installs the launcher; it does not add, enable or edit `.addin` manifests, because
  managing those is the product's job.
- After the pipeline, inspect the release build, the installer and the published artifacts.

## Publish automation

The workflow runs on `workflow_dispatch` with a boolean `draft` input (default `false`); a tag push
alone does not trigger it.

The workflow:

- checks out with full history, installs the SDK from `global.json` and WiX 6.0.2;
- runs `./scripts/Verify-Release.ps1 -FromHeadTag -AncestorOf main`: `HEAD` must carry exactly one
  stable tag matching `vMAJOR.MINOR.PATCH`, `CHANGELOG.md` must have an `## [Unreleased]` section with
  no entries below it, exactly one `## [VERSION] - YYYY-MM-DD` section for the tag directly below it,
  and `HEAD` must be an ancestor of `origin/main`;
- runs `dotnet run --project build/Revit.AddinManager.Build.csproj -c Release -- publish --Publish:Draft=<input>`;
- has a 120-minute job timeout.

The `repository-policy` workflow runs the structural part of the same script on every push and pull
request, so a malformed changelog is reported before the immutable tag exists. The pipeline publishes
the changelog version section as the GitHub release description and attaches the MSI.

The rest of the contributor workflow is defined by the [development policy](development.md).
