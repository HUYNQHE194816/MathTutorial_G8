"""Ghép thử màn hình chính đúng bố cục sẽ dựng trong Unity (toạ độ canvas 1920x1080, gốc ở tâm)."""
import sys, os
from common import *
HERE = os.path.dirname(os.path.abspath(__file__))

A = sys.argv[1] if len(sys.argv) > 1 else os.path.join(HERE, 'out', 'assets')
ld = lambda n: Image.open(f'{A}/{n}.png').convert('RGBA')

def tl(img, cx, cy):
    return (int(round(W / 2 + cx / S - img.width / 2)), int(round(H / 2 - cy / S - img.height / 2)))

def put(base, img, cx, cy, flip=False, alpha=1.0):
    if flip: img = img.transpose(Image.FLIP_LEFT_RIGHT)
    if alpha < 1:
        a = img.split()[3].point(lambda v: int(v * alpha)); img = img.copy(); img.putalpha(a)
    base.alpha_composite(img, tl(img, cx, cy))

def put_tl(base, img, x, y, flip=False, alpha=1.0):
    if flip: img = img.transpose(Image.FLIP_LEFT_RIGHT)
    if alpha < 1:
        a = img.split()[3].point(lambda v: int(v * alpha)); img = img.copy(); img.putalpha(a)
    base.alpha_composite(img, (x, y))

scene = ld('bg')
put(scene, ld('glow_cold'), 0, 292, alpha=.55)
put(scene, ld('mist_high'), 0, 250, alpha=.5)
put(scene, ld('mist_far'), 0, -244, alpha=.9)
for tx in (100, 336):
    put_tl(scene, ld('glow_warm'), tx + 22 - 48, 148 - 48, alpha=.9)
    put_tl(scene, ld('glow_warm'), tx + 22 - 48, 148 - 48, alpha=.5)
put_tl(scene, ld('knight_0'), 32, 175)
put_tl(scene, ld('torch'), 100, 132)
put_tl(scene, ld('torch'), 336, 132, flip=True)
put_tl(scene, ld('flame_1'), 100 + 22 - 12, 130)
put_tl(scene, ld('flame_2'), 336 + 22 - 12, 130)
put(scene, ld('mist_near'), 0, -412, alpha=.55)

# UI
put(scene, ld('panel'), 0, -80)
for name, lbl, cy in (('btn_start_n', 'lbl_start', 82), ('btn_menu_n', 'lbl_progress', -30), ('btn_menu_n', 'lbl_settings', -130), ('btn_menu_n', 'lbl_exit', -230)):
    put(scene, ld(name), 0, cy)
    put(scene, ld(lbl), 0, cy)
put(scene, ld('title'), 0, 335)
put(scene, ld('kicker'), 0, 505, alpha=.9)
put(scene, ld('tagline'), 0, 235, alpha=.95)

# vignette thô để xem
ys, xs = np.mgrid[0:H, 0:W].astype(np.float32)
d = np.sqrt(((xs - W / 2) / (W / 2)) ** 2 + ((ys - H / 2) / (H / 2)) ** 2) / 1.4142
v = np.clip((d - 0.4) / 0.6, 0, 1) ** 2 * 0.6
arr = np.array(scene).astype(np.float32)
arr[..., :3] = arr[..., :3] * (1 - v[..., None]) + np.array([14, 11, 20]) * v[..., None]
scene = Image.fromarray(arr.astype(np.uint8), 'RGBA')
upscale(scene).convert('RGB').save(os.path.join(HERE, 'out', 'compose_full.png'))
scene.convert('RGB').save(os.path.join(HERE, 'out', 'compose_small.png'))
print('ok')
