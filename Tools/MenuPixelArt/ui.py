from common import *
from sprites import Cv, BRONZE

import os as _os
_FD_LOCAL = _os.path.join(_os.path.dirname(_os.path.abspath(__file__)), 'dejavu-fonts', 'dejavu-fonts-ttf-2.37', 'ttf') + _os.sep
FD = _FD_LOCAL if _os.path.isdir(_FD_LOCAL) else '/usr/share/fonts/truetype/dejavu/'
FONT_TITLE = FD + 'DejaVuSerif-Bold.ttf'
FONT_BTN = FD + 'DejaVuSansCondensed-Bold.ttf'
FONT_SMALL = FD + 'DejaVuSansCondensed.ttf'
FONT_ITAL = FD + 'DejaVuSerifCondensed-Italic.ttf'

def text_mask(text, font_path, size, tracking=1, space=None):
    """chữ pixel (không khử răng cưa), trả về mảng bool"""
    f = ImageFont.truetype(font_path, size)
    f.set_variation_by_name if False else None
    widths = [f.getlength(ch) for ch in text]
    total = int(sum(widths) + tracking * (len(text) - 1) + 8)
    asc, desc = f.getmetrics()
    im = Image.new('L', (total + 8, asc + desc + 8), 0)
    d = ImageDraw.Draw(im); d.fontmode = '1'
    x = 4.0
    for ch, wd in zip(text, widths):
        d.text((round(x), 4), ch, font=f, fill=255)
        x += wd + tracking
    m = np.array(im) > 127
    ys, xs = np.where(m)
    if len(ys) == 0: return m
    return m[ys.min():ys.max() + 1, xs.min():xs.max() + 1]

def pad(m, n):
    return np.pad(m, n)

def grad_cols(h, stops):
    """stops: danh sách (màu,...) từ trên xuống, chia đều thành các dải"""
    out = np.zeros((h, 3), np.uint8)
    n = len(stops)
    for y in range(h):
        out[y] = stops[min(n - 1, int(y / h * n))]
    return out

# ------------------------------------------------------------------ tiêu đề kim loại đồng
def title_sprite(text, size=22, tracking=2):
    m = pad(text_mask(text, FONT_TITLE, size, tracking=tracking), 6)
    h, w = m.shape
    arr = np.zeros((h, w, 4), np.uint8)
    # bóng đổ
    sh = shift(dilate(m, 2), 2, 3)
    arr[sh] = (4, 6, 16, 170)
    # viền ngoài tối
    o2 = dilate(m, 2)
    arr[o2] = (24, 12, 6, 255)
    # viền mạ vàng sáng mỏng
    o1 = dilate(m, 1) & ~m
    arr[o1] = (206, 150, 62, 255)
    # thân chữ: gradient đồng, chia dải
    stops = [(255, 232, 150), (246, 200, 100), (226, 164, 66), (196, 126, 46), (156, 92, 34), (116, 66, 28)]
    ys = np.where(m.any(1))[0]; y0, y1 = ys.min(), ys.max()
    gc = grad_cols(y1 - y0 + 1, stops)
    for y in range(y0, y1 + 1):
        row = m[y]
        arr[y, row] = (*gc[y - y0], 255)
    # gõ nổi: sáng trên-trái, tối dưới-phải
    hi = m & ~shift(m, 0, 1) | (m & ~shift(m, 1, 0))
    lo = m & (~shift(m, 0, -1) | ~shift(m, -1, 0))
    arr[hi & ~lo, :3] = (255, 244, 200)
    arr[lo, :3] = (96, 54, 22)
    # vân kim loại: vài điểm lấp lánh
    rng = random.Random(3)
    cand = np.argwhere(m & ~lo & ~hi)
    for i in rng.sample(range(len(cand)), min(18, len(cand))):
        y, x = cand[i]; arr[y, x, :3] = (255, 248, 214)
    return Image.fromarray(arr, 'RGBA')

# ------------------------------------------------------------------ chữ nhỏ
def small_sprite(text, font_path, size, col, outline=(8, 12, 24), tracking=1, shadow_off=(1, 1)):
    m = pad(text_mask(text, font_path, size, tracking), 3)
    h, w = m.shape
    arr = np.zeros((h, w, 4), np.uint8)
    arr[shift(dilate(m, 1), *shadow_off)] = (*outline, 255)
    arr[dilate(m, 1)] = (*outline, 255)
    arr[m] = (*col, 255)
    return Image.fromarray(arr, 'RGBA')

def label_sprite(text, size, col, shade, high=None, tracking=2):
    """nhãn nút: chữ khắc chìm (bóng sáng bên dưới) hoặc nổi"""
    m = pad(text_mask(text, FONT_BTN, size, tracking), 3)
    h, w = m.shape
    arr = np.zeros((h, w, 4), np.uint8)
    if high is not None:
        arr[shift(m, 0, 1)] = (*high, 255)
    arr[shift(m, 1, 1) & ~m] = (*shade, 255)
    arr[m] = (*col, 255)
    return Image.fromarray(arr, 'RGBA')

# ------------------------------------------------------------------ rune
RUNES = {
    'F': [(1, 0, 1, 6), (1, 1, 4, 0), (1, 3, 4, 2)],
    'U': [(0, 0, 0, 6), (0, 0, 3, 2), (3, 2, 3, 6)],
    'T': [(1, 0, 1, 6), (1, 2, 4, 3), (4, 3, 1, 4)],
    'A': [(1, 0, 1, 6), (1, 0, 4, 2), (1, 2, 4, 4)],
    'R': [(1, 0, 1, 6), (1, 0, 4, 2), (4, 2, 1, 4), (1, 4, 4, 6)],
    'K': [(4, 0, 1, 3), (1, 3, 4, 6)],
    'G': [(0, 0, 4, 6), (4, 0, 0, 6)],
    'W': [(1, 0, 1, 6), (1, 0, 4, 2), (4, 2, 1, 4)],
    'H': [(0, 0, 0, 6), (4, 0, 4, 6), (0, 2, 4, 4)],
    'N': [(2, 0, 2, 6), (0, 2, 4, 4)],
    'I': [(2, 0, 2, 6)],
    'S': [(3, 0, 1, 2), (1, 2, 3, 4), (3, 4, 1, 6)],
    'B': [(1, 0, 1, 6), (1, 0, 4, 1), (4, 1, 1, 3), (1, 3, 4, 5), (4, 5, 1, 6)],
    'D': [(0, 0, 4, 3), (4, 3, 0, 6), (0, 0, 0, 6), (4, 0, 4, 6)],
    'O': [(2, 0, 0, 2), (2, 0, 4, 2), (0, 2, 4, 6), (4, 2, 0, 6)],
    'L': [(1, 0, 1, 6), (1, 2, 4, 0)],
}

def rune_img(ch, col, scale=1):
    cv = Cv(6, 8)
    for x0, y0, x1, y1 in RUNES[ch]:
        cv.line(x0 + 1, y0 + 1, x1 + 1, y1 + 1, col)
    return cv.a

def stamp(arr, sub, x, y):
    h, w = sub.shape[:2]
    m = sub[..., 3] > 0
    H_, W_ = arr.shape[:2]
    for j in range(h):
        for i in range(w):
            if m[j, i] and 0 <= y + j < H_ and 0 <= x + i < W_:
                arr[y + j, x + i] = sub[j, i]

# ------------------------------------------------------------------ nút
BW = 120

def button(h, kind, state, BW=120):
    """kind: 'primary' | 'secondary'; state: 'normal' | 'hover' | 'down'. Trả về ảnh RGBA BW x h."""
    arr = np.zeros((h, BW, 4), np.uint8)
    ys, xs = np.mgrid[0:h, 0:BW]
    # hình chữ nhật cắt góc
    def box(inset):
        m = (xs >= inset) & (xs < BW - inset) & (ys >= inset) & (ys < h - inset)
        c = 3 - inset if inset < 3 else 0
        for (cx, cy) in [(inset, inset), (BW - 1 - inset, inset), (inset, h - 1 - inset), (BW - 1 - inset, h - 1 - inset)]:
            dx, dy = abs(xs - cx), abs(ys - cy)
            near = (np.abs(xs - cx) < 3) & (np.abs(ys - cy) < 3) & ((xs < cx + 3 if cx == inset else xs > cx - 3)) & ((ys < cy + 3 if cy == inset else ys > cy - 3))
            m &= ~(near & (dx + dy < 3 - 0) & False)
        # cắt 2 điểm ảnh ở góc
        for (cx, cy, sx, sy) in [(inset, inset, 1, 1), (BW - 1 - inset, inset, -1, 1), (inset, h - 1 - inset, 1, -1), (BW - 1 - inset, h - 1 - inset, -1, -1)]:
            for (ox, oy) in [(0, 0), (1, 0), (0, 1)]:
                m[cy + sy * oy, cx + sx * ox] = False
        return m
    m0, m1, m2 = box(0), box(1), box(3)
    if kind == 'primary':
        rim_hi, rim_lt, rim_dk = (255, 226, 140), (214, 158, 66), (104, 62, 24)
        fills = {'normal': [(236, 190, 98), (222, 166, 72), (204, 140, 56), (176, 110, 44)],
                 'hover': [(255, 224, 138), (246, 198, 96), (228, 164, 70), (198, 130, 52)],
                 'down': [(190, 140, 66), (176, 120, 52), (158, 100, 42), (136, 82, 36)]}[state]
        rune_c = (92, 52, 20)
    else:
        rim_hi, rim_lt, rim_dk = (196, 150, 82), (150, 108, 54), (70, 46, 24)
        fills = {'normal': [(58, 70, 96), (46, 56, 80), (36, 44, 66), (28, 34, 52)],
                 'hover': [(84, 98, 130), (68, 80, 110), (54, 64, 92), (42, 50, 74)],
                 'down': [(36, 44, 66), (30, 38, 58), (24, 30, 48), (20, 24, 40)]}[state]
        rune_c = (186, 142, 74) if state != 'hover' else (255, 214, 120)
    arr[m0] = (10, 8, 14, 255)
    arr[m1] = (*rim_lt, 255)
    top = m1 & ~shift(m1, 0, 1); left = m1 & ~shift(m1, 1, 0)
    bot = m1 & ~shift(m1, 0, -1); right = m1 & ~shift(m1, -1, 0)
    arr[top | left, :3] = rim_hi
    arr[bot | right, :3] = rim_dk
    # mặt nút: 4 dải + dither giữa các dải
    bay = bayer_grid(BW, h)
    t = (ys - 3) / max(1, h - 7) * 3.0
    band = np.clip(np.floor(t).astype(int) + ((t - np.floor(t)) > bay), 0, 3)
    for c in range(3):
        arr[..., c] = np.where(m2, np.array([f[c] for f in fills], np.uint8)[band], arr[..., c])
    arr[m2, 3] = 255
    # đường vân sáng trên cùng của mặt
    topline = m2 & ~shift(m2, 0, 1)
    arr[topline, :3] = np.minimum(np.array(fills[0]) + 26, 255)
    # đinh tán ở góc
    for (x, y) in [(5, 5), (BW - 6, 5), (5, h - 6), (BW - 6, h - 6)]:
        arr[y, x, :3] = rim_hi; arr[y + 1, x + 1, :3] = rim_dk if False else rim_dk
    # rune hai đầu
    order = ['F', 'U', 'T', 'A', 'R', 'K', 'G', 'W'] if kind == 'primary' else ['H', 'N', 'I', 'S', 'B', 'D', 'O', 'L']
    cy = (h - 8) // 2
    ro = 0 if state != 'down' else 0
    for k, ch in enumerate(order[:2]):
        stamp(arr, rune_img(ch, rune_c), 9 + k * 8, cy + ro)
    for k, ch in enumerate(order[2:4]):
        stamp(arr, rune_img(ch, rune_c), BW - 9 - 6 - (1 - k) * 8 + 8 - 8 + (0), cy + ro)
    # chấm trang trí gần rune
    for x in (7, BW - 8):
        arr[cy + 3:cy + 5, x:x + 2, :3] = rune_c
        arr[cy + 3:cy + 5, x:x + 2, 3] = 255
    return Image.fromarray(arr, 'RGBA')

# ------------------------------------------------------------------ khung menu
PW, PH = 150, 125

def panel(PW=150, PH=125):
    arr = np.zeros((PH, PW, 4), np.uint8)
    ys, xs = np.mgrid[0:PH, 0:PW]
    bay = bayer_grid(PW, PH)
    def rect(inset): return (xs >= inset) & (xs < PW - inset) & (ys >= inset) & (ys < PH - inset)
    outer, mid, inner = rect(0), rect(2), rect(8)
    # cắt góc kiểu chữ L
    for cx, cy, sx, sy in [(0, 0, 1, 1), (PW - 1, 0, -1, 1), (0, PH - 1, 1, -1), (PW - 1, PH - 1, -1, -1)]:
        for k in range(3):
            for j in range(3 - k):
                outer[cy + sy * j, cx + sx * k] = False
    arr[outer] = (8, 8, 14, 255)
    m1 = outer & rect(1)
    # khung đồng thau/sắt
    arr[m1] = (92, 66, 36, 255)
    top = m1 & ~shift(m1, 0, 1); left = m1 & ~shift(m1, 1, 0)
    arr[top | left, :3] = (204, 154, 76)
    fr = m1 & ~inner
    # nền khung: sắt tối có vân dither
    lvl = np.where(fr, 0.5, 0)
    dm = (lvl > bay) & fr
    arr[fr, :3] = (60, 44, 30)
    arr[dm, :3] = (74, 54, 34)
    arr[top | left, :3] = (214, 164, 84)
    bot = m1 & ~shift(m1, 0, -1); right = m1 & ~shift(m1, -1, 0)
    arr[bot | right, :3] = (40, 28, 18)
    # đường chỉ sáng ở rìa trong
    in_edge = inner & ~rect(9)
    arr[in_edge, :3] = (168, 122, 58); arr[in_edge, 3] = 255
    in2 = rect(9) & ~rect(10)
    arr[in2, :3] = (22, 18, 20); arr[in2, 3] = 255
    # nền bên trong: tối xanh, hơi trong
    body = rect(10)
    n = fbm(PW, PH, 6, 5, 2)
    base = np.array([12, 16, 30, 236], np.uint8)
    arr[body] = base
    arr[body & (n > 0.52) & (bay > 0.5), :3] = (16, 22, 40)
    # hoạ tiết rune ở viền trên/dưới
    rc = (150, 108, 52)
    seq = list('FUTARKGWHNISBDOL')
    cxm = PW // 2
    for side in (-1, 1):
        for k in range(6):
            ch = seq[(k * 3 + (0 if side < 0 else 1)) % len(seq)]
            x = cxm + side * (14 + k * 10) - 3
            stamp(arr, rune_img(ch, rc), x, 2)
            stamp(arr, rune_img(seq[(k * 5 + 3) % len(seq)], rc), x, PH - 10)
    # mảng ngọc giữa viền trên/dưới
    for yy in (5, PH - 6):
        for dx in range(-4, 5):
            for dy in range(-4, 5):
                if abs(dx) + abs(dy) <= 4:
                    c = (232, 184, 92) if abs(dx) + abs(dy) <= 2 else (120, 78, 32)
                    arr[yy + dy, cxm + dx, :3] = c; arr[yy + dy, cxm + dx, 3] = 255
        arr[yy, cxm, :3] = (110, 172, 232)
    # chuỗi hoa văn hai bên dọc
    for side in (0, PW - 6):
        for y in range(18, PH - 18, 6):
            arr[y:y + 2, side + 2:side + 4, :3] = (180, 132, 62); arr[y:y + 2, side + 2:side + 4, 3] = 255
    # đinh tán góc
    for (x, y) in [(5, 14), (PW - 6, 14), (5, PH - 15), (PW - 6, PH - 15)]:
        for dx, dy, c in [(0, 0, (236, 190, 100)), (1, 0, (170, 120, 52)), (0, 1, (170, 120, 52)), (1, 1, (90, 56, 24))]:
            arr[y + dy, x + dx, :3] = c; arr[y + dy, x + dx, 3] = 255
    return Image.fromarray(arr, 'RGBA')

if __name__ == '__main__':
    t = title_sprite('MÀN SƯƠNG LÃNG QUÊN'); t.save('/home/claude/out/title.png'); print('title', t.size)
    k = small_sprite('KHOA HỌC TỰ NHIÊN  •  LỚP 8', FONT_SMALL, 10, (178, 204, 236)); k.save('/home/claude/out/kicker.png'); print('kicker', k.size)
    g = small_sprite('Mỗi điều bạn biết là một ngọn đèn giữa màn sương.', FONT_ITAL, 11, (214, 228, 248)); g.save('/home/claude/out/tagline.png'); print('tag', g.size)
    sheet = Image.new('RGBA', (440, 220), (24, 40, 64, 255))
    sheet.alpha_composite(t, (10, 8)); sheet.alpha_composite(k, (10, 50)); sheet.alpha_composite(g, (10, 66))
    for i, (h, kd) in enumerate([(26, 'primary'), (21, 'secondary')]):
        for j, st in enumerate(['normal', 'hover', 'down']):
            b = button(h, kd, st); b.save(f'/home/claude/out/btn_{kd}_{st}.png')
            sheet.alpha_composite(b, (10 + j * 0, 90 + i * 0 + j * 0)) if False else None
    y = 92
    for kd, h, labels in [('primary', 26, 'BẮT ĐẦU'), ('secondary', 21, 'TIẾN ĐỘ')]:
        x = 10
        for st in ['normal', 'hover', 'down']:
            b = button(h, kd, st); sheet.alpha_composite(b, (x, y))
            if st == 'normal':
                lab = label_sprite(labels, 12, (50, 28, 10) if kd == 'primary' else (232, 220, 190), (255, 220, 140) if kd == 'primary' else (6, 8, 16), (255, 236, 170) if kd == 'primary' else None)
                sheet.alpha_composite(lab, (x + (BW - lab.width) // 2, y + (h - lab.height) // 2))
            x += 130
        y += h + 6
    upscale(sheet, 3).save('/home/claude/out/ui_preview.png')
    p = panel(); p.save('/home/claude/out/panel.png'); upscale(p, 4).save('/home/claude/out/panel_preview.png')
