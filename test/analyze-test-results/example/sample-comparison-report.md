# Example: comparison vs baseline (synthetic data)

Generated: 2026-09-02T06:07:01.028657+00:00

Total records analyzed: **150**

## Headline

Median wall-clock time for one full iteration (all queries in that project's fixed sequence), the single most useful number to check first. `Scenario` is `load` (data seeded this run) or `reload` (data read back from a previous invocation, local store already warm) - see the module docstring.

| Project | Framework | Cache | Scenario | Median (ms) | N |
|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net9.0 | non-cached | load | 96.781 | 50 |

## Comparison vs baseline

Delta % and verdict, per `(project, framework, testId, cache, scenario)` present in both runs. Positive delta = current run slower (regression); negative = faster (improvement). A verdict only fires when **both** thresholds are exceeded: **±5%** **and** **±1.00 ms** absolute — this avoids flagging noise on sub-millisecond queries, where a large percent swing can still be an insignificant absolute difference (e.g. GC pauses, JIT warmup, OS scheduling jitter).

**⚠️ Overall verdict: 1 regression(s) detected** (out of 3 compared tests, 0 improved).

| Project | Framework | Test | Cache | Scenario | Baseline median (ms) | Current median (ms) | Delta % | Verdict |
|---|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | load | 3.251 | 6.742 | +107.4% | REGRESSION |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | load | 8.531 | 8.631 | +1.2% | no significant change |
| KEFCore.Benchmark.Test | net9.0 | IterationTotal (sum of all queries above, not comparable to them individually) | non-cached | load | 95.724 | 96.781 | +1.1% | no significant change |

## Failures

None. All reported outcomes had `success: true`.

## Summary by test

`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds). `Scenario` is `load` (data seeded this run) or `reload` (data read back from a previous invocation, local store already warm) - kept separate from Cache so the two axes are never silently averaged together.

| Project | Framework | Test | Cache | Scenario | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | load | 50 | 8.592 | 8.631 | 6.145 | 11.071 | 1.030 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | load | 50 | 6.709 | 6.742 | 4.099 | 8.200 | 1.167 | 100% |
| KEFCore.Benchmark.Test | net9.0 | IterationTotal (sum of all queries above, not comparable to them individually) | non-cached | load | 50 | 96.365 | 96.781 | 89.920 | 102.374 | 2.981 | 100% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median), computed separately per framework **and per scenario** so neither a difference in EF Core version behavior nor a `load` vs `reload` leg (see "Summary by test" above) is hidden by averaging across them - a `reload` leg hits an already-warm local store on both sides of the comparison, which would otherwise flatten a real cached-vs-non-cached difference seen on `load`. For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case.

_No test has records in both cache buckets for the same scenario, so no delta can be computed. Run the same scenario once with `/p:ForwardCacheTimeout=-1` and once with a positive value (e.g. `/p:ForwardCacheTimeout=60`), keeping `/p:LoadApplicationData` consistent between the two, and pass both result files to this script to populate this section._

## Cross-framework comparison

For the same project/test/cache-bucket/scenario, how the median elapsed time compares across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from more than one framework.

_No test has records from more than one framework, so no cross-framework comparison can be made. Pass result files from multiple `net*.0` runs together to populate this section._
