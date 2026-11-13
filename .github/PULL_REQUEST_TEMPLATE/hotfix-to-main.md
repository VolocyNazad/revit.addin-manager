## Summary

<!-- The production defect, its impact on users and the version that fixes it. Link the issue. -->

## Branch

- [ ] The source branch is `hotfix/<version>`, cut from `main`, as the
  [branching policy](../../docs/policies/branching.md) requires.
- [ ] The pull request targets `main` and contains only the fix for this defect.
- [ ] `main` is not changed by a direct push.

## Release

- [ ] A regression test fails without the fix and passes with it, as the
  [testing policy](../../docs/policies/testing.md) requires.
- [ ] The build is green for Debug and Release, with warnings as errors, as the
  [release policy](../../docs/policies/releases.md) requires.
- [ ] `./scripts/Update-Changelog.ps1 -Compose` collected the fragments and the composed entries
  were edited as one narrative.
- [ ] `./scripts/Update-Changelog.ps1 -Finalize <version>` closed the section and deleted the
  fragments of `changelog.d/`.
- [ ] `## [Unreleased]` is empty and `changelog.d/` holds no fragment.
- [ ] `./scripts/Verify-Release.ps1 -FromHeadTag -AncestorOf main` passes on the tagged commit.
- [ ] The tag is created on the merge commit after this pull request is merged; the commit carries
  exactly one stable `vMAJOR.MINOR.PATCH` tag, as the [release policy](../../docs/policies/releases.md)
  requires.
- [ ] The fix reaches `dev`, or the open release branch when there is one, through its own pull
  request; without that step the next release drops the fix. The branch is deleted after the merges.

## Checklist

- [ ] Build and tests for the affected projects pass, with no new compiler or analyzer warnings.
- [ ] `./scripts/Validate-Repository.ps1` passes after changing repository-level
  files, documentation or the solution.
- [ ] `./scripts/Verify-Release.ps1` passes after changing `CHANGELOG.md`.
- [ ] User documentation and localized strings (`*.resx` with the `.ru.resx` pair) are updated when
  user-visible behaviour or text changes.
- [ ] Saved settings, `.addin` files and upgrade paths are reviewed when the change affects them.
- [ ] Logging was reviewed for significant operations, refusals and failures.
