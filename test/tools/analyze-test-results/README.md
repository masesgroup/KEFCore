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

1. **Failures** — every record with `success: false`, listed first and prominently, regardless
   of which test or cache bucket it came from.
2. **Summary by test** — count / mean / median / min / max / stdev / success rate, grouped by
   `(project, testId, cache bucket)`. The cache bucket is derived from `forwardCacheTimeout`
   using the same `> TimeSpan.Zero` semantics as `KEFCoreCachedValueBufferStore.IsEnabled` in
   the actual provider code — not a naive `!= 0` check (see the projection push-down design
   notes for why that distinction matters: the default TTL in test configs is `-1`, not `0`).
3. **Cached vs non-cached delta** — for any `(project, testId)` pair that has records in both
   cache buckets, the percentage difference in median elapsed time. A consistently positive
   delta (cached slower than non-cached) for a projection-related test is the expected signature
   of the per-entity cache gate correctly suppressing projection push-down when the cache is
   enabled.

## No dependencies

Standard library only (`json`, `statistics`, `argparse`, `pathlib`) — no `pip install` needed,
runs with any Python 3.10+.

## Example

`example/` contains synthetic (not real) sample data — two files mimicking one CI matrix run
with `use_cache: false` and one with `use_cache: true` — and the report generated from them,
to show the tool end-to-end without needing a live Kafka cluster. Regenerate it with:

```bash
python analyze_results.py \
  --input example/sample-noncached.jsonl example/sample-cached.jsonl \
  --output example/sample-report.md \
  --title "Example run (synthetic data)"
```
