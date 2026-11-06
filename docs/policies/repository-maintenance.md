# Repository maintenance policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: adding a file or solution item, changing the formatting baseline, or changing a
repository-level file.

This policy defines what the root solution exposes, which root files must exist, how repository-level
changes are validated, and the formatting baseline.

## Solution items

- Keep the root solution listing the existing repository-level documentation, the license, Git and
  editor settings, SDK, NuGet and versioning files, shared MSBuild configuration, and root
  maintenance scripts.
- Place root files under the `/solutionItems/` folder, `.github/` and `scripts/` files in matching
  subfolders, and documentation under `/docs/` with its directory hierarchy.
- Add, rename and remove solution links in the same change as the corresponding files. A solution
  folder does not pick up new files by itself.
- Include only files that exist, each exactly once. Exclude local settings, secrets and generated
  output; build output is never a solution item.
- Keep the list explicit, not a filesystem glob.
- Treat every file under `docs/`, `.github/` and `scripts/` as managed. A new policy file is
  therefore also a solution item in the `/docs/policies/` folder; add the link together with the file.

## Required root files

- Keep exactly one NuGet configuration, one license and one root solution. The validator accepts
  `NuGet.config`/`nuget.config`, `LICENSE`/`LICENSE.md`/`LICENSE.txt` and a single `.sln` or `.slnx`;
  do not introduce a second candidate name or a second root solution.
- Keep the required root documents present and non-empty: `README.md`, `AGENTS.md`, `CHANGELOG.md`,
  `CONTRIBUTING.md`, `GLOSSARY.md`, `global.json`, the policy set the validator enumerates,
  `docs/repository.md` and the scripts the validator enumerates.
- Do not rename or relocate a required root file without updating the validator, the links that point
  to it and this policy in the same change.
- Keep the root solution at the repository root; nested solutions belong to their build unit, as the
  [build and toolchain policy](build.md) defines.

## Repository validation

- Run `./scripts/Validate-Repository.ps1` after changing repository-level files, documentation, a
  workflow or the solution.
- The same validator runs in CI on every push to `main` and `dev` and on every pull request
  (`.github/workflows/repository-policy.yml`). Neither replaces the other.
- Expect the validator to check the required files, every relative Markdown link and its anchor, and
  the completeness, uniqueness and validity of solution items. It also fails when Git tracks `bin/`
  or `obj/` content, when a workflow action is not pinned to a full version tag, when a tracked text
  file is not valid UTF-8, and when the neutral and Russian resource keys differ.
- Update the solution together with added, renamed or removed managed files; the validator compares
  the managed set against the solution list and names what is missing.
- Do not silence a validator failure by deleting a required file or link. Fix the cause, and when the
  rule itself is wrong, change the validator and this policy in the same change.

## Formatting baseline

- Keep the root `.editorconfig` in the solution. It is the repository-wide baseline for encoding
  (UTF-8), line endings (CRLF) and trailing-whitespace trimming, plus the shared C# conventions. The
  root `.gitattributes` keeps the working tree on CRLF while the repository stores LF, and marks
  binary assets so they are never converted.
- Apply the baseline to every file it matches. Do not add a file-local override that fights it
  without a reason recorded in the change.
- Let repository-specific sections add stricter language rules. Do not replace a specialized rule
  with the shared minimum, and do not weaken the baseline to accommodate one file.
- Update the file deliberately when a convention changes, and apply the reformat in the same change;
  do not mix formatting churn with a behaviour change.
- Private fields follow one grouping rule: fields of the same logical group (constants, injected
  dependencies, mutable state, caches) stay contiguous with no blank lines between them, while
  exactly one blank line separates the groups and precedes a field's XML-doc/attribute block; never
  leave a blank line immediately after `{` or before `}`. The Roslyn formatter does not enforce this,
  so treat it as a review convention and follow the existing classes (`MainViewModel`,
  `FileSystemAddinStore`, `FileSystemAddinChangeWatcher`) as the reference.

## When to run the validator

- Run it after any change to a root repository-level file, a file under `docs/`, `.github/` or
  `scripts/`, the solution item list, or a required document's links.
- Run it after resolving a merge conflict that touched any of those files, before the change is
  reviewed.
- Run it as the last step before opening a pull request, once the files and the solution are final;
  CI repeats it on the pull request.
- A failure is a defect in the change, not in the validator: repair the file or the link instead of
  skipping the check.

The rest of the contributor workflow is defined by the [development policy](development.md).
