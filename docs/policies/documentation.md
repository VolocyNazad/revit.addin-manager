# Documentation policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: writing, moving or restructuring contributor documentation or XML documentation.

Every document has one genre and one subject, so each fact has one home and the rest of the repository
links to it instead of restating it.

## Documents and genres

| Document | Genre | Answers |
|---|---|---|
| `docs/policies/*.md` (index: [repository policies](README.md)) | Binding rules | What a change must do in one area |
| [Repository guide](../repository.md) | Facts about the repository | How the solution, projects, tooling and documents are arranged |
| [Architecture decisions](../architecture.md) | Decisions | Which stable module boundaries and design rules the product keeps |
| [Product scope](../FEATURES.md) | Product contract | Which scope and compatibility contract the current release promises |
| [TODO.md](../../TODO.md) | Plan | Agreed future work that is not implemented |
| `docs/WIKI.md` | User documentation | What a user does, step by step, and what the product offers |
| [GLOSSARY.md](../../GLOSSARY.md) | Vocabulary | Which terms the code, documents and tests share |
| [AGENTS.md](../../AGENTS.md) | Entry point | Which documents a change must read |
| [CONTRIBUTING.md](../../CONTRIBUTING.md) | Contributor workflow | How a change is prepared, checked and submitted |
| [CHANGELOG.md](../../CHANGELOG.md) | Release history | What one release changed |

The genre decides the content: do not move a rule into a guide, and do not record a procedure as a
policy rule.

## Single source of truth

- Each fact, rule or procedure has exactly one owning document. Another document that needs it links
  to it.
- Do not copy a section into a second document; keep the full text in the owning document and link to
  it.
- A summary in a guide links to the owning document and does not become a second rule.
- When a section is renamed, moved or split, update every document that references it in the same
  change. Never leave a link pointing at a heading that no longer exists.

## Updating documentation in the same change

- A change that affects the folder structure, the technology stack, a dependency, a recorded version
  or a new convention updates the relevant policy and the [repository guide](../repository.md) in the
  same change, never as a later follow-up.
- A change that alters product behaviour updates the [product scope](../FEATURES.md) and the affected
  user guide in the same change, as the [wiki policy](wiki.md) requires.
- A change that introduces or revises a fundamental architectural decision updates
  [architecture decisions](../architecture.md) in the same change.
- A change that adds or removes a user-visible string updates the localized resources and the
  affected user guide page.
- A change covered by the [changelog policy](changelog.md) adds its fragment in `changelog.d/` in the
  way that policy requires.
- A new documentation file is added as a solution item together with the file; the
  [repository maintenance policy](repository-maintenance.md) defines the solution items contract.
- Documentation is not deferred.

## XML documentation

- XML documentation is mandatory for public APIs in production projects (`CS1591` is an error); test
  projects are exempt (`NoWarn`).
- Documentation explains the observable contract rather than restating the identifier.
- Use `<remarks>` for behaviour that is not evident from the signature: implicit fallback, inferred
  defaults, side effects, ordering, caching, thread requirements and intentionally ignored failures.
- Keep the note next to the affected API or configuration setting, not in a distant architecture
  document.
- If a type is public only accidentally and has no external or cross-project consumer, reduce its
  visibility instead of documenting it as an API.

## Documentation language

- Policies and contributor guides are written in English.
- The user guide `docs/WIKI.md` is written in Russian; it is the only published user documentation.
- Identifiers follow the naming rules of the [development policy](development.md); documentation uses
  the terms recorded in [GLOSSARY.md](../../GLOSSARY.md).

## Links and anchors

- Link between repository documents with relative Markdown links. Do not use absolute repository URLs
  in repository documents; the [wiki policy](wiki.md) defines how the published page links back to
  repository files.
- An anchor must point at a heading that exists.
- Do not leave a broken or ambiguous link. If a target no longer exists, update the text instead of
  deleting only the link.
- `scripts/Validate-Repository.ps1` checks every relative link and anchor; the
  [repository maintenance policy](repository-maintenance.md) defines when it must run.

## Glossary

- [GLOSSARY.md](../../GLOSSARY.md) is the single source of the shared product vocabulary.
- A new term, or a change to an existing term, is made in the same change that introduces or renames
  the concept, and every usage across code, documentation and messages is updated with it.
- Documentation, messages and tests use the glossary term and never the variants listed under
  `_Avoid_:` in the glossary entry.

The rest of the contributor workflow is defined by the [development policy](development.md).
