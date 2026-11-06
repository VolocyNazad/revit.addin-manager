# Repository policies

Read the policy for your area before changing it; the [development policy](development.md) is the
entry point and links the rest.

Facts belong in the [repository guide](../repository.md), architecture decisions in
[architecture decisions](../architecture.md), user-visible behaviour in [product scope](../FEATURES.md),
vocabulary in [GLOSSARY.md](../../GLOSSARY.md), and future work in [TODO.md](../../TODO.md).

## Conventions

- Policies are written in English.
- Every policy has a `Read when:` line after the paths sentence.
- Every policy starts with the sentence "Paths mentioned in this policy are relative to the
  repository root unless a Markdown link specifies otherwise.".
- Policy links are relative Markdown links; an anchor must point at an existing heading, checked by
  `scripts/Validate-Repository.ps1`.
- A policy ends by pointing to the [development policy](development.md).
- A policy owns exactly one subject. Rules owned by another policy are linked, not repeated.

## Policy index

| Policy | Covers | Enforced by |
| --- | --- | --- |
| [Development policy](development.md) | Entry point: repository rules, asking before a change, code basics, links to every policy | Review |
| [Documentation policy](documentation.md) | Document genres, single source of truth, updates in the same change, XML documentation, language, links, glossary | Review; `scripts/Validate-Repository.ps1` checks links and anchors |
| [Repository maintenance policy](repository-maintenance.md) | Solution items, required root files, repository validation, formatting baseline | `scripts/Validate-Repository.ps1` through `.github/workflows/repository-policy.yml` |
| [Build and toolchain policy](build.md) | .NET SDK selection, build units, warnings, package versions | `.github/workflows/build-and-test.yml` (warnings as errors); review |
| [Branching policy](branching.md) | Branch kinds, base branches, merge strategy, protected branches | Review; branch protection is configured in GitHub |
| [Commit message policy](commits.md) | Conventional Commits, types, scope, body and footers | Review |
| [Pull request policy](pull-requests.md) | Source branch and target, required checks, review, author checklist | Review; `CI gate` in `.github/workflows/build-and-test.yml` and `.github/workflows/repository-policy.yml` are the required checks |
| [Changelog policy](changelog.md) | `CHANGELOG.md` format, `changelog.d/` fragments, the compose and finalize tool, release section | `scripts/Update-Changelog.ps1 -Verify` and `scripts/Verify-Release.ps1` in `.github/workflows/repository-policy.yml`; `Verify-Release.ps1` in `publish-release.yml` also checks the tag |
| [Release policy](releases.md) | Release channels, tags, versioning, release metadata, MSI artifacts, publish automation | `.github/workflows/publish-release.yml` and `scripts/Verify-Release.ps1` check the tag and the changelog |
| [Testing policy](testing.md) | Test kinds, what needs a test, determinism, running the tests, manual smoke checks | `.github/workflows/build-and-test.yml` and `scripts/Invoke-HeadlessTests.ps1` |
| [Benchmark policy](benchmarks.md) | Benchmark kinds, when to measure, reporting | Review; benchmarks deliberately do not run in CI |
| [Logging policy](logging.md) | Log levels, one log per failure, structured messages, privacy | Review |
| [Wiki policy](wiki.md) | Wiki as a mirror of `docs/WIKI.md`, publication rules | `.github/workflows/publish-wiki.yml` |

## Adding or changing a policy

1. Add or edit the file in `docs/policies/`. Keep one subject per policy.
2. Add the row to the [policy index](#policy-index) above.
3. Add the file to the `/docs/policies/` solution folder in `Revit.AddinManager.slnx`, or the
   validator fails.
4. Link the policy from [development policy](development.md), [CONTRIBUTING.md](../../CONTRIBUTING.md),
   the pull request template or the area guide; keep no second copy of the full list.
5. Run `./scripts/Validate-Repository.ps1`.

When an automated check replaces a policy rule, say so in the "Enforced by" column. Fix a policy that
is no longer enforced instead of leaving a dead rule.

The rest of the contributor workflow is defined by the [development policy](development.md).
