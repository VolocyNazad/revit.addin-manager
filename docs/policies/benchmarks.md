# Benchmark policy

Paths mentioned in this policy are relative to the repository root unless a Markdown link specifies otherwise.

Performance work is measured, not estimated. A claim about speed is backed by numbers from a
repeatable run on a stated environment.

## When

- a `perf:` change;
- a change to a hot path: parsing, serialization, collection handling, the file system or
  start-up;
- a user-visible claim about speed or memory in a pull request, the changelog or the
  documentation.

## How

Benchmarks live in `benchmarks/<Project>.Benchmarks` and use
[BenchmarkDotNet](https://benchmarkdotnet.org/):

- add `[MemoryDiagnoser]` when allocations matter and run in `Release`, without a debugger,
  on an otherwise idle machine;
- run with `dotnet run -c Release --project benchmarks/<Project>.Benchmarks -- --filter
  "*<Benchmarks>*"`;
- a benchmark that compiles differently per build configuration runs on each of those
  configurations, so the numbers match the shipped code;
- compare against the current `main`/`dev` baseline in the same session; a number from another
  machine or day is not a comparison.

## Not in CI

Benchmarks never run in CI: a shared runner is too noisy for a meaningful number. They are run
locally, before and after the change.

## Reporting

- A `perf:` commit carries the before and after numbers and the environment in its body.
- Results are reported in the pull request and not committed; benchmark output is generated
  data.
- No thresholds are enforced in CI; a regression is caught in review, not by a red build.

The rest of the workflow is defined by the [development policy](development.md); what must be
tested is in the [testing policy](testing.md).
