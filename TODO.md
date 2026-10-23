# TODO

Agreed future work that is not implemented yet. Items are checked off when the change
lands; user-visible release notes still belong in `CHANGELOG.md`, not here.

## Repository infrastructure

- [x] Build `build/Revit.AddinManager.Build.slnx` and the installer generator in CI, not only the root solution.
- [x] Centralize vendor, product, GitHub destinations, MSI name and synchronization names in `ProductIdentity`.
- [ ] Stabilize watcher tests that currently wait on fixed timer delays; replace them with a deterministic clock seam where practical.

## Manifest and editor

- [ ] Decide how `RootOrder` should follow a deleted entry: comments and unknown nodes currently keep their tokens, so a comment can move next to the following entry.
- [ ] Replace the remaining log-text assertions in tests with structured fields now that `RecordingLogger` exposes `EventId`, template and properties where practical.

## Release tooling

- [ ] Verify the MSI end to end with the WiX toolchain (install, upgrade, uninstall) before the next release; local builds only compile the generator.
- [ ] Review whether the publish pipeline should compose the changelog from Conventional Commits instead of requiring a hand-written release section.
