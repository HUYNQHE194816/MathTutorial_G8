"""Vẽ toàn bộ pixel art của màn hình chính "Màn Sương Lãng Quên" bằng code (Python + Pillow + numpy).

Chạy:   python3 gen_all.py                          -> ghi PNG vào ./out/assets (để xem/chỉnh)
        python3 gen_all.py --unity <đường dẫn>/Assets/Resources/MenuPixel    -> ghi thêm .bytes cho Unity
Xem thử bố cục:  python3 compose.py   (ra out/compose_full.png)
Lưới: 480x270 điểm ảnh, mỗi điểm = 4 điểm canvas (1920x1080).
Muốn đổi chữ (vd. "LỚP 8" -> "LỚP 9"): sửa chuỗi trong gen_all.py rồi chạy lại lệnh --unity.
"""
import os, sys
from common import *
import bg, sprites, ui, fx

HERE = os.path.dirname(os.path.abspath(__file__))
OUT = sys.argv[1] if len(sys.argv) > 1 and not sys.argv[1].startswith('--') else os.path.join(HERE, 'out', 'assets')
os.makedirs(OUT, exist_ok=True)
A = {}

A['bg'] = bg.make_background()
for i, f in enumerate(sprites.make_knight_frames()): A[f'knight_{i}'] = f
A['torch'] = sprites.torch_pillar()
for i in range(4): A[f'flame_{i}'] = sprites.flame_frame(i)
A['title'] = ui.title_sprite('MÀN SƯƠNG LÃNG QUÊN', 22, 2)
A['kicker'] = ui.small_sprite('KHOA HỌC TỰ NHIÊN  •  LỚP 8', ui.FD + 'DejaVuSans.ttf', 11, (160, 190, 226))
A['tagline'] = ui.small_sprite('Mỗi điều bạn biết là một ngọn đèn giữa màn sương.', ui.FD + 'DejaVuSerif-Italic.ttf', 12, (196, 214, 238))
A['panel'] = ui.panel()
for st, tag in (('normal', 'n'), ('hover', 'h'), ('down', 'd')):
    A[f'btn_start_{tag}'] = ui.button(26, 'primary', st)
    A[f'btn_menu_{tag}'] = ui.button(21, 'secondary', st)
    A[f'btn_wide_{tag}'] = ui.button(21, 'secondary', st, BW=150)
dark, hi = (50, 28, 10), (255, 232, 160)
A['lbl_start'] = ui.label_sprite('BẮT ĐẦU', 13, dark, hi, hi)
bone, sh = (234, 222, 192), (6, 8, 16)
A['lbl_progress'] = ui.label_sprite('TIẾN ĐỘ', 12, bone, sh)
A['lbl_settings'] = ui.label_sprite('CÀI ĐẶT', 12, bone, sh)
A['lbl_exit'] = ui.label_sprite('THOÁT', 12, bone, sh)
A['panel_small'] = ui.panel(170, 92)
A['lbl_sound_on'] = ui.label_sprite('ÂM THANH: BẬT', 10, bone, sh, tracking=1)
A['lbl_sound_off'] = ui.label_sprite('ÂM THANH: TẮT', 10, bone, sh, tracking=1)
A['lbl_close'] = ui.label_sprite('ĐÓNG', 13, dark, hi, hi)
A['mist_far'] = fx.mist(480, 56, 3, 1.0)
A['mist_high'] = fx.mist(480, 48, 9, 0.8, cell=48)
A['mist_near'] = fx.mist(480, 44, 17, 1.0, cell=24)
A['glow_warm'] = fx.glow(96, (255, 150, 60), 150, 2.2, 5)
A['glow_cold'] = fx.glow(128, (110, 170, 244), 120, 2.0, 5)

UNITY = None
if '--unity' in sys.argv:
    UNITY = sys.argv[sys.argv.index('--unity') + 1]; os.makedirs(UNITY, exist_ok=True)
for k, im in A.items():
    im.save(f'{OUT}/{k}.png')
    if UNITY:   # Unity nạp PNG qua TextAsset (.bytes) để giữ nguyên điểm ảnh, không bị nén/làm mờ
        import shutil; shutil.copyfile(f'{OUT}/{k}.png', f'{UNITY}/{k}.bytes')
print(len(A), 'assets ->', OUT)
for k in ('lbl_sound_on', 'lbl_close', 'panel_small', 'title', 'kicker', 'tagline', 'panel', 'knight_0', 'torch', 'flame_0', 'btn_start_n', 'btn_menu_n', 'lbl_start'):
    print(k, A[k].size)
