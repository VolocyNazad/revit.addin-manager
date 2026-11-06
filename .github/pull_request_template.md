## Summary

<!-- What changed and why. Link the issue when there is one. -->

## Branch

- [ ] The source branch follows the [branching policy](../docs/policies/branching.md):
  `feature/<ticket>-<slug>`, `fix/<ticket>-<slug>` or `chore/<slug>` from `dev`,
  `release/<version>` from `dev`, or `hotfix/<version>` from `main`.
- [ ] The pull request targets `dev` for a feature, a fix or a chore, and `main` for a
  release or a hotfix. A hotfix is also merged back into `dev` or into the open release
  branch.
- [ ] `main` and `dev` are not changed by a direct push.

## Checklist

- [ ] Build and tests for the affected projects pass.
- [ ] `./scripts/Validate-Repository.ps1` passes after changing repository-level
  files, documentation or the solution.
- [ ] `./scripts/Verify-Release.ps1` passes after changing `CHANGELOG.md`.
- [ ] The change carries its entry as a fragment in `changelog.d/` when it is
  user-visible, as the [changelog policy](../docs/policies/changelog.md) requires.
