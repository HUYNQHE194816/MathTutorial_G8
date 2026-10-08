from common import *

def mist(w, h, seed, amp=1.0, tone=(160, 200, 236), cell=24):
    """sương tileable theo chiều ngang, pixel art (dither)."""
    n = fbm(w, h, cell, seed, 3, wrap_x=True)
    ys = np.mgrid[0:h, 0:w][0].astype(np.float32)
    prof = np.exp(-((ys - h * 0.55) / (h * 0.30)) ** 2)
    lvl = np.clip((n - 0.30) * 2.2, 0, 1) * prof * amp
    bay = bayer_grid(w, h)
    v = lvl * 3
    q = np.floor(v) + ((v - np.floor(v)) > bay)
    a = np.clip(q / 3.0, 0, 1)
    arr = np.zeros((h, w, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = tone
    lt = a > 0.9
    arr[lt, 0], arr[lt, 1], arr[lt, 2] = 196, 226, 250
    arr[..., 3] = (a * 120).astype(np.uint8)
    return Image.fromarray(arr, 'RGBA')

def glow(size, color, amax, power=2.0, steps=5):
    """quầng sáng tròn dạng bậc thang + dither (không dùng gradient mượt)."""
    ys, xs = np.mgrid[0:size, 0:size].astype(np.float32)
    d = np.sqrt(((xs - size / 2 + .5) / (size / 2)) ** 2 + ((ys - size / 2 + .5) / (size / 2)) ** 2)
    lvl = np.clip(1 - d, 0, 1) ** power
    bay = bayer_grid(size, size)
    v = lvl * steps
    q = np.floor(v) + ((v - np.floor(v)) > bay)
    a = np.clip(q / steps, 0, 1) * amax
    arr = np.zeros((size, size, 4), np.uint8)
    arr[..., 0], arr[..., 1], arr[..., 2] = color
    arr[..., 3] = a.astype(np.uint8)
    return Image.fromarray(arr, 'RGBA')
