# AGENTS.md

This repository keeps its rules and facts in documents, not in this file. Load only what your change
needs: the table routes you, and each document says what it owns.

| Your change touches | Read first | Then, only when the change crosses that area |
| --- | --- | --- |
| Anything | [Development policy](docs/policies/development.md) — asking before a change, code basics | The [policy index](docs/policies/README.md): pick the one policy that covers the area |
| Repository structure, dependencies, technology stack, build units, release pipeline | [Repository guide](docs/repository.md) | [Build and toolchain policy](docs/policies/build.md) when the SDK, a build unit or a package version changes |
| A branch, a commit, a pull request | [Branching policy](docs/policies/branching.md) and [commit message policy](docs/policies/commits.md) | [Pull request policy](docs/policies/pull-requests.md) when preparing, reviewing or merging |
| `CHANGELOG.md`, `changelog.d/`, a release, version, MSI | [Changelog policy](docs/policies/changelog.md) | [Release policy](docs/policies/releases.md) when a tag or the MSI is involved |
| Tests, benchmarks, a UI change | [Testing policy](docs/policies/testing.md) | [Benchmark policy](docs/policies/benchmarks.md) for a performance claim |
| Logging, an exception path | [Logging policy](docs/policies/logging.md) | The [repository guide](docs/repository.md) for the log file location |
| User-visible text or behaviour | [Wiki policy](docs/policies/wiki.md) and [docs/WIKI.md](docs/WIKI.md) | [Documentation policy](docs/policies/documentation.md) when a contributor document changes |
| Repository-level files, solution items, formatting | [Repository maintenance policy](docs/policies/repository-maintenance.md) | — |
| A term, a name in code or docs | [GLOSSARY.md](GLOSSARY.md) | [Documentation policy](docs/policies/documentation.md) for the language rules |

## How to load context

- Open sections, not whole files. [docs/repository.md](docs/repository.md) and
  [docs/architecture.md](docs/architecture.md) are references for the area you touch.
- Documents under [docs/policies/](docs/policies/) are binding rules; the
  [policy index](docs/policies/README.md) says what each one owns. Read the policy that covers your
  area and the [development policy](docs/policies/development.md), not all of them.
- A user-visible change carries its entry as one fragment in `changelog.d/<category>/<slug>.md`;
  do not edit `## [Unreleased]` during regular development.
- Do not read generated or ignored directories for context: `output/`, `bin/`, `obj/`, `.vs/`,
  `.idea/`, `TestResults/`.
- Verify before finishing:
  [scripts/Validate-Repository.ps1](scripts/Validate-Repository.ps1) checks files, links, anchors,
  solution items, action pins and encoding; [scripts/Update-Changelog.ps1](scripts/Update-Changelog.ps1)
  checks the pending fragments; [scripts/Verify-Release.ps1](scripts/Verify-Release.ps1) checks the
  `CHANGELOG.md` structure.
