#!/usr/bin/env python3
"""
Aggregates and analyzes the JSON Lines files produced by ProgramConfig.ReportResult
(see test/Common/ProgramConfig.cs) across one or more KEFCore.*.Test runs, and writes
a Markdown report.

Each input line is expected to be a JSON object with the shape:
  {
    "timestamp": "2026-07-25T10:00:00.0000000Z",
    "project": "KEFCore.Benchmark.Test",
    "framework": "net9.0",
    "testId": "Blogs.Where(BlogId==1).Select(Url) [scalar projection]",
    "elapsedMs": 12.34,
    "success": true,
    "details": "Max ... Min ... Mean ... Median ...",
    "forwardCacheTimeout": -1,
    "reverseCacheTimeout": -1,
    "loadApplicationData": true,
    "backendLabel": "KafkaStreams.Raw"
  }

"framework" (e.g. "net8.0"/"net9.0"/"net10.0", which corresponds directly to the EF Core major
version under test) lets this script compare/aggregate results across the different .NET/EF Core
versions the CI matrix runs against, not just across cache buckets. Records from before this field
existed are grouped under "unknown".

"loadApplicationData" tells apart a "load" CI leg (data seeded fresh this run, /p:LoadApplicationData=true,
the default) from a "reload" leg (data already written by a previous process invocation and read back,
/p:LoadApplicationData=false - see KEFCore.Complex.Test). Both legs can report measurements against the
same (project, framework, cache) combination, but a reload leg hits an already-warm local store, so mixing
the two together in the same group understates any real cached-vs-non-cached difference. Records from
before this field existed default to True (load), matching the field's actual default in ProgramConfig.

"backendLabel" identifies which Kafka Streams backend/topology config produced this record - the file name
(without extension) of the /f: config file the process was launched with, e.g. "KafkaStreams.Raw",
"KNetStreams.Buffered.Prefetch", "KNetReplicator". KEFCore.Benchmark.Test's Linux CI leg (build_common.yaml)
runs the same binary up to 7 times per matrix cell, once per backend config, all appending to the same
--input file - without this field there is nothing in the record itself distinguishing native Kafka Streams
from KNetStreams, Raw from Buffered persistence, or prefetch on/off, so every report that groups by
(project, framework, testId, cache, scenario) alone silently averages backends with genuinely different
performance profiles into one number (observed spread: a non-cached IterationTotal median ranging
123ms-197ms across the 6 backends in a single matrix cell). Records from before this field existed, or from
projects/legs that only ever run one backend (KEFCore.Complex.Test's CI legs, non-Linux runners), have
backendLabel None and are grouped under the "(single backend)" label - see backend_group_label below.

Usage:
    python analyze_results.py --input run1.jsonl run2.jsonl --output report.md
    python analyze_results.py --input results/*.jsonl --output report.md --title "KEFCore benchmark run"

Comparing against a previous run (e.g. before/after a PR merges), with an automatic verdict:
    python analyze_results.py --input after/*.jsonl --baseline before/*.jsonl --output report.md

No third-party dependencies; standard library only.
"""

from __future__ import annotations

import argparse
import json
import statistics
import sys
from collections import defaultdict
from dataclasses import dataclass, field
from datetime import datetime, timezone
from pathlib import Path


@dataclass
class Record:
    timestamp: str
    project: str
    framework: str
    testId: str
    elapsedMs: float
    success: bool
    details: str | None
    forwardCacheTimeout: float
    reverseCacheTimeout: float
    loadApplicationData: bool
    backendLabel: str | None
    source_file: str

    @property
    def cache_enabled(self) -> bool:
        # Matches KEFCoreCachedValueBufferStore.IsEnabled semantics: TTL > 0 means caching is active.
        # See the projection push-down design notes for why this exact comparison matters (a naive
        # "!= 0" check would misclassify negative TTLs, which are the disabled-cache default in tests).
        return self.forwardCacheTimeout is not None and self.forwardCacheTimeout > 0

    @property
    def scenario_label(self) -> str:
        # "load": data seeded fresh this process invocation. "reload": data read back from a previous
        # invocation (process restarted, local store already warm) - see loadApplicationData above.
        return "load" if self.loadApplicationData else "reload"

    @property
    def backend_group_label(self) -> str:
        # "(single backend)" for records with no backendLabel (older records, or a leg that only ever
        # runs one backend, e.g. KEFCore.Complex.Test) so they still form one clean group instead of a
        # confusing "None" - real multi-backend legs (KEFCore.Benchmark.Test on Linux) get their actual
        # backendLabel instead, so distinct backends never collapse into that shared "no label" bucket.
        return self.backendLabel if self.backendLabel else "(single backend)"


def load_records(paths: list[Path]) -> list[Record]:
    records: list[Record] = []
    for path in paths:
        with open(path, encoding="utf-8") as f:
            for lineno, line in enumerate(f, start=1):
                line = line.strip()
                if not line:
                    continue
                try:
                    obj = json.loads(line)
                except json.JSONDecodeError as e:
                    print(f"warning: skipping malformed line {path}:{lineno}: {e}", file=sys.stderr)
                    continue
                try:
                    records.append(Record(
                        timestamp=obj.get("timestamp", ""),
                        project=obj.get("project", "unknown"),
                        framework=obj.get("framework", "unknown"),
                        testId=obj.get("testId", "unknown"),
                        elapsedMs=float(obj["elapsedMs"]),
                        success=bool(obj.get("success", True)),
                        details=obj.get("details"),
                        forwardCacheTimeout=float(obj.get("forwardCacheTimeout", -1)),
                        reverseCacheTimeout=float(obj.get("reverseCacheTimeout", -1)),
                        loadApplicationData=bool(obj.get("loadApplicationData", True)),
                        backendLabel=obj.get("backendLabel"),
                        source_file=str(path),
                    ))
                except (KeyError, TypeError, ValueError) as e:
                    print(f"warning: skipping record with unexpected shape {path}:{lineno}: {e}", file=sys.stderr)
    return records


@dataclass
class GroupStats:
    count: int
    mean_ms: float
    median_ms: float
    min_ms: float
    max_ms: float
    stdev_ms: float | None
    success_rate: float


def summarize(records: list[Record]) -> GroupStats:
    values = [r.elapsedMs for r in records]
    successes = sum(1 for r in records if r.success)
    return GroupStats(
        count=len(records),
        mean_ms=statistics.fmean(values),
        median_ms=statistics.median(values),
        min_ms=min(values),
        max_ms=max(values),
        stdev_ms=statistics.stdev(values) if len(values) > 1 else None,
        success_rate=successes / len(records) if records else 0.0,
    )


def fmt_ms(v: float) -> str:
    return f"{v:.3f}"


def verdict(delta_pct: float, delta_ms: float, threshold_pct: float, threshold_abs_ms: float) -> str:
    # Requires BOTH thresholds to be exceeded, not just the percent one. Rationale (found by
    # inspecting a real CI report): sub-millisecond queries routinely show 10-40% swings between
    # runs that are just measurement noise (GC pauses, JIT warmup, OS scheduling jitter) - a percent
    # threshold alone flags these as REGRESSION/IMPROVEMENT even though the absolute difference is
    # a fraction of a millisecond and not a real change. Requiring an absolute floor too keeps
    # sensitivity on operations where a real regression would show up as a meaningful number of ms.
    if delta_pct > threshold_pct and delta_ms > threshold_abs_ms:
        return "REGRESSION"
    if delta_pct < -threshold_pct and delta_ms < -threshold_abs_ms:
        return "IMPROVEMENT"
    return "no significant change"


# Case-insensitive substring match against testId: identifies the headline-worthy synthetic
# measurements (see Benchmark.Test's TestNames "IterationTotal (sum of all queries above, not
# comparable to them individually)", and Complex.Test's "ScalarOnlyProjection_BlogId10_Url") as
# at-a-glance numbers for a run, distinct from the per-query breakdown in "Summary by test".
# Matched by pattern, not an exact/hardcoded name, so it keeps working if the exact wording changes.
#
# ScalarOnlyProjection is included separately from IterationTotal (rather than only appearing
# summed into IterationTotal) because it is the only one of Complex.Test's two projection queries
# where a cached-vs-non-cached delta can mean anything: KEFCoreQueryExpression.GetProjectedProperties()
# falls back to "no narrowing possible, use the full entity" whenever a projection element binds to a
# complex-type property (see its docstring), which NestedComplexTypeProjection always does (it reads
# b.PricingInfo.Tax.Code / TaxInfoExtended.CodeExtended). So push-down never engages for that query
# regardless of the cache TTL, and any cached/non-cached difference on it is measurement noise, not
# signal. Summing it into IterationTotal drowns out whatever real push-down signal ScalarOnlyProjection
# (a plain scalar property read, narrowing-eligible) would otherwise show. IterationTotal is kept too,
# since it's still the right number for "how long does a full pass over this project's queries take".
HEADLINE_PATTERNS: tuple[tuple[str, str], ...] = (
    ("iterationtotal", "IterationTotal"),
    ("scalaronlyprojection", "ScalarOnlyProjection"),
)


def headline_label(test_id: str) -> str | None:
    """Returns the short display label for `test_id` if it's headline-worthy, else None."""
    tl = test_id.lower()
    for pattern, label in HEADLINE_PATTERNS:
        if pattern in tl:
            return label
    return None


def build_report(records: list[Record], title: str, baseline_records: list[Record] | None = None,
                  regression_threshold_pct: float = 5.0, regression_threshold_abs_ms: float = 1.0,
                  control_test_patterns: list[str] | None = None) -> str:
    lines: list[str] = []
    lines.append(f"# {title}")
    lines.append("")
    lines.append(f"Generated: {datetime.now(timezone.utc).isoformat()}")
    lines.append("")
    lines.append(f"Total records analyzed: **{len(records)}**")
    lines.append("")

    if not records:
        lines.append("_No records to report._")
        return "\n".join(lines)

    # --- Headline: one at-a-glance number per (project, framework, test, cache-bucket, scenario, backend)
    # The single most useful numbers to eyeball first, before any detailed table. Uses the
    # headline-worthy synthetic/scalar measurements (see HEADLINE_PATTERNS) when present. `Test`
    # is its own key column (not merged away) because different headline tests can have genuinely
    # different cached-vs-non-cached behavior for the same project - see HEADLINE_PATTERNS'
    # docstring re: IterationTotal vs ScalarOnlyProjection on Complex.Test. Scenario ("load" vs
    # "reload") is likewise its own key column rather than folded into cache, so a reload leg
    # (already-warm local store) never gets silently averaged together with a load leg under the
    # same cache setting - see Record.scenario_label. `Backend` (Record.backend_group_label) is
    # kept separate too: KEFCore.Benchmark.Test's Linux CI leg reports up to 7 different Kafka
    # Streams backend configs per matrix cell (native Kafka Streams vs KNetStreams, Raw vs Buffered,
    # with/without prefetch), and those have genuinely different performance profiles - averaging
    # them away is what made a non-cached IterationTotal spread of 123ms-197ms collapse into one
    # uninformative median. See backendLabel in the module docstring.
    headline_groups: dict[tuple[str, str, str, bool, str, str], list[Record]] = defaultdict(list)
    for r in records:
        label = headline_label(r.testId)
        if label is not None:
            headline_groups[(r.project, r.framework, label, r.cache_enabled, r.scenario_label, r.backend_group_label)].append(r)

    lines.append("## Headline")
    lines.append("")
    if not headline_groups:
        lines.append("_No test's `testId` matches any headline pattern, so no at-a-glance number is "
                      "available. See \"Summary by test\" below for the full per-query breakdown._")
    else:
        lines.append("Median wall-clock time for the headline-worthy queries, the numbers most useful "
                      "to check first. `IterationTotal` is the sum of all queries in that project's fixed "
                      "sequence; other rows are individual queries worth watching on their own — see the "
                      "module docstring for why. `Scenario` is `load` (data seeded this run) or `reload` "
                      "(data read back from a previous invocation, local store already warm). `Backend` is "
                      "the Kafka Streams config the run used (e.g. `KafkaStreams.Raw`, "
                      "`KNetStreams.Buffered.Prefetch`), or `(single backend)` for a project/leg that only "
                      "ever exercises one.")
        lines.append("")
        lines.append("| Project | Framework | Test | Cache | Scenario | Backend | Median (ms) | N |")
        lines.append("|---|---|---|---|---|---|---|---|")
        for (project, framework, label, cached, scenario, backend), group_records in sorted(headline_groups.items(), key=lambda kv: kv[0]):
            s = summarize(group_records)
            cache_label = "cached" if cached else "non-cached"
            lines.append(f"| {project} | {framework} | {label} | {cache_label} | {scenario} | {backend} | {fmt_ms(s.median_ms)} | {s.count} |")
    lines.append("")

    # --- Comparison vs baseline, when --baseline was supplied -------------------------------
    if baseline_records is not None:
        lines.append("## Comparison vs baseline")
        lines.append("")
        lines.append(f"Delta % and verdict, per `(project, framework, testId, cache, scenario, backend)` present "
                      f"in both runs. "
                      f"Positive delta = current run slower (regression); negative = faster (improvement). "
                      f"A verdict only fires when **both** thresholds are exceeded: **±{regression_threshold_pct:.0f}%** "
                      f"**and** **±{regression_threshold_abs_ms:.2f} ms** absolute — this avoids flagging noise on "
                      f"sub-millisecond queries, where a large percent swing can still be an insignificant "
                      f"absolute difference (e.g. GC pauses, JIT warmup, OS scheduling jitter).")
        lines.append("")

        current_groups: dict[tuple[str, str, str, bool, str, str], list[Record]] = defaultdict(list)
        for r in records:
            current_groups[(r.project, r.framework, r.testId, r.cache_enabled, r.scenario_label, r.backend_group_label)].append(r)
        baseline_groups: dict[tuple[str, str, str, bool, str, str], list[Record]] = defaultdict(list)
        for r in baseline_records:
            baseline_groups[(r.project, r.framework, r.testId, r.cache_enabled, r.scenario_label, r.backend_group_label)].append(r)

        raw_rows = []
        for key in sorted(set(current_groups) & set(baseline_groups)):
            project, framework, test_id, cached, scenario, backend = key
            cur_stats = summarize(current_groups[key])
            base_stats = summarize(baseline_groups[key])
            if base_stats.median_ms == 0:
                continue
            delta_ms = cur_stats.median_ms - base_stats.median_ms
            delta_pct = delta_ms / base_stats.median_ms * 100
            is_control = control_test_patterns is not None and any(p in test_id.lower() for p in control_test_patterns)
            raw_rows.append({"project": project, "framework": framework, "test_id": test_id, "cached": cached,
                              "scenario": scenario, "backend": backend, "base_ms": base_stats.median_ms,
                              "cur_ms": cur_stats.median_ms, "delta_pct": delta_pct, "is_control": is_control})

        # --- Environment drift: measured from --control-tests (operations known to be unaffected by
        # the change under test, e.g. write-path operations when the change is read-path only). If the
        # environment itself got faster/slower between the two runs (different runner hardware, shared
        # CI load, etc.), these tests should show ~0% delta but won't - that observed shift is the drift
        # estimate, used below to separate "the code changed" from "the environment changed" for every
        # OTHER test. Opt-in only: with no --control-tests, no drift adjustment is computed or shown.
        drift_pct = None
        if control_test_patterns:
            control_deltas = [r["delta_pct"] for r in raw_rows if r["is_control"]]
            lines.append("### Environment drift")
            lines.append("")
            if not control_deltas:
                lines.append(f"_No test matched the given `--control-tests` patterns "
                              f"(`{', '.join(control_test_patterns)}`), so no drift estimate could be computed._")
            else:
                drift_pct = statistics.median(control_deltas)
                lines.append(f"Median delta across {len(control_deltas)} control test(s) matching "
                              f"`{', '.join(control_test_patterns)}` (operations assumed unaffected by the change "
                              f"under test): **{drift_pct:+.1f}%**. This is used as an estimate of environment-only "
                              f"drift (different runner hardware, shared CI load, etc. between the baseline and "
                              f"current run) and subtracted from every other test's delta below to get an "
                              f"\"env-adjusted\" delta and verdict — shown alongside the untouched raw numbers, "
                              f"never in place of them.")
            lines.append("")

        comparison_rows = []
        for r in raw_rows:
            adj_delta_pct = r["delta_pct"] - drift_pct if drift_pct is not None else None
            adj_delta_ms = adj_delta_pct / 100 * r["base_ms"] if adj_delta_pct is not None else None
            raw_verdict = verdict(r["delta_pct"], r["cur_ms"] - r["base_ms"], regression_threshold_pct, regression_threshold_abs_ms)
            adj_verdict = (verdict(adj_delta_pct, adj_delta_ms, regression_threshold_pct, regression_threshold_abs_ms)
                           if adj_delta_pct is not None else None)
            comparison_rows.append((r["project"], r["framework"], r["test_id"], r["cached"], r["scenario"],
                                     r["backend"], r["base_ms"], r["cur_ms"], r["delta_pct"], raw_verdict,
                                     adj_delta_pct, adj_verdict, r["is_control"]))

        only_in_current = sorted(set(current_groups) - set(baseline_groups))
        only_in_baseline = sorted(set(baseline_groups) - set(current_groups))

        if not comparison_rows:
            lines.append("_No `(project, framework, testId, cache)` combination is present in both the current "
                          "and baseline input, so no comparison can be made._")
        else:
            # Score using the env-adjusted verdict when available (more meaningful signal), the raw
            # verdict otherwise. Control tests themselves are excluded from the scored count: by
            # construction their adjusted delta is ~0 (they define the drift), so counting them as
            # "compared tests" would just be counting the calibration data as if it were signal too.
            scored = [r for r in comparison_rows if not r[12]]
            verdict_index = 11 if drift_pct is not None else 9
            regressions = [r for r in scored if r[verdict_index] == "REGRESSION"]
            improvements = [r for r in scored if r[verdict_index] == "IMPROVEMENT"]
            adjusted_note = " (env-adjusted)" if drift_pct is not None else ""
            if regressions:
                lines.append(f"**⚠️ Overall verdict{adjusted_note}: {len(regressions)} regression(s) detected** "
                              f"(out of {len(scored)} compared tests, {len(improvements)} improved).")
            else:
                lines.append(f"**✅ Overall verdict{adjusted_note}: no regressions detected** "
                              f"(out of {len(scored)} compared tests, {len(improvements)} improved).")
            lines.append("")
            # Sort by the same verdict used for scoring; regressions first, then improvements, then unchanged.
            order = {"REGRESSION": 0, "IMPROVEMENT": 1, "no significant change": 2}
            comparison_rows.sort(key=lambda r: (order[r[verdict_index]], r[0], r[1], r[2], r[3]))
            if drift_pct is None:
                lines.append("| Project | Framework | Test | Cache | Scenario | Backend | Baseline median (ms) | Current median (ms) | Delta % | Verdict |")
                lines.append("|---|---|---|---|---|---|---|---|---|---|")
                for project, framework, test_id, cached, scenario, backend, base_ms, cur_ms, delta_pct, raw_verdict, _, _, _ in comparison_rows:
                    cache_label = "cached" if cached else "non-cached"
                    lines.append(f"| {project} | {framework} | {test_id} | {cache_label} | {scenario} | {backend} | {fmt_ms(base_ms)} | "
                                  f"{fmt_ms(cur_ms)} | {delta_pct:+.1f}% | {raw_verdict} |")
            else:
                lines.append("| Project | Framework | Test | Cache | Scenario | Backend | Baseline (ms) | Current (ms) | Delta % (raw) | "
                              "Delta % (env-adjusted) | Verdict (env-adjusted) |")
                lines.append("|---|---|---|---|---|---|---|---|---|---|---|")
                for project, framework, test_id, cached, scenario, backend, base_ms, cur_ms, delta_pct, raw_verdict, adj_pct, adj_v, is_control in comparison_rows:
                    cache_label = "cached" if cached else "non-cached"
                    control_tag = " _(control)_" if is_control else ""
                    lines.append(f"| {project} | {framework} | {test_id}{control_tag} | {cache_label} | {scenario} | {backend} | {fmt_ms(base_ms)} | "
                                  f"{fmt_ms(cur_ms)} | {delta_pct:+.1f}% | {adj_pct:+.1f}% | {adj_v} |")
        if only_in_current or only_in_baseline:
            lines.append("")
            if only_in_current:
                lines.append(f"_{len(only_in_current)} test(s) present only in the current run (new tests, not "
                              "compared)._")
            if only_in_baseline:
                lines.append(f"_{len(only_in_baseline)} test(s) present only in the baseline run (removed tests, "
                              "not compared)._")
        lines.append("")

    # --- Failures section, always first and prominent -------------------------------------
    failures = [r for r in records if not r.success]
    lines.append("## Failures")
    lines.append("")
    if not failures:
        lines.append("None. All reported outcomes had `success: true`.")
    else:
        lines.append(f"**{len(failures)}** failing record(s):")
        lines.append("")
        lines.append("| Project | Framework | Test | Cache | Scenario | Backend | Elapsed (ms) | Details | Source |")
        lines.append("|---|---|---|---|---|---|---|---|---|")
        for r in failures:
            cache = "cached" if r.cache_enabled else "non-cached"
            details = (r.details or "").replace("|", "\\|")
            lines.append(f"| {r.project} | {r.framework} | {r.testId} | {cache} | {r.scenario_label} | {r.backend_group_label} | {fmt_ms(r.elapsedMs)} | {details} | {r.source_file} |")
    lines.append("")

    # --- Per (project, framework, testId, cache-bucket, scenario, backend) summary ----------
    groups: dict[tuple[str, str, str, bool, str, str], list[Record]] = defaultdict(list)
    for r in records:
        groups[(r.project, r.framework, r.testId, r.cache_enabled, r.scenario_label, r.backend_group_label)].append(r)

    lines.append("## Summary by test")
    lines.append("")
    lines.append("`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly "
                  "to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: "
                  "`cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers "
                  "zero/negative TTL (the default in test configs, e.g. -1 seconds). `Scenario` is `load` (data "
                  "seeded this run) or `reload` (data read back from a previous invocation, local store already "
                  "warm) - kept separate from Cache so the two axes are never silently averaged together. "
                  "`Backend` is the Kafka Streams config the run used (from `backendLabel` - e.g. "
                  "`KafkaStreams.Raw`, `KNetStreams.Buffered.Prefetch`), or `(single backend)` when the "
                  "project/leg only ever exercises one - see the module docstring for why this matters "
                  "(KEFCore.Benchmark.Test's Linux CI leg runs up to 7 different backends per matrix cell).")
    lines.append("")
    lines.append("| Project | Framework | Test | Cache | Scenario | Backend | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|---|---|---|")
    for (project, framework, test_id, cached, scenario, backend), group_records in sorted(groups.items(), key=lambda kv: kv[0]):
        s = summarize(group_records)
        stdev = fmt_ms(s.stdev_ms) if s.stdev_ms is not None else "n/a"
        cache_label = "cached" if cached else "non-cached"
        lines.append(
            f"| {project} | {framework} | {test_id} | {cache_label} | {scenario} | {backend} | {s.count} | {fmt_ms(s.mean_ms)} | {fmt_ms(s.median_ms)} | "
            f"{fmt_ms(s.min_ms)} | {fmt_ms(s.max_ms)} | {stdev} | {s.success_rate:.0%} |"
        )
    lines.append("")

    # --- Cached vs non-cached delta, for (project, framework, testId, scenario) present in both buckets
    lines.append("## Cached vs non-cached delta")
    lines.append("")
    lines.append("Positive `delta %` means the cached run was slower than the non-cached run for that same test "
                  "(higher median), computed separately per framework, **per scenario**, and **per backend** so "
                  "none of a difference in EF Core version behavior, a `load` vs `reload` leg (see \"Summary by "
                  "test\" above), or a difference between Kafka Streams backend configs is hidden by averaging "
                  "across them - a `reload` leg hits an already-warm local store on both sides of the comparison, "
                  "which would otherwise flatten a real cached-vs-non-cached difference seen on `load`; likewise, "
                  "KEFCore.Benchmark.Test's Linux CI leg reports up to 7 backends per matrix cell (native Kafka "
                  "Streams vs KNetStreams, Raw vs Buffered persistence, with/without prefetch — see `backendLabel` "
                  "in the module docstring), and those have different enough performance profiles on their own "
                  "that averaging them together can swamp a real caching effect. For a test whose non-cached path "
                  "benefits from projection push-down, a consistently "
                  "positive delta here is expected — projection is intentionally skipped when the per-entity cache "
                  "is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch cost in that "
                  "case. Not every test qualifies: `KEFCoreQueryExpression.GetProjectedProperties()` falls back to "
                  "\"use the full entity, no narrowing\" whenever a projection element binds to a complex-type "
                  "property, so a test like `NestedComplexTypeProjection_BlogId10_TaxCode` is never push-down "
                  "eligible in the first place — its delta here should hover near zero regardless of framework, "
                  "and a nonzero-looking value is measurement noise, not a caching effect.")
    lines.append("")
    test_keys = {(project, framework, test_id, scenario, backend)
                 for (project, framework, test_id, _cached, scenario, backend) in groups}
    delta_rows = []
    for project, framework, test_id, scenario, backend in sorted(test_keys):
        cached_group = groups.get((project, framework, test_id, True, scenario, backend))
        noncached_group = groups.get((project, framework, test_id, False, scenario, backend))
        if not cached_group or not noncached_group:
            continue
        cached_stats = summarize(cached_group)
        noncached_stats = summarize(noncached_group)
        if noncached_stats.median_ms == 0:
            continue
        delta_pct = (cached_stats.median_ms - noncached_stats.median_ms) / noncached_stats.median_ms * 100
        delta_rows.append((project, framework, test_id, scenario, backend, noncached_stats.median_ms,
                            cached_stats.median_ms, delta_pct))

    if not delta_rows:
        lines.append("_No test has records in both cache buckets for the same scenario and backend, so no delta "
                      "can be computed. Run the same scenario/backend once with `/p:ForwardCacheTimeout=-1` and "
                      "once with a positive value (e.g. `/p:ForwardCacheTimeout=60`), keeping "
                      "`/p:LoadApplicationData` and the `/f:` config file consistent between the two, and pass "
                      "both result files to this script to populate this section._")
    else:
        lines.append("| Project | Framework | Test | Scenario | Backend | Non-cached median (ms) | Cached median (ms) | Delta % |")
        lines.append("|---|---|---|---|---|---|---|---|")
        for project, framework, test_id, scenario, backend, noncached_median, cached_median, delta_pct in delta_rows:
            lines.append(
                f"| {project} | {framework} | {test_id} | {scenario} | {backend} | {fmt_ms(noncached_median)} | {fmt_ms(cached_median)} | {delta_pct:+.1f}% |"
            )
    lines.append("")

    # --- Cross-framework comparison, for (project, testId, cache-bucket, scenario, backend) present in 2+ frameworks
    lines.append("## Cross-framework comparison")
    lines.append("")
    lines.append("For the same project/test/cache-bucket/scenario/backend, how the median elapsed time compares "
                  "across the .NET/EF Core versions the CI matrix runs against. Only shown for combinations with "
                  "data from more than one framework.")
    lines.append("")
    by_test_cache: dict[tuple[str, str, bool, str, str], dict[str, GroupStats]] = defaultdict(dict)
    for (project, framework, test_id, cached, scenario, backend), group_records in groups.items():
        by_test_cache[(project, test_id, cached, scenario, backend)][framework] = summarize(group_records)

    frameworks_present = sorted({fw for (_p, fw, _t, _c, _s, _b) in groups})
    multi_framework_rows = {k: v for k, v in by_test_cache.items() if len(v) > 1}
    if not multi_framework_rows or len(frameworks_present) < 2:
        lines.append("_No test has records from more than one framework, so no cross-framework comparison can be "
                      "made. Pass result files from multiple `net*.0` runs together to populate this section._")
    else:
        header = "| Project | Test | Cache | Scenario | Backend | " + " | ".join(f"{fw} median (ms)" for fw in frameworks_present) + " |"
        sep = "|---|---|---|---|---|" + "---|" * len(frameworks_present)
        lines.append(header)
        lines.append(sep)
        for (project, test_id, cached, scenario, backend), per_fw in sorted(multi_framework_rows.items(), key=lambda kv: kv[0]):
            cache_label = "cached" if cached else "non-cached"
            cells = [fmt_ms(per_fw[fw].median_ms) if fw in per_fw else "n/a" for fw in frameworks_present]
            lines.append(f"| {project} | {test_id} | {cache_label} | {scenario} | {backend} | " + " | ".join(cells) + " |")
    lines.append("")

    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--input", nargs="+", required=True, help="One or more JSON Lines files (as produced by ReportResult) for the CURRENT run")
    parser.add_argument("--baseline", nargs="+", default=None,
                         help="Optional: one or more JSON Lines files for a PREVIOUS/baseline run to compare "
                              "--input against. When given, adds a 'Comparison vs baseline' section with an "
                              "automatic verdict (regression/improvement/no significant change) per test.")
    parser.add_argument("--regression-threshold", type=float, default=5.0,
                         help="Percent delta (median elapsed time) beyond which a --baseline comparison is "
                              "called a regression/improvement rather than 'no significant change'. Default: 5.0")
    parser.add_argument("--regression-threshold-abs-ms", type=float, default=1.0,
                         help="Absolute delta in milliseconds (median elapsed time) that must ALSO be exceeded, "
                              "alongside --regression-threshold, for a verdict to be REGRESSION/IMPROVEMENT. "
                              "Prevents noise on sub-millisecond queries (e.g. a 0.05ms -> 0.07ms swing is a "
                              "40% delta but an insignificant absolute one) from being flagged. Default: 1.0")
    parser.add_argument("--control-tests", nargs="+", default=None,
                         help="Optional (used with --baseline): one or more substrings (case-insensitive, matched "
                              "against testId) identifying tests known to be UNAFFECTED by the change under test "
                              "(e.g. write-path operations like SaveChanges/LocalStoreSynchronized when the change "
                              "is read-path only). Their median delta is used as an environment-drift estimate "
                              "(different runner hardware, shared CI load, etc. between the two runs) and "
                              "subtracted from every other test's delta to produce an env-adjusted delta/verdict, "
                              "shown alongside (never instead of) the raw ones.")
    parser.add_argument("--output", required=True, help="Path to write the Markdown report to")
    parser.add_argument("--title", default="KEFCore test results analysis", help="Report title")
    args = parser.parse_args()

    paths = [Path(p) for p in args.input]
    missing = [p for p in paths if not p.exists()]
    if missing:
        print(f"error: input file(s) not found: {', '.join(str(p) for p in missing)}", file=sys.stderr)
        return 1

    records = load_records(paths)

    baseline_records = None
    if args.baseline:
        baseline_paths = [Path(p) for p in args.baseline]
        missing_baseline = [p for p in baseline_paths if not p.exists()]
        if missing_baseline:
            print(f"error: baseline file(s) not found: {', '.join(str(p) for p in missing_baseline)}", file=sys.stderr)
            return 1
        baseline_records = load_records(baseline_paths)

    control_test_patterns = [p.lower() for p in args.control_tests] if args.control_tests else None

    report = build_report(records, args.title, baseline_records=baseline_records,
                           regression_threshold_pct=args.regression_threshold,
                           regression_threshold_abs_ms=args.regression_threshold_abs_ms,
                           control_test_patterns=control_test_patterns)

    out_path = Path(args.output)
    out_path.write_text(report, encoding="utf-8")
    baseline_note = f", compared against {len(baseline_records)} baseline record(s)" if baseline_records is not None else ""
    print(f"Wrote {out_path} ({len(records)} records from {len(paths)} file(s){baseline_note}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
