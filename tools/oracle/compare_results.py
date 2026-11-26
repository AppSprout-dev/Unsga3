#!/usr/bin/env python3
"""Print a comparison table from oracle out/*_meta.json files."""
from __future__ import annotations

import json
from pathlib import Path

OUT = Path(__file__).resolve().parent / "out"


def main() -> None:
    rows = []
    for p in sorted(OUT.glob("*_meta.json")):
        meta = json.loads(p.read_text(encoding="utf-8"))
        rows.append(meta)

    if not rows:
        print("No meta files in", OUT)
        return

    print(f"{'source':12} {'problem':8} {'mode':16} {'pop':>4} {'gens':>5} {'seed':>4} {'n':>4} {'IGD':>12}")
    print("-" * 80)
    for m in rows:
        mode = m.get("tournament") or m.get("algorithm") or ""
        raw_igd = m.get("igd")
        igd_cell = f"{float(raw_igd):12.6g}" if raw_igd is not None else f"{'n/a':>12}"
        print(
            f"{m.get('source', '?'):12} "
            f"{m.get('problem', '?'):8} "
            f"{str(mode)[:16]:16} "
            f"{m.get('pop_size', 0):4} "
            f"{m.get('n_gen', 0):5} "
            f"{m.get('seed', 0):4} "
            f"{m.get('n_solutions', 0):4} "
            f"{igd_cell}"
        )


if __name__ == "__main__":
    main()
