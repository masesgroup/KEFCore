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
    "reverseCacheTimeout": -1
  }

"framework" (e.g. "net8.0"/"net9.0"/"net10.0", which corresponds directly to the EF Core major
version under test) lets this script compare/aggregate results across the different .NET/EF Core
versions the CI matrix runs against, not just across cache buckets. Records from before this field
existed are grouped under "unknown".

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
    source_file: str

    @property
    def cache_enabled(self) -> bool:
        # Matches KEFCoreCachedValueBufferStore.IsEnabled semantics: TTL > 0 means caching is active.
        # See the projection push-down design notes for why this exact comparison matters (a naive
        # "!= 0" check would misclassify negative TTLs, which are the disabled-cache default in tests).
        return self.forwardCacheTimeout is not None and self.forwardCacheTimeout > 0


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


# Case-insensitive substring match against testId: identifies the "whole iteration" synthetic
# measurement (see Benchmark.Test's TestNames — "IterationTotal (sum of all queries above, not
# comparable to them individually)") as the single at-a-glance headline number for a run, distinct
# from the per-query breakdown in "Summary by test". Matched by pattern, not an exact/hardcoded
# name, so it keeps working if the exact wording changes.
HEADLINE_PATTERN = "iterationtotal"


def build_report(records: list[Record], title: str, baseline_records: list[Record] | None = None,
                  regression_threshold_pct: float = 5.0, regression_threshold_abs_ms: float = 1.0) -> str:
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

    # --- Headline: one at-a-glance number per (project, framework, cache-bucket) -----------
    # The single most useful number to eyeball first, before any detailed table. Uses the
    # "whole iteration" synthetic measurement (see HEADLINE_PATTERN) when present.
    headline_groups: dict[tuple[str, str, bool], list[Record]] = defaultdict(list)
    for r in records:
        if HEADLINE_PATTERN in r.testId.lower():
            headline_groups[(r.project, r.framework, r.cache_enabled)].append(r)

    lines.append("## Headline")
    lines.append("")
    if not headline_groups:
        lines.append(f"_No test's `testId` matches the headline pattern (`{HEADLINE_PATTERN}`), so no "
                      "single at-a-glance number is available. See \"Summary by test\" below for the "
                      "full per-query breakdown._")
    else:
        lines.append("Median wall-clock time for one full iteration (all queries in that project's fixed "
                      "sequence), the single most useful number to check first.")
        lines.append("")
        lines.append("| Project | Framework | Cache | Median (ms) | N |")
        lines.append("|---|---|---|---|---|")
        for (project, framework, cached), group_records in sorted(headline_groups.items(), key=lambda kv: kv[0]):
            s = summarize(group_records)
            cache_label = "cached" if cached else "non-cached"
            lines.append(f"| {project} | {framework} | {cache_label} | {fmt_ms(s.median_ms)} | {s.count} |")
    lines.append("")

    # --- Comparison vs baseline, when --baseline was supplied -------------------------------
    if baseline_records is not None:
        lines.append("## Comparison vs baseline")
        lines.append("")
        lines.append(f"Delta % and verdict, per `(project, framework, testId, cache)` present in both runs. "
                      f"Positive delta = current run slower (regression); negative = faster (improvement). "
                      f"A verdict only fires when **both** thresholds are exceeded: **±{regression_threshold_pct:.0f}%** "
                      f"**and** **±{regression_threshold_abs_ms:.2f} ms** absolute — this avoids flagging noise on "
                      f"sub-millisecond queries, where a large percent swing can still be an insignificant "
                      f"absolute difference (e.g. GC pauses, JIT warmup, OS scheduling jitter).")
        lines.append("")

        current_groups: dict[tuple[str, str, str, bool], list[Record]] = defaultdict(list)
        for r in records:
            current_groups[(r.project, r.framework, r.testId, r.cache_enabled)].append(r)
        baseline_groups: dict[tuple[str, str, str, bool], list[Record]] = defaultdict(list)
        for r in baseline_records:
            baseline_groups[(r.project, r.framework, r.testId, r.cache_enabled)].append(r)

        comparison_rows = []
        for key in sorted(set(current_groups) & set(baseline_groups)):
            project, framework, test_id, cached = key
            cur_stats = summarize(current_groups[key])
            base_stats = summarize(baseline_groups[key])
            if base_stats.median_ms == 0:
                continue
            delta_ms = cur_stats.median_ms - base_stats.median_ms
            delta_pct = delta_ms / base_stats.median_ms * 100
            comparison_rows.append((project, framework, test_id, cached, base_stats.median_ms, cur_stats.median_ms,
                                     delta_pct, verdict(delta_pct, delta_ms, regression_threshold_pct, regression_threshold_abs_ms)))

        only_in_current = sorted(set(current_groups) - set(baseline_groups))
        only_in_baseline = sorted(set(baseline_groups) - set(current_groups))

        if not comparison_rows:
            lines.append("_No `(project, framework, testId, cache)` combination is present in both the current "
                          "and baseline input, so no comparison can be made._")
        else:
            regressions = [r for r in comparison_rows if r[7] == "REGRESSION"]
            improvements = [r for r in comparison_rows if r[7] == "IMPROVEMENT"]
            if regressions:
                lines.append(f"**⚠️ Overall verdict: {len(regressions)} regression(s) detected** "
                              f"(out of {len(comparison_rows)} compared tests, {len(improvements)} improved).")
            else:
                lines.append(f"**✅ Overall verdict: no regressions detected** "
                              f"(out of {len(comparison_rows)} compared tests, {len(improvements)} improved).")
            lines.append("")
            # Regressions first — the actionable rows — then improvements, then unchanged.
            order = {"REGRESSION": 0, "IMPROVEMENT": 1, "no significant change": 2}
            comparison_rows.sort(key=lambda r: (order[r[7]], r[0], r[1], r[2], r[3]))
            lines.append("| Project | Framework | Test | Cache | Baseline median (ms) | Current median (ms) | Delta % | Verdict |")
            lines.append("|---|---|---|---|---|---|---|---|")
            for project, framework, test_id, cached, base_median, cur_median, delta_pct, v in comparison_rows:
                cache_label = "cached" if cached else "non-cached"
                lines.append(
                    f"| {project} | {framework} | {test_id} | {cache_label} | {fmt_ms(base_median)} | "
                    f"{fmt_ms(cur_median)} | {delta_pct:+.1f}% | {v} |"
                )
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
        lines.append("| Project | Framework | Test | Cache | Elapsed (ms) | Details | Source |")
        lines.append("|---|---|---|---|---|---|---|")
        for r in failures:
            cache = "cached" if r.cache_enabled else "non-cached"
            details = (r.details or "").replace("|", "\\|")
            lines.append(f"| {r.project} | {r.framework} | {r.testId} | {cache} | {fmt_ms(r.elapsedMs)} | {details} | {r.source_file} |")
    lines.append("")

    # --- Per (project, framework, testId, cache-bucket) summary -----------------------------
    groups: dict[tuple[str, str, str, bool], list[Record]] = defaultdict(list)
    for r in records:
        groups[(r.project, r.framework, r.testId, r.cache_enabled)].append(r)

    lines.append("## Summary by test")
    lines.append("")
    lines.append("`Framework` is the .NET runtime the test ran under (e.g. `net9.0`), which corresponds directly "
                  "to the EF Core major version under test. Cache bucket is derived from `forwardCacheTimeout`: "
                  "`cached` means TTL > 0 (see `KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers "
                  "zero/negative TTL (the default in test configs, e.g. -1 seconds).")
    lines.append("")
    lines.append("| Project | Framework | Test | Cache | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|---|")
    for (project, framework, test_id, cached), group_records in sorted(groups.items(), key=lambda kv: kv[0]):
        s = summarize(group_records)
        stdev = fmt_ms(s.stdev_ms) if s.stdev_ms is not None else "n/a"
        cache_label = "cached" if cached else "non-cached"
        lines.append(
            f"| {project} | {framework} | {test_id} | {cache_label} | {s.count} | {fmt_ms(s.mean_ms)} | {fmt_ms(s.median_ms)} | "
            f"{fmt_ms(s.min_ms)} | {fmt_ms(s.max_ms)} | {stdev} | {s.success_rate:.0%} |"
        )
    lines.append("")

    # --- Cached vs non-cached delta, for (project, framework, testId) present in both buckets
    lines.append("## Cached vs non-cached delta")
    lines.append("")
    lines.append("Positive `delta %` means the cached run was slower than the non-cached run for that same test "
                  "(higher median), computed separately per framework so a difference in EF Core version behavior "
                  "isn't hidden by averaging across them. For a test whose non-cached path benefits from projection "
                  "push-down, a consistently positive delta here is expected — projection is intentionally skipped "
                  "when the per-entity cache is enabled (TTL > 0), so timings should tend back towards the "
                  "full-entity-fetch cost in that case.")
    lines.append("")
    test_keys = {(project, framework, test_id) for (project, framework, test_id, _cached) in groups}
    delta_rows = []
    for project, framework, test_id in sorted(test_keys):
        cached_group = groups.get((project, framework, test_id, True))
        noncached_group = groups.get((project, framework, test_id, False))
        if not cached_group or not noncached_group:
            continue
        cached_stats = summarize(cached_group)
        noncached_stats = summarize(noncached_group)
        if noncached_stats.median_ms == 0:
            continue
        delta_pct = (cached_stats.median_ms - noncached_stats.median_ms) / noncached_stats.median_ms * 100
        delta_rows.append((project, framework, test_id, noncached_stats.median_ms, cached_stats.median_ms, delta_pct))

    if not delta_rows:
        lines.append("_No test has records in both cache buckets, so no delta can be computed. Run the same "
                      "scenario once with `/p:ForwardCacheTimeout=-1` and once with a positive value (e.g. "
                      "`/p:ForwardCacheTimeout=60`) and pass both result files to this script to populate this "
                      "section._")
    else:
        lines.append("| Project | Framework | Test | Non-cached median (ms) | Cached median (ms) | Delta % |")
        lines.append("|---|---|---|---|---|---|")
        for project, framework, test_id, noncached_median, cached_median, delta_pct in delta_rows:
            lines.append(
                f"| {project} | {framework} | {test_id} | {fmt_ms(noncached_median)} | {fmt_ms(cached_median)} | {delta_pct:+.1f}% |"
            )
    lines.append("")

    # --- Cross-framework comparison, for (project, testId, cache-bucket) present in 2+ frameworks
    lines.append("## Cross-framework comparison")
    lines.append("")
    lines.append("For the same project/test/cache-bucket, how the median elapsed time compares across the "
                  ".NET/EF Core versions the CI matrix runs against. Only shown for combinations with data from "
                  "more than one framework.")
    lines.append("")
    by_test_cache: dict[tuple[str, str, bool], dict[str, GroupStats]] = defaultdict(dict)
    for (project, framework, test_id, cached), group_records in groups.items():
        by_test_cache[(project, test_id, cached)][framework] = summarize(group_records)

    frameworks_present = sorted({fw for (_p, fw, _t, _c) in groups})
    multi_framework_rows = {k: v for k, v in by_test_cache.items() if len(v) > 1}
    if not multi_framework_rows or len(frameworks_present) < 2:
        lines.append("_No test has records from more than one framework, so no cross-framework comparison can be "
                      "made. Pass result files from multiple `net*.0` runs together to populate this section._")
    else:
        header = "| Project | Test | Cache | " + " | ".join(f"{fw} median (ms)" for fw in frameworks_present) + " |"
        sep = "|---|---|---|" + "---|" * len(frameworks_present)
        lines.append(header)
        lines.append(sep)
        for (project, test_id, cached), per_fw in sorted(multi_framework_rows.items(), key=lambda kv: kv[0]):
            cache_label = "cached" if cached else "non-cached"
            cells = [fmt_ms(per_fw[fw].median_ms) if fw in per_fw else "n/a" for fw in frameworks_present]
            lines.append(f"| {project} | {test_id} | {cache_label} | " + " | ".join(cells) + " |")
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

    report = build_report(records, args.title, baseline_records=baseline_records,
                           regression_threshold_pct=args.regression_threshold,
                           regression_threshold_abs_ms=args.regression_threshold_abs_ms)

    out_path = Path(args.output)
    out_path.write_text(report, encoding="utf-8")
    baseline_note = f", compared against {len(baseline_records)} baseline record(s)" if baseline_records is not None else ""
    print(f"Wrote {out_path} ({len(records)} records from {len(paths)} file(s){baseline_note}).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
