"""Summarise Void Prospector playtest logs against the GDD pacing targets.

Each tester sends their analytics.jsonl (one JSON event per line; see docs/IMPLEMENTATION_PLAN.md, Phase 8).
Name each file after the tester, put them in one folder and run:

    python tools/analytics/summarize.py playtests/            # or list the files
    python tools/analytics/summarize.py playtests/ > report.md

All times are play time (active seconds with the game open, across sessions), so offline time doesn't count.
Standard library only.
"""
import json
import statistics
import sys
from pathlib import Path

WALL_SECONDS = 600          # GDD: >10 min to the next upgrade in active play is a wall
TARGETS = {2: 10 * 60, 3: 30 * 60}   # GDD: Sector 2 at ~10 min, Sector 3 at ~30 min
FIRST_MINUTES_GAP = (5, 15)  # GDD: first 5 minutes, a purchase every 5-15 s


def load(path):
    events = []
    for n, line in enumerate(path.read_text(encoding="utf-8").splitlines(), 1):
        line = line.strip()
        if not line:
            continue
        try:
            events.append(json.loads(line))
        except json.JSONDecodeError:
            print(f"<!-- {path.name}:{n} is not valid JSON, skipped -->")
    events.sort(key=lambda e: (e.get("play_s", 0), e.get("utc", "")))
    return events


def fmt(seconds):
    if seconds is None:
        return "-"
    seconds = int(round(seconds))
    if seconds < 60:
        return f"{seconds}s"
    if seconds < 3600:
        return f"{seconds // 60}m {seconds % 60:02d}s"
    return f"{seconds // 3600}h {seconds % 3600 // 60:02d}m"


def summarise(name, events):
    starts = [e for e in events if e["event"] == "session_start" and e.get("reason") in ("launch", "resume")]
    ends = [e for e in events if e["event"] == "session_end" and e.get("reason") in ("pause", "quit")]
    buys = [e for e in events if e["event"] == "upgrade_bought"]
    play = max((e.get("play_s", 0) for e in events), default=0)

    sector_at = {}
    for e in events:
        if e["event"] == "sector_unlocked":
            sector_at.setdefault(e["sector_index"], e["play_s"])

    gaps = []   # (gap seconds, event after the gap)
    last = 0.0
    for b in buys:
        gaps.append((b["play_s"] - last, b))
        last = b["play_s"]
    if buys and play - last > 0:
        gaps.append((play - last, None))   # time since the last purchase, up to the end of the log

    early = [g for g, b in gaps if b is not None and b["play_s"] <= 300]
    walls = [(g, b) for g, b in gaps if g > WALL_SECONDS]
    session_lengths = [e["session_s"] for e in ends if "session_s" in e]
    last_end = ends[-1] if ends else (events[-1] if events else {})

    return {
        "name": name,
        "play": play,
        "sessions": max(len(starts), 1 if events else 0),
        "session_median": statistics.median(session_lengths) if session_lengths else None,
        "purchases": len(buys),
        "early_median": statistics.median(early) if early else None,
        "sector_at": sector_at,
        "walls": walls,
        "quit": last_end,
        "milestones": sum(1 for e in events if e["event"] == "milestone"),
        "refines": sum(1 for e in events if e["event"] == "refine_done"),
        "lost": sum(1 for e in events if e["event"] == "refine_lost"),
        "offline": sum(1 for e in events if e["event"] == "offline_paid"),
    }


def mark(value, low, high):
    if value is None:
        return ""
    return " ✓" if low <= value <= high else " ✗"


def main(args):
    if hasattr(sys.stdout, "reconfigure"):
        sys.stdout.reconfigure(encoding="utf-8")   # the report uses ✓ / ✗
    if not args:
        print(__doc__)
        return 1
    files = []
    for a in args:
        p = Path(a)
        files += sorted(p.glob("*.jsonl")) if p.is_dir() else [p]
    if not files:
        print("No .jsonl files found.")
        return 1

    rows = [summarise(f.stem, load(f)) for f in files]

    print("# Playtest summary\n")
    print(f"{len(rows)} tester(s). Times are active play time. GDD targets: Sector 2 at ~10 min, Sector 3 at ~30 min, "
          f"first-5-minute purchases every {FIRST_MINUTES_GAP[0]}-{FIRST_MINUTES_GAP[1]} s, no wall over {WALL_SECONDS // 60} min.\n")
    print("| Tester | Play time | Sessions | Median session | Purchases | First 5 min gap | Sector 2 | Sector 3 | Walls | Milestones | Refined / lost | Offline returns |")
    print("| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |")
    for r in rows:
        s2, s3 = r["sector_at"].get(2), r["sector_at"].get(3)
        early = r["early_median"]
        print(f"| {r['name']} | {fmt(r['play'])} | {r['sessions']} | {fmt(r['session_median'])} | {r['purchases']} "
              f"| {fmt(early)}{mark(early, *FIRST_MINUTES_GAP)} "
              f"| {fmt(s2)}{mark(s2, TARGETS[2] * 0.7, TARGETS[2] * 1.3)} "
              f"| {fmt(s3)}{mark(s3, TARGETS[3] * 0.7, TARGETS[3] * 1.3)} "
              f"| {len(r['walls'])} | {r['milestones']} | {r['refines']} / {r['lost']} | {r['offline']} |")

    long_players = [r for r in rows if r["play"] >= 30 * 60]
    print(f"\n**Done-when check:** {len(long_players)} of {len(rows)} tester(s) played 30+ minutes.\n")

    print("## Walls (over 10 minutes between purchases)\n")
    any_wall = False
    for r in rows:
        for gap, b in r["walls"]:
            any_wall = True
            where = f"before buying {b['id']} L{b['level']} in {b['sector']} depth {b['depth']}" if b else "until the end of the log"
            print(f"- {r['name']}: {fmt(gap)} {where}")
    if not any_wall:
        print("None.")

    print("\n## Where each tester stopped (last session end)\n")
    print("| Tester | Reason | Sector | Depth | Since last purchase | Last upgrade | Credits |")
    print("| --- | --- | --- | --- | --- | --- | --- |")
    for r in rows:
        q = r["quit"]
        print(f"| {r['name']} | {q.get('reason', '-')} | {q.get('sector', '-')} | {q.get('depth', '-')} "
              f"| {fmt(q.get('since_purchase_s')) if q.get('since_purchase_s', -1) >= 0 else '-'} "
              f"| {q.get('last_upgrade') or '-'} | {q.get('credits', '-')} |")
    return 0


if __name__ == "__main__":
    sys.exit(main(sys.argv[1:]))
