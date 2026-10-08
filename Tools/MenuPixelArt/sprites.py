from common import *

OUT = (12, 13, 22)

class Cv:
    """canvas numpy RGBA nhỏ để vẽ sprite từng pixel"""
    def __init__(self, w, h):
        self.w, self.h = w, h
        self.a = np.zeros((h, w, 4), np.uint8)
        self.ys, self.xs = np.mgrid[0:h, 0:w].astype(np.float32)
    def mask(self):
        return self.a[..., 3] > 0
    def px(self, x, y, c):
        if 0 <= x < self.w and 0 <= y < self.h: self.a[int(y), int(x)] = (*c[:3], 255)
    def fill(self, m, c):
        self.a[m] = (*c[:3], 255)
    def rect(self, x0, y0, x1, y1, c):
        self.a[int(y0):int(y1) + 1, int(x0):int(x1) + 1] = (*c[:3], 255)
    def poly(self, pts, c):
        im = Image.new('L', (self.w, self.h), 0)
        ImageDraw.Draw(im).polygon(pts, fill=255)
        self.fill(np.array(im) > 0, c)
        return np.array(im) > 0
    def ell(self, cx, cy, rx, ry, c):
        m = (((self.xs - cx) / rx) ** 2 + ((self.ys - cy) / ry) ** 2) <= 1
        self.fill(m, c); return m
    def line(self, x0, y0, x1, y1, c):
        n = int(max(abs(x1 - x0), abs(y1 - y0))) + 1
        for i in range(n + 1):
            t = i / max(1, n)
            self.px(round(x0 + (x1 - x0) * t), round(y0 + (y1 - y0) * t), c)
    def bevel(self, x0, y0, x1, y1, pal):
        """tấm giáp có vát cạnh: pal = (hi, lt, md, dk)"""
        hi, lt, md, dk = pal
        self.rect(x0, y0, x1, y1, md)
        self.rect(x0, y0, x1, y0, hi); self.rect(x0, y0, x0, y1, lt)
        self.rect(x0, y1, x1, y1, dk); self.rect(x1, y1 - 0, x1, y0 + 1, dk)
    def shade_mask(self, m, cx, cy, rx, ry, pal):
        """tô sáng/tối theo hướng ánh sáng (trên-trái sáng) trong vùng m"""
        hi, lt, md, dk = pal
        nx, ny = (self.xs - cx) / rx, (self.ys - cy) / ry
        s = nx * 0.55 + ny * 0.85
        self.fill(m & (s < 0.55), md)
        self.fill(m & (s < -0.15), lt)
        self.fill(m & (s < -0.62), hi)
        self.fill(m & (s > 0.55), dk)
    def outline(self, c=OUT):
        m = self.mask()
        o = dilate(m, 1) & ~m
        self.a[o] = (*c, 255)
    def rim(self, warm=(226, 144, 72), cool=(160, 200, 238)):
        m = self.mask()
        right = m & ~shift(m, -1, 0)
        top = m & ~shift(m, 0, 1)
        left = m & ~shift(m, 1, 0)
        self.a[left & ~top, :3] = cool
        self.a[top, :3] = cool
        self.a[right & ~top, :3] = warm
    def image(self):
        return Image.fromarray(self.a, 'RGBA')

STEEL = dict(hi=(176, 188, 206), lt=(132, 143, 162), md=(90, 98, 116), dk=(54, 60, 76), dd=(36, 40, 54))
P = (STEEL['hi'], STEEL['lt'], STEEL['md'], STEEL['dk'])
RED = ((244, 104, 92), (206, 48, 54), (148, 30, 40), (96, 20, 32))
BRONZE = ((246, 206, 120), (214, 160, 70), (170, 118, 48), (112, 72, 30))
SHADOW_C = (4, 6, 14)

def shadow(cv, cx, cy, rx, ry, a=120):
    bay = bayer_grid(cv.w, cv.h)
    d = np.sqrt(((cv.xs - cx) / rx) ** 2 + ((cv.ys - cy) / ry) ** 2)
    lvl = np.clip(1.15 - d, 0, 1) * 1.1
    m = (lvl > bay) & ~cv.mask()
    cv.a[m] = (*SHADOW_C, a)

# ------------------------------------------------------------------ hiệp sĩ
KW, KH = 48, 72

def limb(cv, p0, p1, w, pal):
    (x0, y0), (x1, y1) = p0, p1
    dx, dy = x1 - x0, y1 - y0; L = math.hypot(dx, dy) or 1
    nx, ny = -dy / L * w / 2, dx / L * w / 2
    m = cv.poly([(x0 + nx, y0 + ny), (x1 + nx, y1 + ny), (x1 - nx, y1 - ny), (x0 - nx, y0 - ny)], pal[2])
    cv.shade_mask(m, (x0 + x1) / 2, (y0 + y1) / 2, w, max(abs(dy), w) / 1.4, pal)
    return m

def knight(breath=0, sway=0):
    cv = Cv(KW, KH)
    u = -breath
    # --- chân
    for x0 in (16, 27):
        cv.bevel(x0, 60, x0 + 5, 67, P)
        cv.rect(x0, 60, x0 + 5, 61, STEEL['lt']); cv.rect(x0 + 1, 60, x0 + 4, 60, STEEL['hi'])
        cv.rect(x0 + 5, 61, x0 + 5, 67, STEEL['dk'])
        cv.rect(x0 - 1, 66, x0 + 6, 69, STEEL['dk']); cv.rect(x0 - 1, 66, x0 + 6, 66, STEEL['md']); cv.rect(x0 - 1, 69, x0 + 6, 69, STEEL['dd'])
    # --- váy giáp
    cv.poly([(15, 52), (33, 52), (36, 60), (12, 60)], STEEL['md'])
    for x in range(14, 35, 5): cv.rect(x, 55, x, 60, STEEL['dk'])
    cv.rect(14, 52, 34, 52, STEEL['lt']); cv.rect(12, 59, 36, 60, STEEL['dk']); cv.rect(12, 59, 36, 59, STEEL['md'])
    # --- thân giáp
    chest = cv.poly([(16, 34 + u), (32, 34 + u), (34, 40 + u), (33, 48), (31, 53), (17, 53), (15, 48), (14, 40 + u)], STEEL['md'])
    cv.shade_mask(chest, 22, 40, 13, 13, P)
    cv.rect(23, 36 + u, 24, 50, STEEL['lt']); cv.rect(23, 36 + u, 23, 49, STEEL['hi'])
    cv.rect(17, 45, 22, 45, STEEL['dk']); cv.rect(25, 45, 31, 45, STEEL['dk'])
    cv.ell(24, 40 + u, 2, 2, BRONZE[1]); cv.px(23, 39 + u, BRONZE[0]); cv.px(24, 40 + u, (110, 168, 228))
    cv.rect(15, 50, 33, 53, (46, 34, 30)); cv.rect(15, 50, 33, 50, (78, 58, 46))
    cv.rect(22, 50, 26, 53, BRONZE[2]); cv.rect(23, 51, 25, 52, BRONZE[3]); cv.px(24, 51, BRONZE[0])
    # --- cổ giáp
    cv.rect(19, 31 + u, 29, 35 + u, STEEL['dk']); cv.rect(19, 31 + u, 29, 31 + u, STEEL['md'])
    # --- tay: bắp tay + khuỷu + cẳng tay chụm vào chuôi kiếm
    for s in (-1, 1):
        limb(cv, (24 + s * 11, 41 + u), (24 + s * 12, 47), 6, P)
        limb(cv, (24 + s * 12, 47), (24 + s * 3, 53), 6, P)
        cv.ell(24 + s * 12, 47, 3.2, 3.2, STEEL['lt']); cv.px(24 + s * 12 - 1, 46, STEEL['hi']); cv.px(24 + s * 12 + 1, 48, STEEL['dk'])
    # --- vai
    for s in (-1, 1):
        cx = 24 + s * 12
        m = cv.ell(cx, 38 + u, 7.5, 6.5, STEEL['md'])
        cv.shade_mask(m, cx, 38 + u, 7.5, 6.5, P)
        cv.fill(m & (cv.ys == 40 + u), STEEL['dk']); cv.fill(m & (cv.ys == 41 + u), STEEL['dk'])
        cv.px(cx + (-3 if s < 0 else 3), 35 + u, STEEL['hi']); cv.px(cx, 37 + u, BRONZE[1]); cv.px(cx, 38 + u, BRONZE[0])
        cv.poly([(cx - 1, 32 + u), (cx + 1, 32 + u), (cx, 29 + u)], STEEL['lt'])
    # --- mũ
    helm = cv.poly([(19, 9 + u), (28, 9 + u), (31, 11 + u), (33, 14 + u), (33, 27 + u), (31, 30 + u), (28, 33 + u), (20, 33 + u), (17, 30 + u), (15, 27 + u), (15, 14 + u), (17, 11 + u)], STEEL['md'])
    cv.shade_mask(helm, 22, 20 + u, 10, 12, P)
    cv.rect(23, 9 + u, 24, 18 + u, STEEL['hi'])
    cv.rect(16, 18 + u, 32, 22 + u, (9, 11, 19))
    cv.rect(16, 17 + u, 32, 17 + u, STEEL['hi'])
    cv.rect(16, 23 + u, 32, 23 + u, STEEL['dk'])
    cv.rect(23, 22 + u, 24, 32 + u, STEEL['lt']); cv.rect(24, 22 + u, 24, 32 + u, STEEL['md'])
    for x, y in [(19, 20), (20, 20), (27, 20), (28, 20)]: cv.px(x, y + u, (132, 188, 238))
    for x in (18, 20, 27, 29):
        for y in (26, 28, 30): cv.px(x, y + u, STEEL['dd'])
    # --- chùm lông đỏ
    pl = [(20, 10 + u), (20, 7 + u), (18 + sway, 3 + u), (21 + sway, 0 + u), (27 + sway, 0 + u), (31 + sway, 4 + u), (32 + sway, 9 + u), (29, 11 + u)]
    pm = cv.poly(pl, RED[2])
    cv.fill(pm & (cv.xs < 24 + sway) & (cv.ys > 3 + u), RED[1])
    cv.fill(pm & (cv.ys < 6 + u) & (cv.xs < 27 + sway), RED[0])
    cv.fill(pm & (cv.xs > 28 + sway) & (cv.ys > 6 + u), RED[3])
    tail = cv.poly([(28, 8 + u), (32 + sway, 7 + u), (37 + sway, 12 + u), (36 + sway, 19 + u), (33, 14 + u)], RED[2])
    cv.fill(tail & (cv.ys < 12 + u), RED[1])
    cv.px(24 + sway, 1 + u, RED[0]); cv.px(23 + sway, 2 + u, RED[0]); cv.px(20, 6 + u, RED[0]); cv.px(25 + sway, 1 + u, RED[0])
    # --- kiếm
    cv.rect(22, 57, 25, 69, (186, 200, 220)); cv.rect(24, 57, 25, 69, (136, 150, 174)); cv.rect(23, 57, 23, 68, (236, 244, 252))
    cv.px(23, 70, (150, 164, 188)); cv.px(22, 70, (186, 200, 220))
    cv.rect(23, 48, 24, 55, (70, 48, 34)); cv.rect(23, 48, 23, 55, (104, 76, 50))
    cv.ell(24, 46, 2.4, 2.4, BRONZE[1]); cv.px(23, 45, BRONZE[0]); cv.px(25, 47, BRONZE[3])
    cv.rect(14, 56, 33, 57, BRONZE[2]); cv.rect(14, 56, 33, 56, BRONZE[0]); cv.rect(14, 57, 33, 57, BRONZE[3])
    cv.rect(13, 55, 14, 58, BRONZE[1]); cv.rect(33, 55, 34, 58, BRONZE[1])
    cv.px(24, 56, (110, 168, 228))
    # --- găng tay
    for s in (-1, 1):
        x0 = 20 if s < 0 else 25
        cv.rect(x0, 50, x0 + 3, 54, STEEL['md']); cv.rect(x0, 50, x0 + 3, 50, STEEL['hi'])
        cv.rect(x0, 54, x0 + 3, 54, STEEL['dk']); cv.rect(x0, 52, x0 + 3, 52, STEEL['lt'])
    cv.rim()
    cv.outline()
    shadow(cv, 24, 69, 19, 3.2)
    return cv.image()

def make_knight_frames():
    return [knight(0, 0), knight(1, 0), knight(1, 1), knight(0, 1)]

# ------------------------------------------------------------------ đài đuốc
TW, TH = 44, 120

def torch_pillar():
    cv = Cv(TW, TH)
    cx = 22
    ST = [(138, 150, 172), (96, 108, 132), (66, 76, 98), (42, 50, 68)]  # đá xanh xám lạnh
    IRON = [(150, 158, 174), (104, 110, 126), (68, 72, 86), (40, 44, 56)]
    # bệ
    for (x0, x1, y0, y1) in [(4, 40, 108, 116), (8, 36, 100, 108)]:
        cv.bevel(x0, y0, x1, y1, ST)
    for x in range(10, 40, 9): cv.rect(x, 109, x, 116, ST[3])
    for x in range(13, 36, 9): cv.rect(x, 101, x, 108, ST[3])
    # thân trụ (gạch)
    cv.rect(11, 44, 33, 100, ST[2])
    for y in range(44, 100, 6):
        cv.rect(11, y, 33, y, ST[3])
        off = 0 if (y // 6) % 2 == 0 else 5
        for x in range(11 + off + 6, 33, 11): cv.rect(x, y, x, y + 5, ST[3])
    cv.rect(11, 44, 12, 100, ST[1]); cv.rect(11, 44, 11, 100, ST[0])
    cv.rect(32, 44, 33, 100, ST[3])
    for _ in range(20):
        x, y = random.Random(_ * 7).randrange(13, 31), random.Random(_ * 13).randrange(46, 98)
        cv.px(x, y, ST[1] if _ % 2 else ST[3])
    # rêu ở chân trụ
    for x in range(11, 34):
        h = int(2 + 2 * math.sin(x * .9) + (x % 3))
        for k in range(h): cv.px(x, 99 - k, (28, 78, 60) if k < h - 1 else (52, 120, 88))
    # tượng gargoyle bằng kim loại xám
    # cánh gập hai bên
    for s in (-1, 1):
        wx = cx + s * 12
        cv.poly([(wx, 54), (wx + s * 7, 52), (wx + s * 9, 60), (wx + s * 6, 62), (wx + s * 8, 70), (wx + s * 3, 68), (wx + s * 3, 76), (wx, 72)], IRON[2])
        cv.line(wx, 56, wx + s * 8, 54, IRON[0]); cv.line(wx + s * 1, 60, wx + s * 6, 62, IRON[1]); cv.line(wx + s * 1, 66, wx + s * 4, 70, IRON[3])
    # sừng
    for s in (-1, 1):
        cv.poly([(cx + s * 7, 56), (cx + s * 10, 52), (cx + s * 12, 46), (cx + s * 13, 42), (cx + s * 14, 49), (cx + s * 12, 56), (cx + s * 9, 60)], IRON[1])
        cv.line(cx + s * 9, 54, cx + s * 13, 44, IRON[0])
    # mặt
    face = cv.poly([(cx - 9, 54), (cx + 9, 54), (cx + 10, 62), (cx + 8, 72), (cx + 4, 78), (cx - 4, 78), (cx - 8, 72), (cx - 10, 62)], IRON[2])
    cv.shade_mask(face, cx, 64, 10, 12, tuple(IRON))
    cv.rect(cx - 9, 58, cx + 9, 59, IRON[3])                                  # mày
    cv.poly([(cx - 9, 57), (cx - 2, 60), (cx - 2, 62), (cx - 9, 60)], IRON[3]); cv.poly([(cx + 9, 57), (cx + 2, 60), (cx + 2, 62), (cx + 9, 60)], IRON[3])
    for s in (-1, 1):
        cv.rect(cx + s * 5 - (1 if s > 0 else 0), 62, cx + s * 5 + (0 if s > 0 else 1), 63, (255, 168, 54))
        cv.px(cx + s * 5 - (1 if s > 0 else 0), 62, (255, 232, 140))
        cv.rect(cx + s * 5 - 2, 61, cx + s * 5 + 2, 61, IRON[3]) if False else None
    cv.rect(cx - 1, 63, cx + 1, 68, IRON[1]); cv.px(cx, 64, IRON[0]); cv.rect(cx - 2, 68, cx + 2, 68, IRON[3])   # mũi
    cv.rect(cx - 6, 71, cx + 6, 74, (18, 16, 24))                                                                 # miệng
    for x in range(cx - 6, cx + 7, 3):
        cv.rect(x, 71, x + 1, 73, (206, 214, 228)); cv.px(x, 72, (150, 158, 176))                                  # nanh
    cv.rect(cx - 5, 74, cx + 5, 75, IRON[3])
    # mũ trụ - bát đốt lửa
    cv.poly([(5, 34), (39, 34), (36, 42), (33, 46), (11, 46), (8, 42)], IRON[2])
    cv.rect(5, 34, 39, 35, IRON[0]); cv.rect(5, 36, 39, 36, IRON[1])
    cv.rect(8, 40, 36, 40, IRON[3]); cv.rect(11, 45, 33, 46, IRON[3])
    for x in (9, 17, 25, 33): cv.px(x, 38, IRON[0]); cv.px(x, 39, IRON[3])
    cv.rect(11, 46, 33, 49, ST[3]); cv.rect(11, 46, 33, 46, ST[1])             # cổ trụ trên
    # lòng bát cháy: than hồng
    cv.rect(9, 32, 35, 34, (92, 40, 22)); cv.rect(11, 31, 33, 33, (184, 74, 24)); cv.rect(13, 31, 31, 32, (255, 140, 48))
    for x in range(12, 33, 3): cv.px(x, 31, (255, 214, 110))
    cv.rect(5, 34, 39, 34, (255, 190, 100))
    cv.rim(warm=(214, 132, 72), cool=(170, 200, 236))
    cv.outline()
    shadow(cv, cx, 117, 24, 4, 130)
    return cv.image()

def flame_frame(k, n=4):
    w, h = 24, 36
    cv = Cv(w, h)
    t = k / n * 2 * math.pi
    cx = w / 2 - 0.5
    layers = [((255, 104, 24), 1.0), ((255, 154, 38), .78), ((255, 206, 82), .55), ((255, 244, 188), .30)]
    for col, s in layers:
        for y in range(h):
            v = 1 - y / (h - 1)           # 0 ở đáy, 1 ở đỉnh -> đảo
            yy = h - 1 - y
            tt = yy / (h * 0.92 * (0.55 + 0.45 * s))
            if tt >= 1: continue
            wid = (1 - tt ** 1.5) * (w * 0.46) * s * (0.85 + 0.15 * math.sin(t + yy * .35))
            wob = math.sin(t + yy * 0.28) * (1.0 + tt * 1.6) + math.sin(t * 2 + yy * .5) * .6 * tt
            x0, x1 = cx + wob - wid, cx + wob + wid
            for x in range(int(round(x0)), int(round(x1)) + 1):
                cv.px(x, y, col)
    # tia lửa nhỏ tách ra
    for i in range(3):
        a = t + i * 2.1
        sx = cx + math.sin(a) * 5; sy = 4 + (i * 5 + k * 3) % 12
        cv.px(round(sx), round(sy), (255, 214, 120))
    return cv.image()

if __name__ == '__main__':
    kf = make_knight_frames()
    for i, f in enumerate(kf): f.save(f'/home/claude/out/knight_{i}.png')
    tp = torch_pillar(); tp.save('/home/claude/out/torch.png')
    ff = [flame_frame(i) for i in range(4)]
    for i, f in enumerate(ff): f.save(f'/home/claude/out/flame_{i}.png')
    # bảng xem trước
    sheet = Image.new('RGBA', (48 * 4 + 44 + 24 * 4 + 40, 130), (24, 40, 64, 255))
    x = 4
    for f in kf: sheet.alpha_composite(f, (x, 4)); x += 48
    sheet.alpha_composite(tp, (x, 4)); x += 44
    for f in ff: sheet.alpha_composite(f, (x, 4)); x += 24
    upscale(sheet, 5).save('/home/claude/out/sprites_preview.png')
    print(sheet.size)
