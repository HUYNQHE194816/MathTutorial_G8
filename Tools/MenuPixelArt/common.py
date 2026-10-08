"""Tiện ích chung cho bộ vẽ pixel art màn hình chính. Lưới pixel: 1 pixel art = 4 pixel canvas (1920x1080 = 480x270)."""
import math, random
import numpy as np
from PIL import Image, ImageDraw, ImageFont, ImageChops

W, H = 480, 270          # độ phân giải pixel art của toàn màn hình
S = 4                    # hệ số phóng to khi đặt lên canvas 1920x1080

BAYER = np.array([[0, 8, 2, 10], [12, 4, 14, 6], [3, 11, 1, 9], [15, 7, 13, 5]], dtype=np.float32) / 16.0 + 1 / 32.0

def hexc(h, a=255):
    h = h.lstrip('#'); return (int(h[0:2], 16), int(h[2:4], 16), int(h[4:6], 16), a)

def bayer_grid(w, h):
    return np.tile(BAYER, (h // 4 + 1, w // 4 + 1))[:h, :w]

def to_canvas(cx, cy):
    """toạ độ canvas (gốc ở tâm, y hướng lên) -> toạ độ ảnh pixel art (y hướng xuống)"""
    return W / 2 + cx / S, H / 2 - cy / S

def mask_of(img):
    return np.array(img.split()[3]) > 0

def dilate(m, n=1):
    for _ in range(n):
        p = np.pad(m, 1)
        m = p[1:-1, 1:-1] | p[:-2, 1:-1] | p[2:, 1:-1] | p[1:-1, :-2] | p[1:-1, 2:]
    return m

def shift(m, dx, dy):
    out = np.zeros_like(m)
    h, w = m.shape
    ys, yd = (slice(0, h - dy), slice(dy, h)) if dy >= 0 else (slice(-dy, h), slice(0, h + dy))
    xs, xd = (slice(0, w - dx), slice(dx, w)) if dx >= 0 else (slice(-dx, w), slice(0, w + dx))
    out[yd, xd] = m[ys, xs]
    return out

def paint(arr, m, color):
    """tô màu (r,g,b[,a]) lên mảng RGBA (h,w,4) tại mặt nạ m (ghi đè)."""
    c = np.array(color if len(color) == 4 else (*color, 255), dtype=np.uint8)
    arr[m] = c

def blend(arr, m, color, alpha):
    """pha màu lên mảng RGBA đã có nền đục; alpha 0..1 (có thể là mảng cùng kích thước)."""
    c = np.array(color[:3], dtype=np.float32)
    a = alpha if np.isscalar(alpha) else alpha[m][:, None]
    arr[m, :3] = (arr[m, :3].astype(np.float32) * (1 - a) + c * a).astype(np.uint8)

def dith_blend(arr, color, level, bay=None):
    """level: mảng 0..1; pha màu theo ngưỡng Bayer (không pha mềm -> giữ pixel art)."""
    h, w = level.shape
    if bay is None: bay = bayer_grid(w, h)
    m = level > bay
    paint_rgb = np.array(color[:3], dtype=np.uint8)
    arr[m, :3] = paint_rgb
    return m

def value_noise(w, h, cell, seed, wrap_x=False):
    rng = np.random.RandomState(seed)
    gw, gh = w // cell + 2, h // cell + 2
    g = rng.rand(gh, gw).astype(np.float32)
    if wrap_x:
        gw = w // cell
        g = rng.rand(gh, gw).astype(np.float32)
    ys, xs = np.mgrid[0:h, 0:w].astype(np.float32)
    fx, fy = xs / cell, ys / cell
    x0, y0 = np.floor(fx).astype(int), np.floor(fy).astype(int)
    tx, ty = fx - x0, fy - y0
    tx, ty = tx * tx * (3 - 2 * tx), ty * ty * (3 - 2 * ty)
    def at(yy, xx):
        xx = xx % g.shape[1] if wrap_x else np.minimum(xx, g.shape[1] - 1)
        return g[np.minimum(yy, g.shape[0] - 1), xx]
    return (at(y0, x0) * (1 - tx) * (1 - ty) + at(y0, x0 + 1) * tx * (1 - ty) +
            at(y0 + 1, x0) * (1 - tx) * ty + at(y0 + 1, x0 + 1) * tx * ty)

def fbm(w, h, cell, seed, octaves=3, wrap_x=False):
    tot, amp, norm = np.zeros((h, w), np.float32), 1.0, 0.0
    for o in range(octaves):
        tot += amp * value_noise(w, h, max(2, cell >> o), seed + o * 17, wrap_x); norm += amp; amp *= 0.5
    return tot / norm

def save(img, path):
    img.save(path)

def upscale(img, k=S):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)
