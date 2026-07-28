# analyze_results.py

Aggregates and analyzes the JSON Lines files produced by `ProgramConfig.ReportResult`
(see `test/Common/ProgramConfig.cs`) and writes a Markdown report.

`ReportResult` is opt-in: it only writes to a file when `ResultsOutputPath` is configured
(via the existing `/p:ResultsOutputPath=...` CLI override, same mechanism as
`ForwardCacheTimeout`/`ReverseCacheTimeout`). When enabled, every test project appends one
JSON object per line, e.g.:

```json
{"timestamp":"2026-07-25T10:00:00.0000000Z","project":"KEFCore.Benchmark.Test","testId":"Test 4","elapsedMs":12.34,"success":true,"details":"Max ... Min ... Mean ... Median ...","forwardCacheTimeout":-1,"reverseCacheTimeout":-1}
```

## Usage

```bash
python analyze_results.py --input run1.jsonl run2.jsonl --output report.md
```

Pass every result file you want included in one call — e.g. both legs of the `use_cache:
[true, false]` CI matrix (`build_common.yaml`), so the report's "cached vs non-cached delta"
section can actually compute something:

```bash
python analyze_results.py \
  --input results-noncached.jsonl results-cached.jsonl \
  --output report.md \
  --title "KEFCore.Benchmark.Test — 2026-07-25"
```

## What the report contains

1. **Headline** — one at-a-glance median, per `(project, framework, cache bucket)`: the whole-iteration
   wall-clock time (matched by pattern against `testId`, see `HEADLINE_PATTERN` — today that's
   Benchmark.Test's `IterationTotal (sum of all queries above, ...)` entry). Meant to be the single
   number you check first, before any detailed table.
2. **Comparison vs baseline** *(only when `--baseline` is given)* — see below.
3. **Failures** — every record with `success: false`, listed first and prominently, regardless
   of which test or cache bucket it came from.
4. **Summary by test** — count / mean / median / min / max / stdev / success rate, grouped by
   `(project, testId, cache bucket)`. The cache bucket is derived from `forwardCacheTimeout`
   using the same `> TimeSpan.Zero` semantics as `KEFCoreCachedValueBufferStore.IsEnabled` in
   the actual provider code — not a naive `!= 0` check (see the projection push-down design
   notes for why that distinction matters: the default TTL in test configs is `-1`, not `0`).
5. **Cached vs non-cached delta** — for any `(project, testId)` pair that has records in both
   cache buckets, the percentage difference in median elapsed time. A consistently positive
   delta (cached slower than non-cached) for a projection-related test is the expected signature
   of the per-entity cache gate correctly suppressing projection push-down when the cache is
   enabled.
6. **Cross-framework comparison** — side-by-side median elapsed time for the same
   project/test/cache-bucket across every `net*.0` framework present in the input.

## Comparing against a previous run (regression check)

Pass `--baseline` alongside `--input` to compare the current run against a previous one and get
an automatic verdict:

```bash
python analyze_results.py \
  --input after/*.jsonl \
  --baseline before/*.jsonl \
  --output report.md \
  --regression-threshold 5.0
```

For every `(project, framework, testId, cache bucket)` present in **both** runs, this adds a
"Comparison vs baseline" section right after the Headline, with:
- an overall verdict banner (✅ no regressions / ⚠️ N regression(s) detected);
- a table of every compared test, sorted regressions-first, each with its own delta % and
  verdict (`REGRESSION` / `IMPROVEMENT` / `no significant change`);
- a note on any test present in only one of the two runs (new/removed tests aren't compared).

A verdict only fires when **both** thresholds are exceeded:
- `--regression-threshold` (percent, default `5.0`)
- `--regression-threshold-abs-ms` (absolute milliseconds, default `1.0`)

The absolute floor matters in practice: sub-millisecond queries routinely show large percent
swings between runs (GC pauses, JIT warmup, OS scheduling jitter) that are not real regressions —
e.g. a query going from 0.058ms to 0.068ms is a +17% delta but only +0.01ms in absolute terms, and
is correctly reported as "no significant change" with the default thresholds. A real regression on
a heavier operation (say 6000ms → 6500ms, +8.3% and +500ms) still fires normally. Tune both flags
to match the scale of what you're measuring:

```bash
python analyze_results.py \
  --input after/*.jsonl \
  --baseline before/*.jsonl \
  --output report.md \
  --regression-threshold 5.0 \
  --regression-threshold-abs-ms 1.0
```

This is the natural way to check "did this PR make things faster or slower": run the test suite
before and after the change with `/p:ResultsOutputPath=...` pointed at two different files, then
pass both to this script.

## Separating a real regression from "the runner was just faster/slower"

CI runners vary between runs (different hardware, shared load on GitHub-hosted runners, etc.),
which can shift *every* test's timing in the same direction regardless of any code change — making
a real regression on one test invisible (masked by an overall speedup) or a fake one appear (masked
as a slowdown) on another. If you know some tests exercise a code path the change under test
provably doesn't touch (e.g. write-path operations like `SaveChanges`/`LocalStoreSynchronized` when
the change is read-path only), pass them as `--control-tests` to get an environment-drift estimate
and adjusted verdicts:

```bash
python analyze_results.py \
  --input after/*.jsonl \
  --baseline before/*.jsonl \
  --control-tests SaveChanges LocalStoreSynchronized DataLoad \
  --output report.md
```

This adds an "Environment drift" note (the median delta measured across the matched control tests)
and, for every other test, an env-adjusted delta/verdict column *alongside* the raw one — nothing
is hidden, both numbers are always shown. Control tests themselves are excluded from the scored
regression/improvement count (their adjusted delta is ~0% by construction, since they define the
drift) but still listed in the table, tagged `(control)`, for transparency.

Matching is a case-insensitive substring against `testId` — with no `--control-tests`, no drift
adjustment is computed and the report looks exactly as it did before this feature (opt-in only,
since deciding what's "unaffected by the change under test" requires knowing what the change
actually is, which this script can't infer on its own).

## No dependencies

Standard library only (`json`, `statistics`, `argparse`, `pathlib`) — no `pip install` needed,
runs with any Python 3.10+.

## Example

`example/` contains synthetic (not real) sample data:
- `sample-noncached.jsonl` / `sample-cached.jsonl` — one CI matrix run with `use_cache: false` and
  one with `use_cache: true`, across three frameworks → `sample-report.md`.
- `baseline-run.jsonl` / `current-run.jsonl` — a "before" and "after" run with a deliberate 2x
  regression injected into one test, to demonstrate the `--baseline` verdict → `sample-comparison-report.md`.
- `drift-baseline.jsonl` / `drift-current.jsonl` — a "before"/"after" pair where every test shifts
  ~-10% (simulating a faster runner) while one read-path test has a real +7% regression hidden
  underneath that shift → `sample-drift-adjusted-report.md`, demonstrating `--control-tests`.

None of this is real benchmark data — it exists to show the tool end-to-end without needing a
live Kafka cluster. Regenerate it with:

```bash
python analyze_results.py \
  --input example/sample-noncached.jsonl example/sample-cached.jsonl \
  --output example/sample-report.md \
  --title "Example run (synthetic data)"

python analyze_results.py \
  --input example/current-run.jsonl \
  --baseline example/baseline-run.jsonl \
  --output example/sample-comparison-report.md \
  --title "Example: comparison vs baseline (synthetic data)"

python analyze_results.py \
  --input example/drift-current.jsonl \
  --baseline example/drift-baseline.jsonl \
  --control-tests SaveChanges LocalStoreSynchronized DataLoad \
  --regression-threshold-abs-ms 0.5 \
  --output example/sample-drift-adjusted-report.md \
  --title "Example: environment-drift-adjusted comparison (synthetic data)"
```