# Example: environment-drift-adjusted comparison (synthetic data)

Generated: 2026-09-03T01:00:15.449214+00:00

Total records analyzed: **5**

## Headline

Median wall-clock time for the headline-worthy queries, the numbers most useful to check first. `IterationTotal` is the sum of all queries in that project's fixed sequence; other rows are individual queries worth watching on their own — see the module docstring for why. `Scenario` is `load` (data seeded this run) or `reload` (data read back from a previous invocation, local store already warm). `Backend` is the Kafka Streams config the run used (e.g. `KafkaStreams.Raw`, `KNetStreams.Buffered.Prefetch`), or `(single backend)` for a project/leg that only ever exercises one.

| Project | Framework | Test | Cache | Scenario | Backend | Median (ms) | N |
|---|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | ScalarOnlyProjection | non-cached | load | (single backend) | 7.600 | 1 |

## Comparison vs baseline

Delta % and verdict, per `(project, framework, testId, cache, scenario, backend)` present in both runs. Positive delta = current run slower (regression); negative = faster (improvement). A verdict only fires when **both** thresholds are exceeded: **±5%** **and** **±0.50 ms** absolute — this avoids flagging noise on sub-millisecond queries, where a large percent swing can still be an insignificant absolute difference (e.g. GC pauses, JIT warmup, OS scheduling jitter).

### Environment drift

Median delta across 3 control test(s) matching `savechanges, localstoresynchronized, dataload` (operations assumed unaffected by the change under test): **-10.0%**. This is used as an estimate of environment-only drift (different runner hardware, shared CI load, etc. between the baseline and current run) and subtracted from every other test's delta below to get an "env-adjusted" delta and verdict — shown alongside the untouched raw numbers, never in place of them.

**⚠️ Overall verdict (env-adjusted): 1 regression(s) detected** (out of 2 compared tests, 0 improved).

| Project | Framework | Test | Cache | Scenario | Backend | Baseline (ms) | Current (ms) | Delta % (raw) | Delta % (env-adjusted) | Verdict (env-adjusted) |
|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | NestedComplexTypeProjection_BlogId10_TaxCode | non-cached | load | (single backend) | 400.000 | 388.800 | -2.8% | +7.2% | REGRESSION |
| KEFCore.Complex.Test | net9.0 | DataLoad _(control)_ | non-cached | load | (single backend) | 6000.000 | 5430.000 | -9.5% | +0.5% | no significant change |
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized _(control)_ | non-cached | load | (single backend) | 3000.000 | 2700.000 | -10.0% | +0.0% | no significant change |
| KEFCore.Complex.Test | net9.0 | SaveChanges _(control)_ | non-cached | load | (single backend) | 7000.000 | 6300.000 | -10.0% | +0.0% | no significant change |
| KEFCore.Complex.Test | net9.0 | ScalarOnlyProjection_BlogId10_Url | non-cached | load | (single backend) | 8.000 | 7.600 | -5.0% | +5.0% | no significant change |

## Failures

None. All reported outcomes had `success: true`.

## Summary by test

`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL (the default in test configs, e.g. -1 seconds). `Scenario` is `load` (data seeded this run) or `reload` (data read back from a previous invocation, local store already warm) - kept separate from Cache so the two axes are never silently averaged together. `Backend` is the Kafka Streams config the run used (from `backendLabel` - e.g. `KafkaStreams.Raw`, `KNetStreams.Buffered.Prefetch`), or `(single backend)` when the project/leg only ever exercises one - see the module docstring for why this matters (KEFCore.Benchmark.Test's Linux CI leg runs up to 7 different backends per matrix cell).

| Project | Framework | Test | Cache | Scenario | Backend | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |
|---|---|---|---|---|---|---|---|---|---|---|---|---|
| KEFCore.Complex.Test | net9.0 | DataLoad | non-cached | load | (single backend) | 1 | 5430.000 | 5430.000 | 5430.000 | 5430.000 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | LocalStoreSynchronized | non-cached | load | (single backend) | 1 | 2700.000 | 2700.000 | 2700.000 | 2700.000 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | NestedComplexTypeProjection_BlogId10_TaxCode | non-cached | load | (single backend) | 1 | 388.800 | 388.800 | 388.800 | 388.800 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | SaveChanges | non-cached | load | (single backend) | 1 | 6300.000 | 6300.000 | 6300.000 | 6300.000 | n/a | 100% |
| KEFCore.Complex.Test | net9.0 | ScalarOnlyProjection_BlogId10_Url | non-cached | load | (single backend) | 1 | 7.600 | 7.600 | 7.600 | 7.600 | n/a | 100% |

## Cached vs non-cached delta

Positive `delta %` means the cached run was slower than the non-cached run for that same test (higher median), computed separately per framework, **per scenario**, and **per backend** so none of a difference in EF Core version behavior, a `load` vs `reload` leg (see "Summary by test" above), or a difference between Kafka Streams backend configs is hidden by averaging across them - a `reload` leg hits an already-warm local store on both sides of the comparison, which would otherwise flatten a real cached-vs-non-cached difference seen on `load`; likewise, KEFCore.Benchmark.Test's Linux CI leg reports up to 7 backends per matrix cell (native Kafka Streams vs KNetStreams, Raw vs Buffered persistence, with/without prefetch — see `backendLabel` in the module docstring), and those have different enough performance profiles on their own that averaging them together can swamp a real caching effect. For a test whose non-cached path benefits from projection push-down, a consistently positive delta here is expected — projection is intentionally skipped when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that case. Not every test qualifies: `KEFCoreQueryExpression.GetProjectedProperties()` falls back to "use the full entity, no narrowing" whenever a projection element binds to a complex-type property, so a test like `NestedComplexTypeProjection_BlogId10_TaxCode` is never push-down eligible in the first place — its delta here should hover near zero regardless of framework, and a nonzero-looking value is measurement noise, not a caching effect.

_No test has records in both cache buckets for the same scenario and backend, so no delta can be computed. Run the same scenario/backend once with `/p:ForwardCacheTimeout=-1` and once with a positive value (e.g. `/p:ForwardCacheTimeout=60`), keeping `/p:LoadApplicationData` and the `/f:` config file consistent between the two, and pass both result files to this script to populate this section._

## Cross-framework comparison

For the same project/test/cache-bucket/scenario/backend, how the median elapsed time compares across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from more than one framework.

_No test has records from more than one framework, so no cross-framework comparison can be made. Pass result files from multiple `net*.0` runs together to populate this section._
