# Example run (synthetic data)

Generated: 2026-07-24T22:07:28.100538+00:00

Total records analyzed: **407**

## Failures

**1** failing record(s):

| Project | Test | Cache | Elapsed (ms) | Details | Source |
|---|---|---|---|---|---|
| KEFCore.Complex.Test | LocalStoreSynchronized | cached | 5000.000 | timed out waiting for synchronization | example/sample-cached.jsonl |

## Summary by test

Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds).

| Project | Test | Cache | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | Test 1 | non-cached | 100 | 8.652 | 8.701 | 6.242 | 10.702 | 0.793 | 100% |
| KEFCore.Benchmark.Test | Test 1 | cached | 100 | 8.728 | 8.669 | 6.619 | 11.697 | 1.027 | 100% |
| KEFCore.Benchmark.Test | Test 4 | non-cached | 100 | 3.187 | 3.212 | 1.912 | 4.242 | 0.483 | 100% |
| KEFCore.Benchmark.Test | Test 4 | cached | 100 | 8.555 | 8.512 | 6.650 | 10.551 | 0.910 | 100% |
| KEFCore.Complex.Test | DataLoad | non-cached | 1 | 430.556 | 430.556 | 430.556 | 430.556 | n/a | 100% |
| KEFCore.Complex.Test | DataLoad | cached | 1 | 460.823 | 460.823 | 460.823 | 460.823 | n/a | 100% |
| KEFCore.Complex.Test | LocalStoreSynchronized | non-cached | 1 | 43.032 | 43.032 | 43.032 | 43.032 | n/a | 100% |
| KEFCore.Complex.Test | LocalStoreSynchronized | cached | 2 | 2527.662 | 2527.662 | 55.324 | 5000.000 | 3496.414 | 50% |
| KEFCore.Complex.Test | SaveChanges | non-cached | 1 | 123.697 | 123.697 | 123.697 | 123.697 | n/a | 100% |
| KEFCore.Complex.Test | SaveChanges | cached | 1 | 122.609 | 122.609 | 122.609 | 122.609 | n/a | 100% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median). For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case.

| Project | Test | Non-cached median (ms) | Cached median (ms) | Delta % |
|---|---|---|---|---|
| KEFCore.Benchmark.Test | Test 1 | 8.701 | 8.669 | -0.4% |
| KEFCore.Benchmark.Test | Test 4 | 3.212 | 8.512 | +165.0% |
| KEFCore.Complex.Test | DataLoad | 430.556 | 460.823 | +7.0% |
| KEFCore.Complex.Test | LocalStoreSynchronized | 43.032 | 2527.662 | +5773.9% |
| KEFCore.Complex.Test | SaveChanges | 123.697 | 122.609 | -0.9% |
