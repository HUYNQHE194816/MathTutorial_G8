"""Thư viện dùng chung cho ngân hàng đề (kế hoạch v3.2, mục 10-11)."""
import ast, csv, json, operator, random, re

COLUMNS = ["id","subject","difficulty","statement","params","rubric",
           "finalAnswer","misconceptions","hints","tag","sgk"]
SUBJECTS = ("hoa", "sinh", "ly")
DIFFICULTIES = ("easy", "normal", "hard", "expert")
STEP_RANGE = {"easy": (2, 3), "normal": (3, 4), "hard": (4, 5), "expert": (5, 6)}  # mục 15.1
TARGET_PER_CELL = 20  # mục 15.1: 3 môn x 4 mức x 20 đề

_OPS = {ast.Add: operator.add, ast.Sub: operator.sub, ast.Mult: operator.mul,
        ast.Div: operator.truediv, ast.Pow: operator.pow}

def formula_names(formula):
    return {n.id for n in ast.walk(ast.parse(formula, mode="eval")) if isinstance(n, ast.Name)}

def eval_formula(formula, values):
    """Chỉ cho phép số, tên tham số, + - * / ** và dấu ngoặc (dễ port sang C#)."""
    def ev(n):
        if isinstance(n, ast.Expression): return ev(n.body)
        if isinstance(n, ast.Constant) and isinstance(n.value, (int, float)): return n.value
        if isinstance(n, ast.Name): return values[n.id]
        if isinstance(n, ast.BinOp) and type(n.op) in _OPS: return _OPS[type(n.op)](ev(n.left), ev(n.right))
        if isinstance(n, ast.UnaryOp) and isinstance(n.op, (ast.USub, ast.UAdd)):
            return -ev(n.operand) if isinstance(n.op, ast.USub) else ev(n.operand)
        raise ValueError(f"Biểu thức không được phép: {ast.dump(n)}")
    return ev(ast.parse(formula, mode="eval"))

def fmt_vn(x):
    """Số kiểu Việt Nam: dấu phẩy thập phân, cách nghìn bằng dấu cách (11 250; 637,5)."""
    if isinstance(x, float) and x == int(x): x = int(x)
    s = f"{x:,}".replace(",", " ") if isinstance(x, int) else f"{x:g}".replace(".", ",")
    return s

def load_bank(path):
    with open(path, newline="", encoding="utf-8-sig") as f:
        rdr = csv.DictReader(f)
        return rdr.fieldnames, list(rdr)

def parse_row(row):
    p = dict(row)
    for k in ("params", "rubric", "finalAnswer", "misconceptions", "hints"):
        p[k] = json.loads(row[k])
    return p

def make_variant(problem, rng):
    values = {k: rng.choice(v) for k, v in problem["params"].items()}
    statement = re.sub(r"\{(\w+)\}", lambda m: fmt_vn(values[m.group(1)]), problem["statement"])
    fa = problem["finalAnswer"]
    answer = eval_formula(fa["formula"], values)
    return {"id": problem["id"], "values": values, "statement": statement,
            "answer": answer, "unit": fa["unit"], "tolerance": fa["tolerance"]}
