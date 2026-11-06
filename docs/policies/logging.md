# Logging policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Read when: adding or changing a log statement, an exception path, or a user-visible failure.

Log significant lifecycle steps and every caught exception, exactly once:

- Lifecycle: application start (with version) and shutdown at Information; every
  user-meaningful disk mutation (scan results, move/delete/create/save, with file
  identity) at Information, routine per-file scan details at Debug; refusals
  (Revit running, already exists, validation) at Warning.
- Exceptions: log where the failure is first known — user-visible failures at Warning,
  disk/data failures at Error with the exception instance. Callers up the chain surface
  only the message (UI banners) and stay silent; never log the same instance twice
  (wrap-and-rethrow with the original as `InnerException`, don't `throw;` an
  already-logged one).
- Benign documented fallbacks stay silent with an explanatory comment: `Try*` probes,
  corrupt-settings → default, per-process read failures, designer fallbacks, and
  cancellation-as-control-flow.
- Structured logging with named placeholders, never interpolation into the template.
- Adapters, decorators and wrappers do not log an exception they rethrow; log once where the failure is first understood and handled, then surface only the message.
- User cancellation is control flow, not a failure: log it at Debug or stay silent, never Warning/Error.
- Every user-visible exception message comes from `IStringLocalizer` resources; the technical `InnerException` may stay in its original language and is never shown as the primary message.
- Log files live under `%LOCALAPPDATA%\Volocy\Revit.AddinManager\logs\`, roll daily and at 20 MB, and
  retain the newest 14 files; the configuration is wired once in the composition root, not per module.
- Privacy: log file paths and manifest identifiers, never manifest content or user-entered text that
  is not needed to identify the failure.

The rest of the contributor workflow is defined by the [development policy](development.md).
