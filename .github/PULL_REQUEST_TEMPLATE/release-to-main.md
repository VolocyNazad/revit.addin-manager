## Summary

<!-- The version that ships, what is in it and a link to the release section of CHANGELOG.md. -->

## Branch

- [ ] The source branch is `release/<version>`, cut from `dev`, as the
  [branching policy](../../docs/policies/branching.md) requires.
- [ ] The pull request targets `main` and carries only fixes; a new feature waits for the next
  release.
- [ ] `main` is not changed by a direct push.

## Release

- [ ] The build is green for Debug and Release, with warnings as errors, and the release section of
  `CHANGELOG.md` is committed, as the [release policy](../../docs/policies/releases.md) requires.
- [ ] `./scripts/Update-Changelog.ps1 -Compose` collected the fragments and the composed entries
  were edited as one narrative.
- [ ] `./scripts/Update-Changelog.ps1 -Finalize <version>` closed the section and deleted the
  fragments of `changelog.d/`.
- [ ] `## [Unreleased]` is empty and `changelog.d/` holds no fragment.
- [ ] `./scripts/Verify-Release.ps1 -FromHeadTag -AncestorOf main` passes on the tagged commit.
- [ ] The tag is created on the merge commit after this pull request is merged; the commit carries
  exactly one stable `vMAJOR.MINOR.PATCH` tag, as the [release policy](../../docs/policies/releases.md)
  requires.
- [ ] The release branch is merged back into `dev` and deleted after the merge.

## Checklist

- [ ] Build and tests for the affected projects pass, with no new compiler or analyzer warnings.
- [ ] `./scripts/Validate-Repository.ps1` passes after changing repository-level
  files, documentation or the solution.
- [ ] `./scripts/Verify-Release.ps1` passes after changing `CHANGELOG.md`.
- [ ] User documentation and localized strings (`*.resx` with the `.ru.resx` pair) are updated when
  user-visible behaviour or text changes.
- [ ] Saved settings, `.addin` files and upgrade paths are reviewed when the change affects them.
- [ ] Logging was reviewed for significant operations, refusals and failures.
