<!-- Default template: a feature, fix or chore branch into dev. A release or hotfix pull request uses
     the matching template from .github/PULL_REQUEST_TEMPLATE/; the pull request policy lists them. -->

## Summary

<!-- What changed and why. Link the issue when there is one. -->

## Branch

- [ ] The source branch follows the [branching policy](../docs/policies/branching.md):
  `feature/<ticket>-<slug>`, `fix/<ticket>-<slug>` or `chore/<slug>`, cut from `dev`.
- [ ] The pull request targets `dev`. A `release/<version>` or `hotfix/<version>` pull request is
  opened with its own template instead of this one.
- [ ] `main` and `dev` are not changed by a direct push.

## Checklist

- [ ] Build and tests for the affected projects pass; a change that affects the window or a dialog
  passed the manual smoke check of the [testing policy](../docs/policies/testing.md).
- [ ] `./scripts/Validate-Repository.ps1` passes after changing repository-level
  files, documentation or the solution.
- [ ] `./scripts/Verify-Release.ps1` passes after changing `CHANGELOG.md`.
- [ ] The change carries its entry as a fragment in `changelog.d/` when it is user-visible, as the
  [changelog policy](../docs/policies/changelog.md) requires.
- [ ] User documentation and localized strings (`*.resx` with the `.ru.resx` pair) are updated when
  user-visible behaviour or text changes.
- [ ] Saved settings, `.addin` files and upgrade paths are reviewed when the change affects them.
- [ ] Logging was reviewed for significant operations, refusals and failures.
