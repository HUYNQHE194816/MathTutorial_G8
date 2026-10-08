from common import *

HORIZON = 200
MOON = (240, 62, 46)     # x, y, bán kính (pixel art)

def horizon_y(x):
    return int(HORIZON + 2 * math.sin(x / 37.0) + 1.5 * math.sin(x / 11.0 + 1))

def layer():
    return Image.new('RGBA', (W, H), (0, 0, 0, 0))

# ------------------------------------------------------------------ cây khô
def branch(d, x, y, ang, length, width, depth, rng, col, droop=0.03):
    segs = 3
    for _ in range(segs):
        a = ang + rng.uniform(-0.28, 0.28) + (droop if ang > 0 else -droop)
        l = length / segs
        nx, ny = x + math.sin(a) * l, y - math.cos(a) * l
        d.line([(x, y), (nx, ny)], fill=col, width=max(1, int(round(width))))
        x, y, ang = nx, ny, a
        width = max(1.0, width * 0.8)
    if depth > 0:
        for _ in range(rng.choice([2, 2, 3])):
            da = rng.uniform(0.35, 0.95) * rng.choice([-1, 1])
            branch(d, x, y, ang + da, length * rng.uniform(0.5, 0.72), width * 0.8, depth - 1, rng, col, droop)

def dead_tree(lay, x0, y0, h, w0, col, rng, lean=0.0, nb=9, depth=3, reach=1.0):
    d = ImageDraw.Draw(lay)
    steps, phase = 24, rng.uniform(0, 6)
    L, R, C = [], [], []
    for i in range(steps + 1):
        t = i / steps
        cx = x0 + lean * h * t * t + math.sin(t * 4 + phase) * h * 0.015
        cy = y0 - h * t
        w = w0 * (1 - t * 0.82) + 1 + (w0 * 0.8 * max(0, 0.12 - t) / 0.12)   # gốc loe ra
        L.append((cx - w / 2, cy)); R.append((cx + w / 2, cy)); C.append((cx, cy, w))
    d.polygon(L + R[::-1], fill=col)
    for k in range(nb):
        t = 0.22 + 0.7 * (k + rng.uniform(0, .6)) / nb
        i = min(steps, int(t * steps)); cx, cy, w = C[i]
        side = 1 if k % 2 == 0 else -1
        if rng.random() < .25: side = -side
        ang = side * rng.uniform(0.85, 1.4)
        length = h * rng.uniform(0.2, 0.42) * (1.15 - t * 0.7) * reach
        branch(d, cx, cy, ang, length, max(1.5, w * 0.6), depth, rng, col)
    cx, cy, w = C[-1]
    for s in (-1, 1):
        branch(d, cx, cy, s * rng.uniform(.15, .5), h * .16, 2, depth, rng, col)

# ------------------------------------------------------------------ tàn tích
def ruin(arr, shape_fn, body, mortar, rim, lit_side, fog_col, fog, seed):
    m_img = Image.new('L', (W, H), 0)
    shape_fn(ImageDraw.Draw(m_img))
    m = np.array(m_img) > 0
    ys, xs = np.mgrid[0:H, 0:W]
    row = ys // 5; off = (row % 2) * 6; colx = (xs + off) // 12
    hs = ((row * 73856093) ^ (colx * 19349663) ^ seed) % 5
    col = np.zeros((H, W, 3), np.int32)
    for c in range(3):
        col[..., c] = body[c] + (hs - 2) * 3
    mort = ((ys % 5) == 0) | (((xs + off) % 12) == 0)
    for c in range(3):
        col[..., c] = np.where(mort, mortar[c], col[..., c])
    # sáng ở mép hướng về trăng
    edge = m & ~shift(m, 0, 1)
    edge |= m & ~shift(m, -lit_side, 0)
    for c in range(3):
        col[..., c] = np.where(edge, rim[c], col[..., c])
    # sương mù xa
    for c in range(3):
        col[..., c] = (col[..., c] * (1 - fog) + fog_col[c] * fog)
    arr[m, :3] = np.clip(col[m], 0, 255).astype(np.uint8)

def left_ruin(d):
    d.polygon([(88, 205), (88, 158), (96, 152), (104, 156), (112, 146), (122, 150), (128, 166), (140, 168), (146, 182), (152, 205)], fill=255)
    d.rectangle([74, 148, 85, 205], fill=255)                              # cột gãy
    d.polygon([(74, 148), (78, 141), (82, 146), (85, 143), (85, 148)], fill=255)
    d.rectangle([101, 168, 110, 186], fill=0)                              # ô cửa sổ
    d.polygon([(101, 168), (105.5, 162), (110, 168)], fill=0)

def right_ruin(d):
    d.rectangle([378, 120, 440, 205], fill=255)
    d.rectangle([393, 146, 425, 205], fill=0)                              # cổng vòm
    d.ellipse([393, 130, 425, 162], fill=0)
    d.polygon([(425, 120), (440, 120), (440, 132), (432, 128)], fill=0)    # mẻ vỡ
    d.polygon([(378, 120), (378, 128), (384, 124)], fill=0)
    d.polygon([(440, 205), (440, 168), (452, 164), (460, 172), (470, 170), (480, 180), (480, 205)], fill=255)  # tường thấp

def center_ruin(d):
    d.rectangle([166, 178, 174, 205], fill=255)
    d.polygon([(166, 178), (169, 172), (172, 176), (174, 174), (174, 178)], fill=255)
    d.rectangle([300, 184, 307, 205], fill=255)
    d.polygon([(300, 184), (303, 180), (307, 183)], fill=255)

# ------------------------------------------------------------------ nền
def make_background():
    rng = random.Random(11)
    arr = np.zeros((H, W, 4), np.uint8); arr[..., 3] = 255
    ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
    bay = bayer_grid(W, H)

    # --- bầu trời: các dải màu + dither
    sky = [hexc(c)[:3] for c in ['#03050b', '#050a16', '#08122a', '#0b1c3c', '#10284f', '#173762', '#21497b']]
    v = np.clip(ys / (HORIZON + 4), 0, 1) ** 1.15 * (len(sky) - 1)
    idx = np.floor(v).astype(int); frac = v - idx
    idx = np.minimum(idx + (frac > bay).astype(int), len(sky) - 1)
    for c in range(3):
        arr[..., c] = np.array([s[c] for s in sky], np.uint8)[idx]

    # --- quầng sáng trăng (xanh lam lạnh), bậc thang
    mx, my, mr = MOON
    dist = np.sqrt((xs - mx) ** 2 + (ys - my) ** 2)
    st = np.clip(1 - (dist - mr) / 120.0, 0, 1) ** 2
    q = np.floor(st * 6) + ((st * 6 - np.floor(st * 6)) > bay)
    a = (q / 6.0 * 0.62)[..., None]
    halo = np.array([84, 150, 224], np.float32)
    arr[..., :3] = (arr[..., :3] * (1 - a) + halo * a).astype(np.uint8)

    # --- sao
    for _ in range(95):
        x, y = rng.randrange(W), rng.randrange(0, 150)
        if math.hypot(x - mx, y - my) < mr + 14: continue
        b = rng.choice([(110, 150, 215), (140, 180, 235), (190, 215, 250)])
        arr[y, x, :3] = b
        if rng.random() < .12:
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)):
                if 0 <= x + dx < W and 0 <= y + dy < H: arr[y + dy, x + dx, :3] = (70, 105, 170)

    # --- cây rất xa (phía sau trăng và hai bên)
    far = layer(); fr = random.Random(5)
    for x, h in [(150, 78), (196, 56), (284, 62), (330, 84), (118, 60), (368, 66), (60, 70), (426, 74)]:
        dead_tree(far, x, 205, h, 4, hexc('#13294a'), fr, lean=fr.uniform(-.1, .1), nb=7, depth=3)
    far_a = np.array(far)
    m = far_a[..., 3] > 0
    arr[m, :3] = far_a[m, :3]

    # --- trăng
    disc = dist <= mr
    base = np.array([186, 218, 246], np.float32)
    lx, ly = (xs - mx) / mr, (ys - my) / mr
    sh = lx * -0.45 + ly * -0.55          # >0 ở phía tối (dưới phải)
    col = np.zeros((H, W, 3), np.float32); col[:] = base
    nz = fbm(W, H, 14, 99, 3)
    mare = (nz > 0.56 - 0.0)
    col[mare] = (150, 186, 226)
    mare2 = (nz > 0.64)
    col[mare2] = (128, 166, 212)
    shade = (-sh) > 0.38
    col[shade & ~mare] = (160, 196, 232); col[shade & mare] = (122, 158, 206)
    rim = disc & (dist > mr - 1.6) & ((lx + ly) < -0.1)
    col[rim] = (232, 246, 255)
    dark_rim = disc & (dist > mr - 1.6) & ((lx + ly) > 0.3)
    col[dark_rim] = (108, 148, 200)
    for cx, cy, rx, ry in [(mx - 14, my - 10, 7, 6), (mx + 16, my + 14, 9, 7), (mx + 6, my - 24, 5, 4), (mx - 24, my + 14, 4, 4), (mx + 26, my - 6, 4, 3)]:
        cm = (((xs - cx) / rx) ** 2 + ((ys - cy) / ry) ** 2) <= 1
        col[cm & disc] = (136, 172, 214)
        cm2 = (((xs - cx + 1.5) / rx) ** 2 + ((ys - cy + 1.5) / ry) ** 2) <= 0.55
        col[cm2 & disc & cm] = (120, 156, 202)
        hl = cm & ~shift(cm, 1, 1) & disc
        col[hl] = (200, 228, 250)
    arr[disc, :3] = col[disc].astype(np.uint8)

    # --- dải sương ngang cắt ngang mặt trăng (ánh sáng xuyên màn sương)
    for yy, hh, lv in [(48, 3, .40), (60, 2, .55), (72, 4, .45), (84, 2, .5), (36, 2, .35)]:
        band = ((ys >= yy) & (ys < yy + hh))
        wob = np.sin(xs / 23.0 + yy) * 1.2
        band = ((ys + wob) >= yy) & ((ys + wob) < yy + hh)
        level = np.where(band, lv, 0) * np.clip(1 - np.abs(xs - mx) / 150.0, 0.25, 1)
        dith_blend(arr, (88, 128, 178), level, bay) if False else None
        mm = level > bay
        arr[mm, :3] = (arr[mm, :3].astype(np.float32) * 0.55 + np.array([70, 112, 168]) * 0.45).astype(np.uint8)

    # --- sương xa sát chân trời
    lvl = np.clip(1 - np.abs(ys - (HORIZON - 6)) / 38.0, 0, 1) ** 1.3 * 0.75
    mm = lvl > bay
    arr[mm, :3] = (arr[mm, :3].astype(np.float32) * 0.45 + np.array([120, 164, 208]) * 0.55).astype(np.uint8)

    # --- tàn tích xa
    ruin(arr, center_ruin, (24, 40, 62), (18, 31, 50), (60, 96, 140), 1, (70, 110, 160), 0.38, 1)
    ruin(arr, left_ruin, (28, 45, 68), (19, 32, 52), (72, 112, 160), 1, (70, 110, 160), 0.25, 2)
    ruin(arr, right_ruin, (28, 45, 68), (19, 32, 52), (72, 112, 160), -1, (70, 110, 160), 0.25, 3)

    # --- cây trung cảnh
    mid = layer(); mr_ = random.Random(21)
    for x, h, lean in [(36, 170, .12), (440, 176, -.12), (120, 128, .05), (356, 120, -.06)]:
        dead_tree(mid, x, 206, h, 8, hexc('#09152a'), mr_, lean=lean, nb=8, depth=3, reach=0.95)
    mm = np.array(mid)[..., 3] > 0
    arr[mm, :3] = np.array(mid)[mm, :3]

    # --- mặt đất
    gy = np.array([horizon_y(x) for x in range(W)])
    ground = ys >= gy[None, :]
    u = np.clip((ys - HORIZON) / 70.0, 0, 1.2)          # độ gần camera
    gcols = [hexc(c)[:3] for c in ['#071a1a', '#0b2422', '#10302b', '#17443a']]
    n = fbm(W, H, 5, 3, 3)
    gl = np.clip(n * 3.0 - 0.35, 0, 3)
    gi = np.minimum(np.floor(gl).astype(int) + ((gl - np.floor(gl)) > bay), 3)
    gcol = np.zeros((H, W, 3), np.uint8)
    for c in range(3): gcol[..., c] = np.array([g[c] for g in gcols], np.uint8)[gi]
    arr[ground, :3] = gcol[ground]

    # gạch lát (phối cảnh) xen cỏ
    zone = fbm(W, H, 30, 41, 3)
    stone = ground & (zone > 0.47) & (ys > gy[None, :] + 4)
    K = 9
    rowf = (np.clip(u, 0.001, 1) ** (1 / 1.55)) * K
    rid = np.floor(rowf).astype(int)
    ru = (rid + 0.5) / K
    bw = (7 + 22 * ru ** 1.55).astype(int)
    boff = (rid % 2) * (bw // 2)
    joint_x = ((xs.astype(int) + boff) % bw) == 0
    joint_y = np.floor(rowf) != np.floor(np.vstack([rowf[:1], rowf[:-1]]))
    hsh = ((rid * 2654435761) ^ (((xs.astype(int) + boff) // bw) * 40503)) % 11
    sc = np.zeros((H, W, 3), np.int32)
    for c, b in enumerate((44, 62, 84)):
        sc[..., c] = b + (hsh % 4 - 1) * 4 + (u * 10).astype(int)
    for c, b in enumerate((18, 28, 40)):
        sc[..., c] = np.where(joint_x | joint_y, b, sc[..., c])
    hi = (~joint_y) & shift(joint_y, 0, 1) & ~joint_x
    for c, b in enumerate((86, 118, 154)):
        sc[..., c] = np.where(hi, b, sc[..., c])
    missing = (hsh == 0) | (hsh == 7)
    st_mask = stone & ~missing
    moss = (fbm(W, H, 7, 61, 2) > 0.58) & st_mask
    moss_d = moss & (bay > 0.45)
    for c, b in enumerate((34, 92, 70)):
        sc[..., c] = np.where(moss_d, b, sc[..., c])
    arr[st_mask, :3] = np.clip(sc[st_mask], 0, 255).astype(np.uint8)
    # vết nứt
    cr = random.Random(77)
    for _ in range(26):
        x, y = cr.randrange(W), cr.randrange(HORIZON + 6, H - 4)
        if not st_mask[y, x]: continue
        for _s in range(cr.randrange(4, 9)):
            if 0 <= x < W and 0 <= y < H and st_mask[y, x]: arr[y, x, :3] = (14, 22, 32)
            x += cr.choice([-1, 0, 1]); y += cr.choice([0, 1])

    # chùm cỏ
    gr = random.Random(5)
    for _ in range(420):
        x = gr.randrange(W); y = gr.randrange(HORIZON + 3, H - 2)
        if y < gy[x] + 2: continue
        uu = (y - HORIZON) / 70.0
        hgt = int(2 + uu * 4)
        c1 = (31, 92, 70) if gr.random() < .5 else (23, 68, 54)
        for b in range(gr.choice([2, 3, 3])):
            bx = x + (b - 1) * (1 if uu < .4 else 2); bh = hgt - abs(b - 1) + gr.randrange(0, 2)
            for k in range(bh):
                px, py = bx + (k // 3) * (b - 1), y - k
                if 0 <= px < W and 0 <= py < H and ground[py, px]:
                    arr[py, px, :3] = c1 if k < bh - 1 else (66, 142, 104)

    # ánh trăng trên nền đất
    gd = np.sqrt(((xs - 240) / 190.0) ** 2 + ((ys - 212) / 55.0) ** 2)
    lvl = np.clip(1 - gd, 0, 1) * 0.30
    mm = ground & (lvl > bay)
    arr[mm, :3] = (arr[mm, :3].astype(np.float32) * 0.62 + np.array([84, 140, 200]) * 0.38).astype(np.uint8)

    # vầng sáng ấm từ đuốc trên mặt đất (tĩnh)
    for tx in (122, 358):
        gd = np.sqrt(((xs - tx) / 70.0) ** 2 + ((ys - 246) / 20.0) ** 2)
        lvl = np.clip(1 - gd, 0, 1) * 0.5
        mm = ground & (lvl > bay)
        arr[mm, :3] = (arr[mm, :3].astype(np.float32) * 0.78 + np.array([230, 140, 70]) * 0.22).astype(np.uint8)

    # --- cây cận cảnh (khung hai bên, cành vươn vào trong)
    near = layer(); nr = random.Random(33)
    dead_tree(near, 8, 262, 262, 22, hexc('#04080f'), nr, lean=.10, nb=11, depth=4, reach=1.25)
    dead_tree(near, 474, 262, 258, 22, hexc('#04080f'), nr, lean=-.10, nb=11, depth=4, reach=1.25)
    na = np.array(near); mm = na[..., 3] > 0
    arr[mm, :3] = na[mm, :3]
    # viền sáng xanh lạnh mỏng ở mép cây hướng về trăng
    for lit in (1, -1):
        e = mm & ~shift(mm, -lit, 0) & ((xs < 240) if lit == 1 else (xs > 240))
        arr[e, :3] = (22, 40, 66)
    return Image.fromarray(arr, 'RGBA')

if __name__ == '__main__':
    bg = make_background()
    bg.save('/home/claude/out/bg.png')
    upscale(bg).save('/home/claude/out/bg_preview.png')
    print('done')
