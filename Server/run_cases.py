#!/usr/bin/env python3
"""Chạy thử E3: 20 ca (10 bài x [làm đúng, làm sai có chủ đích]) gửi vào /essay/grade và so với trạng thái kỳ vọng.
Cần server chạy ở chế độ Development (có /dev/variant) và đã đặt Gemini__ApiKey.
Dùng: python run_cases.py [--url http://localhost:5000] [--key doi-khoa-nay]"""
import argparse, json, time, urllib.request, urllib.error

ap = argparse.ArgumentParser()
ap.add_argument('--url', default='http://localhost:5000')
ap.add_argument('--key', default='doi-khoa-nay')
args = ap.parse_args()
SEED = 1

def fmt(x):
    s = ('%.6f' % x).rstrip('0').rstrip('.')
    return (s or '0').replace('.', ',')

def call(method, path, body=None):
    req = urllib.request.Request(args.url + path, method=method,
        data=json.dumps(body).encode() if body is not None else None,
        headers={'Content-Type': 'application/json', 'X-Client-Key': args.key})
    try:
        with urllib.request.urlopen(req, timeout=60) as r: return r.status, json.load(r)
    except urllib.error.HTTPError as e:
        return e.code, json.loads(e.read() or b'{}')

# (problemId, tên ca, hàm tạo các dòng từ giá trị v và chữ t, trạng thái kỳ vọng từng bước; "a|b" = chấp nhận một trong hai)
C = 'correct'
CASES = []
def add(pid, name, make, exp): CASES.append((pid, name, make, exp))

def ly_kl(v, t, wrong):
    gt = t['verdict'] == 'chìm'
    return ["D = m/V", f"D = {v('m')}/{v('V')}", f"D = {v('D')} g/cm³",
            f"{v('D')} {'>' if gt else '<'} 1 g/cm³ nên vật {t['verdict']} trong nước"]
add('ly8-kl-001', 'đúng', lambda v, t: ly_kl(v, t, False), [C, C, C, C])
add('ly8-kl-001', 'sai', lambda v, t: ["D = m/V", f"D = {v('V')}/{v('m')}", f"D = {v('D')} g/cm³", ""], [C, 'wrong', C, 'missing'])

add('ly8-ap-002', 'đúng', lambda v, t: ["p = F/S", f"S = {v('S')} cm² = {v('S_m2')} m²", f"p = {v('F')}/{v('S_m2')} = {v('p')} Pa"], [C, C, C])
add('ly8-ap-002', 'sai', lambda v, t: ["p = F/S", f"S = {v('S')} cm² = {v('S')} m²", ""], [C, 'wrong', 'missing'])

add('ly8-tocdo-003', 'đúng', lambda v, t: ["v = s/t", f"t = {v('t')} phút = {v('t_h')} giờ", f"v = {v('s')}/{v('t_h')} = {v('v')} km/h"], [C, C, C])
add('ly8-tocdo-003', 'sai', lambda v, t: ["v = s.t", f"t = {v('t')} phút = {v('t_h')} giờ", ""], ['wrong', C, 'missing'])

def ly_lds(v, t, flip):
    sink = t['verdict'] == 'chìm'
    if flip: sink = not sink
    word = 'chìm' if sink else 'nổi'
    return ["F_A = d.V", f"V = {v('V')} cm³ = {v('V_m3')} m³", f"F_A = 10000 × {v('V_m3')} = {v('FA')} N",
            f"F_A = {v('FA')} N, P = {v('P')} N, {'P > F_A' if sink else 'P < F_A'} nên vật {word}"]
add('ly8-lds-004', 'đúng', lambda v, t: ly_lds(v, t, False), [C, C, C, C])
add('ly8-lds-004', 'sai', lambda v, t: [*ly_lds(v, t, True)[:1], "", *ly_lds(v, t, True)[2:]], [C, 'missing', C, 'wrong'])

add('hoa8-pt-001', 'đúng', lambda v, t: ["Fe + 2HCl → FeCl₂ + H₂", f"n(Fe) = {v('m')}/56 = {v('n')} mol",
    f"Tỉ lệ Fe : FeCl₂ = 1 : 1 nên n(FeCl₂) = {v('n')} mol", f"m(FeCl₂) = {v('n')} × 127 = {v('mFeCl2')} g"], [C, C, C, C])
add('hoa8-pt-001', 'sai', lambda v, t: ["Fe + HCl → FeCl₂ + H₂", f"n(Fe) = {v('m')}/56 = {v('n')} mol",
    f"Tỉ lệ Fe : FeCl₂ = 1 : 1 nên n(FeCl₂) = {v('n')} mol", ""], ['wrong|partial', C, C, 'missing'])

add('hoa8-pt-002', 'đúng', lambda v, t: ["Zn + 2HCl → ZnCl₂ + H₂", f"n(Zn) = {v('m')}/65 = {v('n')} mol",
    f"Tỉ lệ Zn : H₂ = 1 : 1 nên n(H₂) = {v('n')} mol", f"V(H₂) = {v('n')} × 24,79 = {v('V')} L"], [C, C, C, C])
add('hoa8-pt-002', 'sai', lambda v, t: ["Zn + 2HCl → ZnCl₂ + H₂", "", f"Tỉ lệ Zn : H₂ = 1 : 1 nên n(H₂) = {v('n')} mol",
    f"V(H₂) = {v('n')} × 22,4 = {fmt(float(t['_n']) * 22.4)} L"], [C, 'missing', C, 'wrong|partial'])

add('hoa8-cm-003', 'đúng', lambda v, t: [f"n(NaCl) = {v('m')}/58,5 = {v('n')} mol", f"{v('V')} mL = {v('V_L')} L",
    f"C_M = {v('n')}/{v('V_L')} = {v('CM')} mol/L"], [C, C, C])
add('hoa8-cm-003', 'sai', lambda v, t: [f"n(NaCl) = {v('m')}/58,5 = {v('n')} mol", "",
    f"C_M = {v('n')}/{v('V')} = {fmt(float(t['_n']) / float(t['_V']))} mol/L"], [C, 'missing', 'wrong'])

SINH = {
 'sinh8-hh-001': (["Khi chạy nhanh, cơ bắp hoạt động mạnh nên cần nhiều năng lượng hơn.",
   "Quá trình tạo năng lượng ở tế bào cần O₂ và thải ra CO₂.",
   "Nhịp thở tăng để đưa nhiều O₂ vào cơ thể và thải CO₂ ra ngoài.",
   "Nhịp tim tăng để máu mang O₂ và chất dinh dưỡng đến cơ nhanh hơn."],
   {0: "Vì em thấy mệt.", 1: "O₂ tự sinh ra năng lượng cho cơ.", 2: ""}, ['wrong', 'wrong|partial', 'missing', C]),
 'sinh8-th-002': (["Hệ tuần hoàn có vai trò vận chuyển các chất trong cơ thể.",
   "Tim co bóp đẩy máu lưu thông trong các mạch máu.",
   "Máu mang O₂ và chất dinh dưỡng đến tế bào, đồng thời mang CO₂ và chất thải đi.",
   "Khi tim ngừng đập, tế bào thiếu O₂ và chất dinh dưỡng, chất thải bị ứ lại nên tế bào nhanh bị tổn thương."],
   {0: "Hệ tuần hoàn giúp cơ thể thở.", 3: ""}, ['wrong', C, C, 'missing']),
 'sinh8-td-003': (["Thức ăn chứa chất phức tạp, cần biến đổi thành chất đơn giản để cơ thể hấp thụ.",
   "Nhai kĩ làm thức ăn nhỏ và mềm, tăng diện tích tiếp xúc với enzim.",
   "Enzim trong nước bọt biến đổi tinh bột thành đường.",
   "Chất đơn giản được hấp thụ vào máu, giúp cơ thể hấp thụ tốt hơn."],
   {1: "Nhai kĩ cho đỡ mỏi miệng.", 2: ""}, [C, 'wrong', 'missing', C]),
}
for pid, (good, bad, exp) in SINH.items():
    add(pid, 'đúng', (lambda g: lambda v, t: list(g))(good), [C] * 4)
    add(pid, 'sai', (lambda g, b: lambda v, t: [b.get(i, x) for i, x in enumerate(g)])(good, bad), exp)

tot = agree = low = fb = errors = 0
t0 = time.time()
for pid, name, make, exp in CASES:
    s, dev = call('GET', f'/dev/variant?problemId={pid}&seed={SEED}')
    if s != 200: print('Không lấy được /dev/variant (server phải chạy Development):', s, dev); raise SystemExit(1)
    vals = dev.get('values', {}); texts = dev.get('texts', {})
    v = lambda k: fmt(vals[k])
    t = dict(texts); t['_n'] = vals.get('n', 0); t['_V'] = vals.get('V', 0)
    lines = make(v, t)
    ts = time.time()
    s, r = call('POST', '/essay/grade', {'problemId': pid, 'studentCode': 'test', 'seed': SEED, 'mode': 'steps', 'lines': lines})
    ms = int((time.time() - ts) * 1000)
    if s != 200:
        errors += 1; print(f'[LỖI {s}] {pid} {name}: {r}'); continue
    got = [st['status'] for st in r['steps']]
    ok = [g in e.split('|') for g, e in zip(got, exp)]
    tot += len(exp); agree += sum(ok); low += r['lowConfidence']; fb += r['source'] == 'fallback'
    mark = 'OK ' if all(ok) else 'LỆCH'
    print(f"{mark} {pid:14s} {name:4s} {ms:5d}ms conf={r['confidence']:.2f} nguồn={r['source']} kỳ vọng={exp} nhận={got}")
    if not all(ok):
        for st, e, o in zip(r['steps'], exp, ok):
            if not o: print(f"      {st['id']}: kỳ vọng {e}, nhận {st['status']} | {st['why']}")
print(f"\nĐộ khớp cấp bước: {agree}/{tot} = {100 * agree / max(tot, 1):.0f}%  (mục tiêu ban đầu >= 85%)")
print(f"Ca lỗi gọi: {errors} | confidence thấp: {low} | dùng fallback: {fb} | tổng {time.time() - t0:.0f}s")
