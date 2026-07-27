# Example: comparison vs baseline (synthetic data)

Generated: 2026-07-27T07:47:19.554722+00:00

Total records analyzed: **150**

## Headline

Median wall-clock time for one full iteration (all queries in that project's fixed sequence), the single most useful number to check first.

| Project | Framework | Cache | Median (ms) | N |
|---|---|---|---|---|
| KEFCore.Benchmark.Test | net9.0 | non-cached | 96.781 | 50 |

## Comparison vs baseline

Delta % and verdict, per `(project, framework, testId, cache)` present in both runs. Positive delta = current run slower (regression); negative = faster (improvement). A verdict only fires when **both** thresholds are exceeded: **±5%** **and** **±1.00 ms** absolute — this avoids flagging noise on sub-millisecond queries, where a large percent swing can still be an insignificant absolute difference (e.g. GC pauses, JIT warmup, OS scheduling jitter).

**⚠️ Overall verdict: 1 regression(s) detected** (out of 3 compared tests, 0 improved).

| Project | Framework | Test | Cache | Baseline median (ms) | Current median (ms) | Delta % | Verdict |
|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | 3.251 | 6.742 | +107.4% | REGRESSION |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | 8.531 | 8.631 | +1.2% | no significant change |
| KEFCore.Benchmark.Test | net9.0 | IterationTotal (sum of all queries above, not comparable to them individually) | non-cached | 95.724 | 96.781 | +1.1% | no significant change |

## Failures

None. All reported outcomes had `success: true`.

## Summary by test

`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds).

| Project | Framework | Test | Cache | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | 50 | 8.592 | 8.631 | 6.145 | 11.071 | 1.030 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | 50 | 6.709 | 6.742 | 4.099 | 8.200 | 1.167 | 100% |
| KEFCore.Benchmark.Test | net9.0 | IterationTotal (sum of all queries above, not comparable to them individually) | non-cached | 50 | 96.365 | 96.781 | 89.920 | 102.374 | 2.981 | 100% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median), computed separately per framework so a difference in EF Core version behavior isn't hidden by averaging across them. For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case.

_No test has records in both cache buckets, so no delta can be computed. Run the same scenario once with `/p:ForwardCacheTimeout=-1` and once with a positive value (e.g. `/p:ForwardCacheTimeout=60`) and pass both result files to this script to populate this section._

## Cross-framework comparison

For the same project/test/cache-bucket, how the median elapsed time compares across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from more than one framework.

_No test has records from more than one framework, so no cross-framework comparison can be made. Pass result files from multiple `net*.0` runs together to populate this section._
