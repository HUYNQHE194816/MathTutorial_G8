using UnityEngine;

/// <summary>
/// Bảng token thiết kế DUY NHẤT của game "Màn Sương Lãng Quên" (dark fantasy ấm, "dark-cozy").
/// Mọi scene lấy màu từ đây, KHÔNG tự đặt Color riêng nữa. Muốn đổi phong cách: chỉ sửa file này.
/// Nguyên tắc: nền tối tím-đen, điểm nhấn lửa cam (đuốc), giấy da cho khối đọc nhiều chữ.
/// Màu theo môn chỉ là "rune accent" (viền nhỏ, thanh tiến độ, icon), không đổi cả theme theo môn.
/// </summary>
public static class GameTheme
{
    public const int CanvasW = 1920, CanvasH = 1080;

    // ---- Nền (tối -> sáng) ----
    public static readonly Color Ink    = Hex(0x0E0B14);   // đen tím, nền sâu nhất
    public static readonly Color Night  = Hex(0x181226);   // nền panel tối
    public static readonly Color Violet = Hex(0x2A2046);   // đỉnh gradient nền
    public static readonly Color Iron   = Hex(0x3A3452);   // khung sắt
    public static readonly Color IronHi = Hex(0x5A5278);   // khung sắt sáng / hover

    // ---- Giấy da (khối chữ dài, sổ tay, học bạ) ----
    public static readonly Color Parchment     = Hex(0xE8D9B5);
    public static readonly Color ParchmentDark = Hex(0xCDB98C);
    public static readonly Color PaperInk      = Hex(0x2B1D12);   // chữ trên giấy
    public static readonly Color PaperMuted    = Hex(0x4A3A28);

    // ---- Lửa (nút chính, tiêu đề, đuốc) ----
    public static readonly Color Ember     = Hex(0xFF9A3C);
    public static readonly Color EmberHi   = Hex(0xFFC36B);
    public static readonly Color EmberDeep = Hex(0xC4561A);

    // ---- Chữ trên nền tối + trạng thái ----
    public static readonly Color Bone  = Hex(0xEDE6D6);   // chữ chính
    public static readonly Color Fog   = Hex(0x8E86B8);   // chữ phụ / sương
    public static readonly Color Blood = Hex(0xC23B4E);   // nguy hiểm, sai
    public static readonly Color Moss  = Hex(0x5FB57A);   // đúng, thành công

    // ---- Màu theo môn (rune accent) ----
    public static readonly Color Ly   = Hex(0x4FC3F7);    // xanh điện
    public static readonly Color Hoa  = Hex(0xC77DFF);    // tím lan (đủ sáng trên nền tím tối)
    public static readonly Color Sinh = Hex(0x6BCB77);    // xanh lá

    public static Color Subject(SubjectId s)
    {
        switch (s) { case SubjectId.Ly: return Ly; case SubjectId.Hoa: return Hoa; default: return Sinh; }
    }

    public static Color Hex(int rgb)
        => new Color(((rgb >> 16) & 255) / 255f, ((rgb >> 8) & 255) / 255f, (rgb & 255) / 255f, 1f);

    public static Color WithAlpha(this Color c, float a) { c.a = a; return c; }
}
