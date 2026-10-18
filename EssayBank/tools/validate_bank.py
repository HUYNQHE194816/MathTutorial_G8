#!/usr/bin/env python3
"""Kiểm tra Google Sheet (xuất CSV) theo lược đồ mục 11. Dùng: validate_bank.py bank.csv"""
import json, re, sys
from collections import Counter
import bank

def main(path):
    fields, rows = bank.load_bank(path)
    errors, warns = [], []
    missing = [c for c in bank.COLUMNS if c not in (fields or [])]
    if missing:
        print("LỖI: thiếu cột:", ", ".join(missing)); return 1
    seen, cells = set(), Counter()
    for i, row in enumerate(rows, start=2):  # dòng 1 là tiêu đề
        rid = row["id"].strip() or f"(dòng {i})"
        def err(m): errors.append(f"{rid} [dòng {i}]: {m}")
        def warn(m): warns.append(f"{rid} [dòng {i}]: {m}")
        if rid in seen: err("id trùng")
        seen.add(rid)
        if row["subject"] not in bank.SUBJECTS: err(f"subject '{row['subject']}' phải là hoa|sinh|ly")
        if row["difficulty"] not in bank.DIFFICULTIES: err(f"difficulty '{row['difficulty']}' phải là easy|normal|hard|expert")
        try: p = bank.parse_row(row)
        except json.JSONDecodeError as e:
            err(f"JSON hỏng: {e}"); continue
        cells[(row["subject"], row["difficulty"])] += 1
        params = p["params"]
        if not isinstance(params, dict) or not all(isinstance(v, list) and v and all(isinstance(x,(int,float)) for x in v) for v in params.values()):
            err("params phải là {tên: [số, ...]} không rỗng"); continue
        used = set(re.findall(r"\{(\w+)\}", row["statement"]))
        if used - set(params): err(f"statement dùng tham số chưa khai báo: {sorted(used - set(params))}")
        if set(params) - used: warn(f"tham số không dùng trong statement: {sorted(set(params) - used)}")
        rub = p["rubric"]
        ids = [s.get("id") for s in rub]
        if len(set(ids)) != len(ids): err("id bước barem trùng")
        if any(not isinstance(s.get("points"), (int, float)) or s["points"] <= 0 or not s.get("desc") for s in rub):
            err("mỗi bước barem cần points > 0 và desc")
        total = sum(s.get("points", 0) for s in rub)
        lo, hi = bank.STEP_RANGE.get(row["difficulty"], (0, 99))
        if not lo <= len(rub) <= hi: warn(f"{len(rub)} bước barem, mức {row['difficulty']} nên {lo}-{hi} bước (mục 15.1)")
        fa = p["finalAnswer"]
        try:
            names = bank.formula_names(fa["formula"])
            if names - set(params): err(f"finalAnswer dùng tham số chưa khai báo: {sorted(names - set(params))}")
            else:
                rng_vals = [{k: v[j % len(v)] for k, v in params.items()} for j in range(max(len(v) for v in params.values()))]
                for vals in rng_vals: bank.eval_formula(fa["formula"], vals)
        except (ValueError, SyntaxError, ZeroDivisionError, KeyError) as e:
            err(f"finalAnswer.formula lỗi: {e}")
        if not fa.get("unit"): err("finalAnswer thiếu unit")
        if not isinstance(fa.get("tolerance"), (int, float)) or fa["tolerance"] < 0: err("finalAnswer.tolerance phải là số >= 0")
        for k, tags in p["misconceptions"].items():
            if k not in ids: err(f"misconceptions trỏ tới bước không có: {k}")
        if len(p["hints"]) != 3 or not all(p["hints"]): err("hints phải đủ 3 bậc (bậc 4 là lời giải đầy đủ do AI)")
        if not row["sgk"].strip(): warn("thiếu cột sgk (dùng đối chiếu hằng số)")
    print(f"Đã đọc {len(rows)} bài.")
    print("\nĐộ phủ (mục tiêu %d đề mỗi môn mỗi mức):" % bank.TARGET_PER_CELL)
    print("  môn   " + "  ".join(f"{d:>7}" for d in bank.DIFFICULTIES))
    for s in bank.SUBJECTS:
        print(f"  {s:<5} " + "  ".join(f"{cells[(s,d)]:>3}/{bank.TARGET_PER_CELL:<3}" for d in bank.DIFFICULTIES))
    for w in warns: print("CẢNH BÁO:", w)
    for e in errors: print("LỖI:", e)
    print(f"\n{len(errors)} lỗi, {len(warns)} cảnh báo.")
    return 1 if errors else 0

if __name__ == "__main__":
    sys.exit(main(sys.argv[1]) if len(sys.argv) == 2 else print(__doc__) or 2)
