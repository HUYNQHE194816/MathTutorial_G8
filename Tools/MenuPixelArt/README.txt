PIXEL ART MÀN HÌNH CHÍNH - "Màn Sương Lãng Quên"
=================================================
Toàn bộ hình (nền rừng/trăng/tàn tích, hiệp sĩ, đài đuốc gargoyle, lửa, khung menu, nút, chữ tiêu đề)
được vẽ bằng code Python, KHÔNG phải ảnh ngoài. Thư mục này nằm NGOÀI Assets nên Unity bỏ qua.

Yêu cầu: Python 3 + Pillow + numpy   (pip install pillow numpy)

Vẽ lại và đẩy vào Unity (từ thư mục này):
    python3 gen_all.py --unity ../../Assets/Resources/MenuPixel
Xem thử bố cục (ra out/compose_full.png):
    python3 gen_all.py && python3 compose.py

Ai làm gì:
    bg.py       nền: trời, trăng, rừng cây khô, tàn tích, cỏ + gạch đá
    sprites.py  hiệp sĩ (4 khung thở), đài đuốc + gargoyle, ngọn lửa (4 khung)
    ui.py       chữ pixel (tiêu đề đồng khắc nổi, kicker, khẩu hiệu), nút, khung menu, rune
    fx.py       sương lặp được, quầng sáng bậc thang
    gen_all.py  gom tất cả; đổi chữ "LỚP 8" / khẩu hiệu ở đây

Bố cục + hoạt ảnh nằm trong Assets/Scripts/UI/MainMenuScreen.cs, nạp ảnh qua Assets/Scripts/UI/MenuPixelArt.cs.
Ảnh được lưu dạng .bytes (PNG) trong Assets/Resources/MenuPixel để Unity không nén/làm mờ điểm ảnh.
