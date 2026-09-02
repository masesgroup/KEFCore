# Example run (synthetic data)

Generated: 2026-09-02T06:07:00.972738+00:00

Total records analyzed: **1207**

## Headline

_No test's `testId` matches the headline pattern (`iterationtotal`), so no single at-a-glance number is available. See "Summary by test" below for the full per-query breakdown._

## Failures

**1** failing record(s):

| Project | Framework | Test | Cache | Scenario | Elapsed (ms) | Details | Source |
|---|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized | cached | load | 5000.000 | timed out waiting for synchronization | example/sample-cached.jsonl |

## Summary by test

`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds). `Scenario` is `load` (data seeded this run) or `reload` (data read back from a previous invocation, local store already warm) - kept separate from Cache so the two axes are never silently averaged together.

| Project | Framework | Test | Cache | Scenario | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net10.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | load | 100 | 7.280 | 7.275 | 5.329 | 9.632 | 0.786 | 100% |
| KEFCore.Benchmark.Test | net10.0 | Blog.SingleOrDefault(BlogId==1) [cold] | cached | load | 100 | 7.399 | 7.410 | 5.892 | 9.597 | 0.661 | 100% |
| KEFCore.Benchmark.Test | net10.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | load | 100 | 2.569 | 2.510 | 1.462 | 3.507 | 0.481 | 100% |
| KEFCore.Benchmark.Test | net10.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | load | 100 | 7.105 | 7.075 | 5.247 | 8.593 | 0.692 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | load | 100 | 8.652 | 8.701 | 6.242 | 10.702 | 0.793 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blog.SingleOrDefault(BlogId==1) [cold] | cached | load | 100 | 8.717 | 8.669 | 6.619 | 11.697 | 1.012 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | load | 100 | 3.187 | 3.212 | 1.912 | 4.242 | 0.483 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | load | 100 | 8.540 | 8.502 | 6.650 | 10.551 | 0.908 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | load | 100 | 7.814 | 7.854 | 5.774 | 9.775 | 0.805 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | cached | load | 100 | 7.809 | 7.877 | 5.997 | 9.293 | 0.683 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | load | 100 | 2.884 | 2.885 | 1.665 | 3.969 | 0.479 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | load | 100 | 7.819 | 7.710 | 5.208 | 10.927 | 0.945 | 100% |
| KEFCore.Complex.Test | net10.0 | DataLoad | non-cached | load | 1 | 363.425 | 363.425 | 363.425 | 363.425 | n/a | 100% |
| KEFCore.Complex.Test | net10.0 | DataLoad | cached | load | 1 | 402.500 | 402.500 | 402.500 | 402.500 | n/a | 100% |
| KEFCore.Complex.Test | net8.0 | DataLoad | non-cached | load | 1 | 517.228 | 517.228 | 517.228 | 517.228 | n/a | 100% |
| KEFCore.Complex.Test | net8.0 | DataLoad | cached | load | 1 | 462.395 | 462.395 | 462.395 | 462.395 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | DataLoad | non-cached | load | 1 | 363.582 | 363.582 | 363.582 | 363.582 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | DataLoad | cached | load | 1 | 361.066 | 361.066 | 361.066 | 361.066 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized | cached | load | 1 | 5000.000 | 5000.000 | 5000.000 | 5000.000 | n/a | 0% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median), computed separately per framework **and per scenario** so neither a difference in EF Core version behavior nor a `load` vs `reload` leg (see "Summary by test" above) is hidden by averaging across them - a `reload` leg hits an already-warm local store on both sides of the comparison, which would otherwise flatten a real cached-vs-non-cached difference seen on `load`. For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case.

| Project | Framework | Test | Scenario | Non-cached median (ms) | Cached median (ms) | Delta % |
|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net10.0 | Blog.SingleOrDefault(BlogId==1) [cold] | load | 7.275 | 7.410 | +1.8% |
| KEFCore.Benchmark.Test | net10.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | load | 2.510 | 7.075 | +181.9% |
| KEFCore.Benchmark.Test | net8.0 | Blog.SingleOrDefault(BlogId==1) [cold] | load | 8.701 | 8.669 | -0.4% |
| KEFCore.Benchmark.Test | net8.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | load | 3.212 | 8.502 | +164.6% |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | load | 7.854 | 7.877 | +0.3% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | load | 2.885 | 7.710 | +167.2% |
| KEFCore.Complex.Test | net10.0 | DataLoad | load | 363.425 | 402.500 | +10.8% |
| KEFCore.Complex.Test | net8.0 | DataLoad | load | 517.228 | 462.395 | -10.6% |
| KEFCore.Complex.Test | net9.0 | DataLoad | load | 363.582 | 361.066 | -0.7% |

## Cross-framework comparison

For the same project/test/cache-bucket/scenario, how the median elapsed time compares across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from more than one framework.

| Project | Test | Cache | Scenario | net10.0 median (ms) | net8.0 median (ms) | net9.0 median (ms) |
|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | load | 7.275 | 8.701 | 7.854 |
| KEFCore.Benchmark.Test | Blog.SingleOrDefault(BlogId==1) [cold] | cached | load | 7.410 | 8.669 | 7.877 |
| KEFCore.Benchmark.Test | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | load | 2.510 | 3.212 | 2.885 |
| KEFCore.Benchmark.Test | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | load | 7.075 | 8.502 | 7.710 |
| KEFCore.Complex.Test | DataLoad | non-cached | load | 363.425 | 517.228 | 363.582 |
| KEFCore.Complex.Test | DataLoad | cached | load | 402.500 | 462.395 | 361.066 |
