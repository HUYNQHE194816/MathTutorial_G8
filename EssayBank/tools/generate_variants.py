#!/usr/bin/env python3
"""Sinh biến thể số liệu và đáp số. Dùng:
  generate_variants.py bank.csv [--id ID] [--n 3] [--seed 1] [--json]"""
import argparse, json, random
import bank

ap = argparse.ArgumentParser()
ap.add_argument("csv"); ap.add_argument("--id"); ap.add_argument("--n", type=int, default=3)
ap.add_argument("--seed", type=int, default=1); ap.add_argument("--json", action="store_true")
a = ap.parse_args()
_, rows = bank.load_bank(a.csv)
rng = random.Random(a.seed)
out = []
for row in rows:
    if a.id and row["id"] != a.id: continue
    p = bank.parse_row(row)
    seen = set()
    for _ in range(a.n * 5):  # tránh trùng bộ số liệu
        v = bank.make_variant(p, rng)
        key = tuple(sorted(v["values"].items()))
        if key in seen: continue
        seen.add(key); out.append(v)
        if len(seen) >= a.n: break
if a.json: print(json.dumps(out, ensure_ascii=False, indent=2))
else:
    for v in out:
        print(f"[{v['id']}] {v['statement']}\n   → đáp số: {v['answer']:g} {v['unit']} (±{v['tolerance']})")
