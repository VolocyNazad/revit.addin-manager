# Pull request policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: preparing, reviewing or merging a pull request.

Every change reaches `main` and `dev` through a short-lived branch and a pull request. The
[branching policy](branching.md) owns branch kinds, base branches, merge strategy and protection
settings. The template is `.github/pull_request_template.md`; the required checks are `CI gate` from
the `build-and-test` workflow and the `repository-policy` workflow.

## Source branch and target

| Source branch | Cut from | Pull request targets |
| --- | --- | --- |
| `feature/<ticket>-<slug>` | `dev` | `dev` |
| `fix/<ticket>-<slug>` | `dev` | `dev` |
| `chore/<slug>` | `dev` | `dev` |
| `release/<version>` | `dev` | `main` |
| `hotfix/<version>` | `main` | `main` |

- Branch names, base branches and lifetimes are defined in the [branching policy](branching.md); the
  table is the pull request view of that contract.
- A hotfix is also merged back into `dev`, or into the open release branch when one exists.
- Never push directly to `main` or `dev`. Every change, including a release or hotfix preparation,
  arrives through a pull request that passes review and CI.

## Required checks

- `CI gate` must pass. It aggregates the `build-and-test` matrix and the `build-tooling` job: Debug
  and Release of the root solution are built with warnings as errors and tested through
  `scripts/Invoke-HeadlessTests.ps1`, and `build/`, `installer/` and `sandbox/` are compiled. A
  documentation-only pull request whose files are all under `docs/`, `changelog.d/` or are Markdown
  skips the Windows jobs; the gate stays green.
- `repository-policy` must pass. It runs `./scripts/Validate-Repository.ps1` for the required files,
  links, anchors, Solution Items, action pins and encoding, `./scripts/Update-Changelog.ps1 -Verify`
  for the pending changelog fragments, and `./scripts/Verify-Release.ps1` for the `CHANGELOG.md`
  structure on every push and pull request.
- Merge only after both required workflows pass and the pull request has the required approval.

## Review

- A pull request needs at least one approval.
- Only maintainers merge into `main`; a release or hotfix pull request may use a single reviewer.
- Merge with a merge commit (`--no-ff`, **Create a merge commit**), not with squash or rebase;
  `git revert -m 1 <merge>` reverts the whole change. A squash-merged branch leaves its commits
  unmerged, so the branch cannot be deleted by `git branch -d`.
- Update a branch by merging its target branch into it; do not rebase a branch that has been pushed
  and reviewed, and never force-push `main` or `dev`.

## Author checklist

Before opening a pull request, check every item that applies to the change:

- Assess whether tests are needed; add or update tests, follow the test approval rule in the
  [development policy](development.md), and use the manual smoke check from the
  [testing policy](testing.md) for user-interface changes.
- Build the affected projects and configurations; resolve new compiler and analyzer warnings in
  touched code, as the [build and toolchain policy](build.md) requires.
- Review compatibility with saved settings, `.addin` files and upgrade paths when the change affects
  them.
- Update the user guide and localized strings when user-visible behaviour or text changes, as the
  [documentation policy](documentation.md) requires.
- Review logging for significant operations, refusals and failures when behavior changes, as the
  [logging policy](logging.md) requires.
- Add the changelog fragment in `changelog.d/` when the change is user-visible, following the
  [changelog policy](changelog.md).
- Run `./scripts/Validate-Repository.ps1` after changing repository-level files, documentation or the
  solution.
- Write commit messages as the [commit message policy](commits.md) requires.

## Release and hotfix pull requests

- A `release/<version>` pull request targets `main` and carries only fixes; new features wait for the
  next release. Compose, edit and finalize the release section with `./scripts/Update-Changelog.ps1`
  in the release branch before the tag, as the [changelog policy](changelog.md) requires.
- A `hotfix/<version>` pull request targets `main`. Merge the same branch back into `dev`, or into
  the open release branch when there is one.
- The tag is created on the merge commit on `main` after the pull request is merged; the branch and
  tag steps are in the [branching policy](branching.md) and the publishing steps in the
  [release policy](releases.md).
- Before publishing, confirm CI succeeds and inspect the release build, the installer and the
  published artifacts when the release pipeline completes.

## Template

A pull request uses `.github/pull_request_template.md` and keeps its three sections:

- **Summary** — what changed and why; link the issue when there is one.
- **Branch** — the source branch, its base and target, and confirmation that `main` and `dev` are
  not changed by a direct push.
- **Checklist** — the build and test result for the affected projects,
  `./scripts/Validate-Repository.ps1`, `./scripts/Verify-Release.ps1` after a changelog change, and
  the changelog fragment when the change is user-visible.

The rest of the contributor workflow is defined by the [development policy](development.md).
