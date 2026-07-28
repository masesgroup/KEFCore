# Example: environment-drift-adjusted comparison (synthetic data)

Generated: 2026-07-28T02:23:46.477451+00:00

Total records analyzed: **5**

## Headline

_No test's `testId` matches the headline pattern (`iterationtotal`), so no single at-a-glance number is available. See "Summary by test" below for the full per-query breakdown._

## Comparison vs baseline

Delta % and verdict, per `(project, framework, testId, cache)` present in both runs. Positive delta = current run slower (regression); negative = faster (improvement). A verdict only fires when **both** thresholds are exceeded: **±5%** **and** **±0.50 ms** absolute — this avoids flagging noise on sub-millisecond queries, where a large percent swing can still be an insignificant absolute difference (e.g. GC pauses, JIT warmup, OS scheduling jitter).

### Environment drift

Median delta across 3 control test(s) matching `savechanges, localstoresynchronized, dataload` (operations assumed unaffected by the change under test): **-10.0%**. This is used as an estimate of environment-only drift (different runner hardware, shared CI load, etc. between the baseline and current run) and subtracted from every other test's delta below to get an "env-adjusted" delta and verdict — shown alongside the untouched raw numbers, never in place of them.

**?? Overall verdict (env-adjusted): 1 regression(s) detected** (out of 2 compared tests, 0 improved).

| Project | Framework | Test | Cache | Baseline (ms) | Current (ms) | Delta % (raw) | Delta % (env-adjusted) | Verdict (env-adjusted) |
|---|---|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | NestedComplexTypeProjection_BlogId10_TaxCode | non-cached | 400.000 | 388.800 | -2.8% | +7.2% | REGRESSION |
| KEFCore.Complex.Test | net9.0 | DataLoad _(control)_ | non-cached | 6000.000 | 5430.000 | -9.5% | +0.5% | no significant change |
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized _(control)_ | non-cached | 3000.000 | 2700.000 | -10.0% | +0.0% | no significant change |
| KEFCore.Complex.Test | net9.0 | SaveChanges _(control)_ | non-cached | 7000.000 | 6300.000 | -10.0% | +0.0% | no significant change |
| KEFCore.Complex.Test | net9.0 | ScalarOnlyProjection_BlogId10_Url | non-cached | 8.000 | 7.600 | -5.0% | +5.0% | no significant change |

## Failures

None. All reported outcomes had `success: true`.

## Summary by test

`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds).

| Project | Framework | Test | Cache | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | DataLoad | non-cached | 1 | 5430.000 | 5430.000 | 5430.000 | 5430.000 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized | non-cached | 1 | 2700.000 | 2700.000 | 2700.000 | 2700.000 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | NestedComplexTypeProjection_BlogId10_TaxCode | non-cached | 1 | 388.800 | 388.800 | 388.800 | 388.800 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | SaveChanges | non-cached | 1 | 6300.000 | 6300.000 | 6300.000 | 6300.000 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | ScalarOnlyProjection_BlogId10_Url | non-cached | 1 | 7.600 | 7.600 | 7.600 | 7.600 | n/a | 100% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median), computed separately per framework so a difference in EF Core version behavior isn't hidden by averaging across them. For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case.

_No test has records in both cache buckets, so no delta can be computed. Run the same scenario once with `/p:ForwardCacheTimeout=-1` and once with a positive value (e.g. `/p:ForwardCacheTimeout=60`) and pass both result files to this script to populate this section._

## Cross-framework comparison

For the same project/test/cache-bucket, how the median elapsed time compares across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from more than one framework.

_No test has records from more than one framework, so no cross-framework comparison can be made. Pass result files from multiple `net*.0` runs together to populate this section._
