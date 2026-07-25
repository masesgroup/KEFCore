#!/usr/bin/env python3
"""
Aggregates and analyzes the JSON Lines files produced by ProgramConfig.ReportResult
(see test/Common/ProgramConfig.cs) across one or more KEFCore.*.Test runs, and writes
a Markdown report.

Each input line is expected to be a JSON object with the shape:
  {
    "timestamp": "2026-07-25T10:00:00.0000000Z",
    "project": "KEFCore.Benchmark.Test",
    "testId": "Test 4",
    "elapsedMs": 12.34,
    "success": true,
    "details": "Max ... Min ... Mean ... Median ...",
    "forwardCacheTimeout": -1,
    "reverseCacheTimeout": -1
  }

Usage:
    python analyze_results.py --input run1.jsonl run2.jsonl --output report.md
    python analyze_results.py --input results/*.jsonl --output report.md --title "KEFCore benchmark run"

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


def build_report(records: list[Record], title: str) -> str:
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

    # --- Failures section, always first and prominent -------------------------------------
    failures = [r for r in records if not r.success]
    lines.append("## Failures")
    lines.append("")
    if not failures:
        lines.append("None. All reported outcomes had `success: true`.")
    else:
        lines.append(f"**{len(failures)}** failing record(s):")
        lines.append("")
        lines.append("| Project | Test | Cache | Elapsed (ms) | Details | Source |")
        lines.append("|---|---|---|---|---|---|")
        for r in failures:
            cache = "cached" if r.cache_enabled else "non-cached"
            details = (r.details or "").replace("|", "\\|")
            lines.append(f"| {r.project} | {r.testId} | {cache} | {fmt_ms(r.elapsedMs)} | {details} | {r.source_file} |")
    lines.append("")

    # --- Per (project, testId, cache-bucket) summary ---------------------------------------
    groups: dict[tuple[str, str, bool], list[Record]] = defaultdict(list)
    for r in records:
        groups[(r.project, r.testId, r.cache_enabled)].append(r)

    lines.append("## Summary by test")
    lines.append("")
    lines.append("Cache bucket is derived from `forwardCacheTimeout`: `cached` means TTL > 0 (see "
                  "`KEFCoreCachedValueBufferStore.IsEnabled`), `non-cached` covers zero/negative TTL "
                  "(the default in test configs, e.g. -1 seconds).")
    lines.append("")
    lines.append("| Project | Test | Cache | N | Mean (ms) | Median (ms) | Min (ms) | Max (ms) | Stdev (ms) | Success rate |")
    lines.append("|---|---|---|---|---|---|---|---|---|---|")
    for (project, test_id, cached), group_records in sorted(groups.items(), key=lambda kv: (kv[0][0], kv[0][1], kv[0][2])):
        s = summarize(group_records)
        stdev = fmt_ms(s.stdev_ms) if s.stdev_ms is not None else "n/a"
        cache_label = "cached" if cached else "non-cached"
        lines.append(
            f"| {project} | {test_id} | {cache_label} | {s.count} | {fmt_ms(s.mean_ms)} | {fmt_ms(s.median_ms)} | "
            f"{fmt_ms(s.min_ms)} | {fmt_ms(s.max_ms)} | {stdev} | {s.success_rate:.0%} |"
        )
    lines.append("")

    # --- Cached vs non-cached delta, for (project, testId) pairs present in both buckets ---
    lines.append("## Cached vs non-cached delta")
    lines.append("")
    lines.append("Positive `delta %` means the cached run was slower than the non-cached run for that same test "
                  "(higher median). For a test whose non-cached path benefits from projection push-down, a "
                  "consistently positive delta here is expected — projection is intentionally skipped when the "
                  "per-entity cache is enabled (TTL > 0), so timings should tend back towards the full-entity-fetch "
                  "cost in that case.")
    lines.append("")
    test_keys = {(project, test_id) for (project, test_id, _cached) in groups}
    delta_rows = []
    for project, test_id in sorted(test_keys):
        cached_group = groups.get((project, test_id, True))
        noncached_group = groups.get((project, test_id, False))
        if not cached_group or not noncached_group:
            continue
        cached_stats = summarize(cached_group)
        noncached_stats = summarize(noncached_group)
        if noncached_stats.median_ms == 0:
            continue
        delta_pct = (cached_stats.median_ms - noncached_stats.median_ms) / noncached_stats.median_ms * 100
        delta_rows.append((project, test_id, noncached_stats.median_ms, cached_stats.median_ms, delta_pct))

    if not delta_rows:
        lines.append("_No test has records in both cache buckets, so no delta can be computed. Run the same "
                      "scenario once with `/p:ForwardCacheTimeout=-1` and once with a positive value (e.g. "
                      "`/p:ForwardCacheTimeout=60`) and pass both result files to this script to populate this "
                      "section._")
    else:
        lines.append("| Project | Test | Non-cached median (ms) | Cached median (ms) | Delta % |")
        lines.append("|---|---|---|---|---|")
        for project, test_id, noncached_median, cached_median, delta_pct in delta_rows:
            lines.append(
                f"| {project} | {test_id} | {fmt_ms(noncached_median)} | {fmt_ms(cached_median)} | {delta_pct:+.1f}% |"
            )
    lines.append("")

    return "\n".join(lines)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__, formatter_class=argparse.RawDescriptionHelpFormatter)
    parser.add_argument("--input", nargs="+", required=True, help="One or more JSON Lines files (as produced by ReportResult)")
    parser.add_argument("--output", required=True, help="Path to write the Markdown report to")
    parser.add_argument("--title", default="KEFCore test results analysis", help="Report title")
    args = parser.parse_args()

    paths = [Path(p) for p in args.input]
    missing = [p for p in paths if not p.exists()]
    if missing:
        print(f"error: input file(s) not found: {', '.join(str(p) for p in missing)}", file=sys.stderr)
        return 1

    records = load_records(paths)
    report = build_report(records, args.title)

    out_path = Path(args.output)
    out_path.write_text(report, encoding="utf-8")
    print(f"Wrote {out_path} ({len(records)} records from {len(paths)} file(s)).")
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
