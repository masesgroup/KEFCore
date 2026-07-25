# Example run (synthetic data, multi-framework)

Generated: 2026-07-25T19:51:46.757460+00:00

Total records analyzed: **1207**

## Failures

**1** failing record(s):

| Project | Framework | Test | Cache | Elapsed (ms) | Details | Source |
|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized | cached | 5000.000 | timed out waiting for synchronization | example/sample-cached.jsonl |

## Summary by test

`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds).

| Project | Framework | Test | Cache | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net10.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | 100 | 7.280 | 7.275 | 5.329 | 9.632 | 0.786 | 100% |
| KEFCore.Benchmark.Test | net10.0 | Blog.SingleOrDefault(BlogId==1) [cold] | cached | 100 | 7.399 | 7.410 | 5.892 | 9.597 | 0.661 | 100% |
| KEFCore.Benchmark.Test | net10.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | 100 | 2.569 | 2.510 | 1.462 | 3.507 | 0.481 | 100% |
| KEFCore.Benchmark.Test | net10.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | 100 | 7.105 | 7.075 | 5.247 | 8.593 | 0.692 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | 100 | 8.652 | 8.701 | 6.242 | 10.702 | 0.793 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blog.SingleOrDefault(BlogId==1) [cold] | cached | 100 | 8.717 | 8.669 | 6.619 | 11.697 | 1.012 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | 100 | 3.187 | 3.212 | 1.912 | 4.242 | 0.483 | 100% |
| KEFCore.Benchmark.Test | net8.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | 100 | 8.540 | 8.502 | 6.650 | 10.551 | 0.908 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | 100 | 7.814 | 7.854 | 5.774 | 9.775 | 0.805 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | cached | 100 | 7.809 | 7.877 | 5.997 | 9.293 | 0.683 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | 100 | 2.884 | 2.885 | 1.665 | 3.969 | 0.479 | 100% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | 100 | 7.819 | 7.710 | 5.208 | 10.927 | 0.945 | 100% |
| KEFCore.Complex.Test | net10.0 | DataLoad | non-cached | 1 | 363.425 | 363.425 | 363.425 | 363.425 | n/a | 100% |
| KEFCore.Complex.Test | net10.0 | DataLoad | cached | 1 | 402.500 | 402.500 | 402.500 | 402.500 | n/a | 100% |
| KEFCore.Complex.Test | net8.0 | DataLoad | non-cached | 1 | 517.228 | 517.228 | 517.228 | 517.228 | n/a | 100% |
| KEFCore.Complex.Test | net8.0 | DataLoad | cached | 1 | 462.395 | 462.395 | 462.395 | 462.395 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | DataLoad | non-cached | 1 | 363.582 | 363.582 | 363.582 | 363.582 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | DataLoad | cached | 1 | 361.066 | 361.066 | 361.066 | 361.066 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized | cached | 1 | 5000.000 | 5000.000 | 5000.000 | 5000.000 | n/a | 0% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median), computed separately per framework so a difference in EF Core version behavior isn't hidden by averaging across them. For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case.

| Project | Framework | Test | Non-cached median (ms) | Cached median (ms) | Delta % |
|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | net10.0 | Blog.SingleOrDefault(BlogId==1) [cold] | 7.275 | 7.410 | +1.8% |
| KEFCore.Benchmark.Test | net10.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | 2.510 | 7.075 | +181.9% |
| KEFCore.Benchmark.Test | net8.0 | Blog.SingleOrDefault(BlogId==1) [cold] | 8.701 | 8.669 | -0.4% |
| KEFCore.Benchmark.Test | net8.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | 3.212 | 8.502 | +164.6% |
| KEFCore.Benchmark.Test | net9.0 | Blog.SingleOrDefault(BlogId==1) [cold] | 7.854 | 7.877 | +0.3% |
| KEFCore.Benchmark.Test | net9.0 | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | 2.885 | 7.710 | +167.2% |
| KEFCore.Complex.Test | net10.0 | DataLoad | 363.425 | 402.500 | +10.8% |
| KEFCore.Complex.Test | net8.0 | DataLoad | 517.228 | 462.395 | -10.6% |
| KEFCore.Complex.Test | net9.0 | DataLoad | 363.582 | 361.066 | -0.7% |

## Cross-framework comparison

For the same project/test/cache-bucket, how the median elapsed time compares across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from more than one framework.

| Project | Test | Cache | net10.0 median (ms) | net8.0 median (ms) | net9.0 median (ms) |
|---|---|---|---|---|---|
| KEFCore.Benchmark.Test | Blog.SingleOrDefault(BlogId==1) [cold] | non-cached | 7.275 | 8.701 | 7.854 |
| KEFCore.Benchmark.Test | Blog.SingleOrDefault(BlogId==1) [cold] | cached | 7.410 | 8.669 | 7.877 |
| KEFCore.Benchmark.Test | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | non-cached | 2.510 | 3.212 | 2.885 |
| KEFCore.Benchmark.Test | Blogs.Where(BlogId==1).Select(Url) [scalar projection] | cached | 7.075 | 8.502 | 7.710 |
| KEFCore.Complex.Test | DataLoad | non-cached | 363.425 | 517.228 | 363.582 |
| KEFCore.Complex.Test | DataLoad | cached | 402.500 | 462.395 | 361.066 |
