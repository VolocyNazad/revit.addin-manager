# Changelog policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: adding a changelog entry, composing the release section, or finalizing a release.

`CHANGELOG.md` follows [Keep a Changelog](https://keepachangelog.com/en/1.1.0/): the newest
release comes first, `## [Unreleased]` stays on top, and every release has one
`## [VERSION] - YYYY-MM-DD` section. Empty category headings under `Unreleased` are
tolerated.

## Fragments

A change carries its entry as one fragment file in `changelog.d/`, committed in the same branch as
the change. Parallel changes never edit the same lines, so they cannot lose each other's entry or
conflict over `CHANGELOG.md`.

- The category is the subdirectory: `added/`, `changed/`, `deprecated/`, `removed/`, `fixed/` or
  `security/` — the `CHANGELOG.md` headings.
- The file name is a lowercase kebab-case slug that describes the change, such as
  `keep-selected-row-after-refresh.md`. It is not the ticket or pull request number, which does not
  exist yet when the fragment is written.
- The file holds one line: the entry text. Do not add the `- ` marker or a heading; the composer
  adds both. A change that spans several files is still one fragment; a change with two unrelated
  outcomes has two, one per category.
- No file lives outside a category directory.

One fragment per change: write for the consumer of the release, keep what a consumer can observe, and
leave out fixes for behaviour that was never released. `CHANGELOG.md` and `## [Unreleased]` stay
untouched during regular development.

## Tool

`./scripts/Update-Changelog.ps1` keeps the fragments valid and turns them into a release section.

| Command | Does |
| --- | --- |
| `./scripts/Update-Changelog.ps1 -Verify` | checks the category directories, the file names and the content; writes nothing |
| `./scripts/Update-Changelog.ps1 -Compose` | writes the fragments into `## [Unreleased]` under their category headings, in category order and then file-name order; refuses when `Unreleased` already has an entry |
| `./scripts/Update-Changelog.ps1 -Finalize VERSION [-Date YYYY-MM-DD]` | turns the composed entries into `## [VERSION] - YYYY-MM-DD`, leaves a new empty `## [Unreleased]` above it and deletes the fragments |

Composing and editing are separate steps. After `-Compose`, a human reads the collected text as one
narrative: merge related entries, drop what a consumer cannot observe and fix the wording. A fix
that lands after the compose step is written into the composed section by hand; the fragments are
the input, not a second source of truth.

## Release

Before creating a release tag:

1. Compose the fragments on the release branch with `./scripts/Update-Changelog.ps1 -Compose` and
   edit the collected entries under `## [Unreleased]`: merge related entries, drop what a consumer
   cannot observe and fix the wording. Commit the composed entries.
2. Close the section with `./scripts/Update-Changelog.ps1 -Finalize VERSION`, where `VERSION` is the
   tag without a leading `v`, and commit the result together with the deleted fragments.
3. Confirm `## [Unreleased]` is empty and `changelog.d/` holds no fragment, then create the tag on
   that commit, so the changelog at the tagged commit names the released version and carries no
   unreleased entries.

The fragments are deleted on the release or hotfix branch, and merging that branch back into `dev`
carries the deletion. A fragment created on `dev` after the release branch was cut is not in the
release branch and survives that merge: it belongs to the next release.

`./scripts/Verify-Release.ps1` enforces the structure and the release inputs: the `repository-policy`
workflow runs its structural checks on every push and pull request, and the publish workflow runs its
tag checks before building. The structural check refuses a `## [Unreleased]` with entries (except on a
`release/` or `hotfix/` branch, through `-AllowUnreleased`), a release heading without a valid date, a
version duplicated or out of order, and a category heading repeated inside one release section. The
tag check also refuses a non-empty `## [Unreleased]`, a commit without exactly one stable tag, and a
`HEAD` that is not an ancestor of `main`. When the release pipeline creates a GitHub release, the
version section becomes the release description, followed by a link to the full changelog at the tag.

The rest of the workflow — tests, build and the message format — is defined by the
[testing policy](testing.md), the [development policy](development.md) and the
[commit message policy](commits.md). Release branches and tags are in the
[branching policy](branching.md).
