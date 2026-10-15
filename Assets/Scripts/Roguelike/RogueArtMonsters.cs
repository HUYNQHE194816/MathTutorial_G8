using UnityEngine;

/// <summary>
/// Quái và boss vẽ bằng code. biome: 0 Phòng thí nghiệm hóa, 1 Phòng sinh học, 2 Phòng vật lý trên mây.
/// kind: 0 Xạ thủ, 1 Lao thẳng, 2 Pháp sư, 3 Robot (có người cưỡi), 4 Triệu hồi, 5 Quân nhỏ.
/// Mỗi quái có 3 khung: [0] bước A, [1] bước B, [2] tư thế ra đòn / báo hiệu.
/// </summary>
public static partial class RogueArt
{
    public static Sprite[] MonsterFrames(int biome, int kind) => GetN("m" + biome + "_" + kind, () =>
    {
        var r = new Sprite[3];
        for (int f = 0; f < 3; f++) r[f] = DrawMonster(biome, kind, f);
        return r;
    });

    /// <summary>Người cưỡi sau khi Robot bị phá (2 khung bước).</summary>
    public static Sprite[] RiderFrames(int biome) => GetN("rider" + biome, () => new[] { DrawRider(biome, 0), DrawRider(biome, 1) });

    static Sprite DrawMonster(int biome, int kind, int f)
    {
        var c = new Px(28, 28); int b = f == 1 ? 1 : 0; bool atk = f == 2; int leg = f == 0 ? 1 : f == 1 ? -1 : 0;
        Color32 eyeR = K(0xFF3B4E), eyeY = K(0xFFD84D);
        switch (biome * 10 + kind)
        {
            // ---------------- KHU 1: PHÒNG THÍ NGHIỆM HÓA ----------------
            case 0: // Nhân viên biến đổi cầm súng tiêm
                {
                    Color32 skin = K(0x84B866), skinL = K(0xB4E08C), skinD = K(0x4F7A3E), coat = K(0xD9DDE8), coatD = K(0x8A90A8);
                    c.Rect(10 + leg, 2, 3, 8, skinD); c.Rect(15 - leg, 2, 3, 8, skinD); c.Rect(9 + leg, 1, 5, 2, K(0x2B2D44)); c.Rect(14 - leg, 1, 5, 2, K(0x2B2D44));
                    c.Poly(V(8, 10 + b, 20, 10 + b, 19, 20 + b, 9, 20 + b), coat); c.Poly(V(8, 10 + b, 11, 10 + b, 11, 4, 7, 6), coatD); c.Rect(9, 17 + b, 10, 1, coatD); c.Rect(12, 11 + b, 1, 7, coatD);
                    c.Rect(15, 14 + b, 3, 2, K(0x7DE05A));
                    c.Blob(14, 23 + b, 5, 4.6f, skin, skinL, skinD); c.Rect(9, 23 + b, 11, 3, K(0x3B2F4A)); c.Rect(11, 23 + b, 3, 2, K(0xFFA14D)); c.Rect(16, 23 + b, 3, 2, K(0xFFA14D)); c.Set(12, 24 + b, White);
                    c.Rect(13, 20 + b, 4, 1, K(0x24351E));
                    c.Rect(17, 15 + b, 9, 3, K(0x7A808F)); c.Rect(25, 16 + b, 3, 1, K(0xBFF6FF)); c.Rect(20, 18 + b, 5, 1, K(0xBFF6FF)); c.Rect(21, 18 + b, 3, 1, atk ? K(0xC8FF9A) : K(0x7DE05A));
                    if (atk) { c.Rect(27, 15 + b, 1, 3, K(0xC8FF9A)); c.Set(26, 19 + b, K(0xC8FF9A)); }
                    break;
                }
            case 1: // Chuột biến dị lao thẳng
                {
                    Color32 fur = K(0x8A7F96), furL = K(0xB8AEC6), furD = K(0x4E4660), pink = K(0xE59AAE);
                    c.Line(3, 10 + b, 0, 6, pink, 1); c.Line(3, 10 + b, 1, 15, pink, 1); c.Set(0, 15, pink);
                    c.Ellipse(11, 10 + b, 9, 6, furD); c.Ellipse(11, 11 + b, 8.2f, 5.6f, fur); c.Ellipse(9, 13 + b, 5, 2.4f, furL);
                    for (int i = 0; i < 4; i++) c.Poly(V(5 + i * 3, 15 + b, 7 + i * 3, 15 + b, 6 + i * 3, 18 + b), K(0x6BD35A));
                    c.Rect(6 + leg, 2, 3, 5, furD); c.Rect(16 - leg, 2, 3, 5, furD); c.Rect(10 - leg, 3, 2, 4, furD); c.Rect(13 + leg, 3, 2, 4, furD);
                    c.Blob(21, 11 + b, 5, 4.2f, fur, furL, furD); c.Poly(V(24, 9 + b, 28, 9 + b, 25, 13 + b), fur); c.Rect(27, 9 + b, 1, 2, pink);
                    c.Blob(18, 16 + b, 2.4f, 2.4f, pink, K(0xFFD0DC), K(0xA85A74));
                    c.Rect(21, 8 + b, 2, atk ? 4 : 3, K(0xFFF3D0)); c.Rect(24, 8 + b, 2, atk ? 4 : 3, K(0xFFF3D0)); c.Set(22, 12 + b, eyeR); c.Set(23, 12 + b, eyeR);
                    c.Rect(21, 13 + b, 2, 2, eyeR); c.Set(21, 14 + b, White);
                    if (atk) { c.Rect(2, 8, 4, 1, K(0xFFFFFF)); c.Rect(0, 11, 5, 1, K(0xBFB8D0)); }
                    break;
                }
            case 2: // Bình thủy tinh biết đi
                {
                    Color32 glass = K(0xBFF6FF), liq = atk ? K(0xD58AFF) : K(0xA25CE8), liqD = K(0x6A33B0);
                    c.Rect(9 + leg, 1, 3, 5, K(0x4A4E66)); c.Rect(15 - leg, 1, 3, 5, K(0x4A4E66)); c.Rect(8 + leg, 1, 5, 2, K(0x2B2D44)); c.Rect(14 - leg, 1, 5, 2, K(0x2B2D44));
                    c.Poly(V(13.5f, 5 + b, 24, 5 + b, 17, 18 + b, 10, 18 + b), glass); c.Poly(V(4, 5 + b, 24, 5 + b, 20, 12 + b, 8, 12 + b), glass);
                    c.Poly(V(6, 6 + b, 22, 6 + b, 19, 13 + b, 9, 13 + b), liqD); c.Poly(V(7, 7 + b, 21, 7 + b, 18, 12 + b, 10, 12 + b), liq);
                    c.Rect(11, 18 + b, 6, 5, glass); c.Rect(12, 18 + b, 4, 4, liq); c.Rect(10, 22 + b, 8, 2, glass); c.Rect(11, 24 + b, 6, 3, K(0x9A6A3A)); c.Rect(11, 26 + b, 6, 1, K(0xC48A52));
                    c.Rect(9, 9 + b, 3, 3, K(0x1B1633)); c.Rect(16, 9 + b, 3, 3, K(0x1B1633)); c.Set(10, 10 + b, eyeY); c.Set(17, 10 + b, eyeY); c.Rect(12, 8 + b, 4, 1, K(0x1B1633));
                    c.Set(12, 14 + b + f, White); c.Set(16, 11 + b, White); c.Set(14, 16 + b - f, Lite(liq)); if (atk) { c.Set(13, 24, K(0xE0B8FF)); c.Set(15, 27, K(0xE0B8FF)); c.Set(11, 26, K(0xE0B8FF)); }
                    break;
                }
            case 3: // Robot dọn dẹp (có chuột biến dị cưỡi)
                {
                    Color32 hull = K(0x8E97B8), hullL = K(0xD3DAF0), hullD = K(0x4A5070);
                    c.Ellipse(14, 7, 12, 4.5f, hullD); c.Ellipse(14, 8, 11.2f, 4.2f, hull); c.Ellipse(11, 10, 7, 1.8f, hullL);
                    c.Rect(2, 6, 24, 2, hullD); c.Rect(10, 1, 4, 4, K(0x2B2D44)); c.Rect(18 - f, 1, 4, 4, K(0x2B2D44)); c.Rect(4 + f, 1, 4, 4, K(0x2B2D44));
                    c.Rect(6, 7, 16, 2, atk ? K(0xFFD84D) : K(0xFF3B4E)); c.Set(20, 7, White);
                    c.Rect(0, 9 + f, 4, 2, K(0xFFD84D)); c.Rect(24, 9 + (1 - f), 4, 2, K(0xFFD84D));   // chổi hai bên
                    c.Blob(14, 15 + b, 4.4f, 3.8f, K(0x8A7F96), K(0xB8AEC6), K(0x4E4660)); c.Poly(V(11, 17 + b, 13, 17 + b, 12, 21 + b), K(0xE59AAE)); c.Poly(V(15, 17 + b, 17, 17 + b, 16, 21 + b), K(0xE59AAE));
                    c.Rect(15, 14 + b, 2, 2, eyeR); c.Rect(17, 15 + b, 4, 2, K(0x7A808F)); c.Rect(20, 15 + b, 4, 1, atk ? K(0xC8FF9A) : K(0x555C7A));
                    break;
                }
            case 4: // Kỹ thuật viên triệu hồi chuột
                {
                    Color32 skin = K(0x84B866), skinD = K(0x4F7A3E), coat = K(0xD9DDE8), coatD = K(0x8A90A8);
                    c.Rect(10 + leg, 1, 3, 8, skinD); c.Rect(15 - leg, 1, 3, 8, skinD);
                    c.Poly(V(7, 8 + b, 21, 8 + b, 20, 20 + b, 8, 20 + b), coat); c.Rect(7, 8 + b, 3, 8, coatD); c.Rect(12, 10 + b, 1, 8, coatD);
                    c.Blob(14, 23 + b, 4.6f, 4.4f, skin, K(0xB4E08C), skinD); c.Rect(10, 21 + b, 9, 1, K(0x3B2F4A)); c.Set(12, 23 + b, eyeY); c.Set(16, 23 + b, eyeY);
                    c.Rect(9, 24 + b, 11, 3, K(0x6B4DA0)); c.Rect(10, 26 + b, 9, 1, K(0x9B7BD8));
                    int ar = atk ? 6 : 0; c.Rect(4, 14 + b + ar, 3, 6, skin); c.Rect(21, 14 + b + ar, 3, 6, skin);
                    c.Rect(1, 18 + b + ar, 7, 6, K(0x6B7392)); c.Rect(2, 19 + b + ar, 5, 4, atk ? K(0xE0B8FF) : K(0x3B2F4A)); c.Set(3, 20 + b + ar, eyeR); c.Set(5, 21 + b + ar, eyeR);
                    c.Rect(20, 17 + b + ar, 7, 7, K(0xBFF6FF)); c.Rect(21, 18 + b + ar, 5, 5, atk ? K(0xC8FF9A) : K(0x7DE05A));
                    break;
                }
            case 5: // Chuột con
                {
                    Color32 fur = K(0xA79CB4), furD = K(0x5C5470), pink = K(0xE59AAE);
                    c = new Px(16, 12);
                    c.Line(1, 4 + b, 0, 8, pink); c.Ellipse(7, 5 + b, 5, 3.4f, furD); c.Ellipse(7, 5.6f + b, 4.4f, 3f, fur); c.Rect(3 + leg, 1, 2, 3, furD); c.Rect(9 - leg, 1, 2, 3, furD);
                    c.Blob(11.5f, 6 + b, 3, 2.6f, fur, K(0xD0C6DC), furD); c.Rect(13, 5 + b, 3, 2, fur); c.Set(15, 6 + b, pink); c.Set(11, 8 + b, eyeR); c.Set(12, 8 + b, eyeR); c.Rect(10, 9 + b, 2, 2, pink);
                    c.Set(13, 3 + b, K(0xFFF3D0)); c.Set(14, 3 + b, K(0xFFF3D0));
                    break;
                }
            // ---------------- KHU 2: PHÒNG SINH HỌC ----------------
            case 10: // Thực vật ăn thịt (xạ thủ)
                {
                    Color32 g = K(0x4FB04A), gL = K(0x8FE06A), gD = K(0x276A32), red = K(0xD9455A), redD = K(0x8E2438);
                    c.Poly(V(8, 1, 14, 6, 11, 9, 4, 6), gD); c.Poly(V(18, 1, 14, 6, 17, 9, 24, 6), gD); c.Poly(V(9, 2, 14, 6, 12, 8), g); c.Poly(V(17, 2, 14, 6, 16, 8), g);
                    c.Rect(11, 1, 6, 3, K(0x6B4423)); c.Rect(12, 3, 4, 8, g); c.Rect(12, 3, 1, 8, gL); c.Rect(15, 3, 1, 8, gD); c.Rect(10, 6 + b, 2, 1, gL);
                    int mo = atk ? 5 : 2 + f; int sway = f == 1 ? 1 : 0;
                    c.Poly(V(10 + sway, 12 + b, 17 + sway, 12 + b, 18 + sway, 18 + b + mo / 2, 9 + sway, 19 + b), red);   // hàm dưới
                    c.Poly(V(9 + sway, 19 + b + mo, 20 + sway, 19 + b + mo, 18 + sway, 25 + b + mo, 10 + sway, 25 + b + mo), g); c.Rect(10 + sway, 24 + b + mo, 8, 1, gL);
                    c.Poly(V(14 + sway, 12 + b, 26 + sway, 14 + b, 26 + sway, 19 + b + mo, 14 + sway, 18 + b + mo), redD);
                    for (int i = 0; i < 4; i++) { c.Poly(V(15 + i * 3 + sway, 18 + b + mo, 17 + i * 3 + sway, 18 + b + mo, 16 + i * 3 + sway, 15 + b + mo / 2), White); c.Poly(V(15 + i * 3 + sway, 14 + b, 17 + i * 3 + sway, 14 + b, 16 + i * 3 + sway, 17 + b), White); }
                    c.Rect(12 + sway, 21 + b + mo, 3, 3, eyeY); c.Set(13 + sway, 22 + b + mo, K(0x1B1633));
                    if (atk) c.Ellipse(26, 17 + b, 2, 2, K(0xC8FF9A));
                    break;
                }
            case 11: // Bọ giáp khổng lồ
                {
                    Color32 sh = K(0x6B3F8A), shL = K(0xB07BE0), shD = K(0x3A1F54), leg6 = K(0x2E1E3A);
                    for (int i = 0; i < 3; i++) { int ox = 7 + i * 5; int w = (i + f) % 2 == 0 ? 2 : -1; c.Line(ox, 9, ox + w - 2, 3, leg6, 1); c.Line(ox, 9, ox + w + 3, 3, leg6, 1); c.Set(ox + w - 2, 2, leg6); c.Set(ox + w + 3, 2, leg6); }
                    c.Ellipse(13, 10 + b, 11, 7, shD); c.Ellipse(12.5f, 11 + b, 10.2f, 6.4f, sh); c.Ellipse(9, 14 + b, 6, 2.6f, shL); c.Rect(13, 5 + b, 1, 12, shD); c.Set(8, 15 + b, White); c.Set(7, 14 + b, White);
                    c.Blob(23, 11 + b, 3.6f, 3.4f, K(0x4A2A66), K(0x8A5CB0), shD); c.Set(24, 13 + b, eyeR); c.Set(25, 12 + b, eyeR);
                    int hr = atk ? -2 : 0; c.Poly(V(24, 13 + b, 28, 17 + b + hr, 27, 18 + b + hr, 22, 14 + b), K(0xE8D8A8)); c.Poly(V(25, 9 + b, 28, 5 + b - hr, 27, 4 + b - hr, 22, 8 + b), K(0xC8B888));
                    if (atk) { c.Rect(1, 14, 6, 1, K(0xFFFFFF)); c.Rect(0, 9, 5, 1, K(0xD0C0E0)); }
                    break;
                }
            case 12: // Nấm bào tử
                {
                    Color32 cap = K(0xC4488E), capL = K(0xF080C0), capD = K(0x7A2458), stem = K(0xEFE6D2), stemD = K(0xB8AA90);
                    c.Rect(10, 2, 8, 12, stem); c.Rect(10, 2, 2, 12, K(0xFFF8E8)); c.Rect(16, 2, 2, 12, stemD); c.Rect(8 + leg, 1, 4, 3, stemD); c.Rect(16 - leg, 1, 4, 3, stemD);
                    c.Rect(11, 9 + b, 2, 2, K(0x1B1633)); c.Rect(15, 9 + b, 2, 2, K(0x1B1633)); c.Set(11, 10 + b, eyeY); c.Set(15, 10 + b, eyeY); c.Rect(13, 6 + b, 2, 1, K(0x1B1633));
                    c.Ellipse(14, 17 + b, 13, 7.5f, capD); c.Ellipse(13.5f, 18 + b, 12.2f, 7f, atk ? K(0xE060B0) : cap); c.Ellipse(9, 21 + b, 6, 2.6f, capL);
                    c.Ellipse(8, 17 + b, 2, 1.6f, K(0xFFE6F3)); c.Ellipse(18, 21 + b, 2.2f, 1.7f, K(0xFFE6F3)); c.Ellipse(21, 15 + b, 1.6f, 1.3f, K(0xFFE6F3));
                    c.Rect(2, 11 + b, 3, 3, stemD); c.Rect(23, 11 + b, 3, 3, stemD);
                    c.Set(5, 25 + f, K(0xC8FF9A)); c.Set(21, 26 - f, K(0xC8FF9A)); c.Set(14, 27, K(0xC8FF9A)); if (atk) { c.Set(2, 24, K(0xC8FF9A)); c.Set(26, 23, K(0xC8FF9A)); c.Set(9, 26, K(0xC8FF9A)); }
                    break;
                }
            case 13: // Chậu cây cơ khí (kỹ thuật viên cưỡi)
                {
                    Color32 pot = K(0xB5693C), potL = K(0xE09A66), potD = K(0x6E3B1F), g = K(0x4FB04A);
                    c.Rect(6 + leg, 1, 4, 6, K(0x4A4E66)); c.Rect(18 - leg, 1, 4, 6, K(0x4A4E66)); c.Rect(5 + leg, 1, 6, 2, K(0x2B2D44)); c.Rect(17 - leg, 1, 6, 2, K(0x2B2D44));
                    c.Poly(V(4, 16 + b, 24, 16 + b, 21, 6 + b, 7, 6 + b), pot); c.Poly(V(4, 16 + b, 8, 16 + b, 9, 6 + b, 7, 6 + b), potL); c.Poly(V(20, 16 + b, 24, 16 + b, 21, 6 + b, 18, 6 + b), potD);
                    c.Rect(3, 15 + b, 22, 3, potL); c.Rect(3, 15 + b, 22, 1, potD); c.Rect(10, 9 + b, 8, 2, atk ? K(0xFFD84D) : K(0xFF3B4E));
                    c.Line(5, 18 + b, 2, 22 + b, g); c.Line(23, 18 + b, 27, 21 + b, g); c.Poly(V(2, 22 + b, 5, 21 + b, 4, 24 + b), g);
                    c.Blob(14, 22 + b, 3.6f, 3.4f, K(0xE7B999), K(0xFFE0C8), K(0x9A6A4A)); c.Rect(10, 24 + b, 9, 3, K(0x4DA0C8)); c.Rect(11, 26 + b, 7, 1, K(0x8EE0FF)); c.Rect(15, 22 + b, 2, 1, K(0x1B1633));
                    c.Rect(18, 19 + b, 8, 2, K(0x7A808F)); c.Rect(25, 19 + b, 2, 2, atk ? K(0xC8FF9A) : K(0x555C7A));
                    break;
                }
            case 14: // Cây mẹ (triệu hồi)
                {
                    Color32 g = K(0x4FB04A), gD = K(0x276A32), pet = K(0xF078B0), petL = K(0xFFB8DA), petD = K(0xA83A78);
                    c.Rect(12, 1, 4, 14, gD); c.Rect(12, 1, 2, 14, g); c.Poly(V(12, 5, 5, 8, 4, 12, 12, 9), g); c.Poly(V(16, 7, 23, 10, 24, 14, 16, 11), g);
                    c.Line(11, 2, 5, 1, gD, 2); c.Line(17, 2, 23, 1, gD, 2);
                    int o = atk ? 3 : f; int pc = 6;
                    for (int i = 0; i < 6; i++) { float a = Mathf.PI * (.12f + i * .15f); float ox = 14 + Mathf.Cos(a) * (9 + o), oy = 18 + b + Mathf.Sin(a) * (7 + o); c.Poly(V(14, 18 + b, ox - 3, oy - 2, ox, oy + 3, ox + 3, oy - 2), i % 2 == 0 ? pet : petD); c.Set((int)ox, (int)oy, petL); }
                    c.Blob(14, 18 + b, 5, 4.8f, K(0xFFD84D), K(0xFFF0A0), K(0xC89A1E)); c.Rect(11, 18 + b, 2, 2, K(0x1B1633)); c.Rect(15, 18 + b, 2, 2, K(0x1B1633)); c.Set(11, 19 + b, eyeR); c.Set(15, 19 + b, eyeR); c.Rect(12, 15 + b, 4, 1, K(0x1B1633));
                    if (atk) { c.Set(4, 26, petL); c.Set(24, 25, petL); c.Set(14, 27, petL); }
                    break;
                }
            case 15: // Mầm sâu nhỏ
                {
                    c = new Px(16, 12); Color32 g = K(0x8FE06A), gD = K(0x3A8A3A);
                    c.Ellipse(6, 4 + b, 4.4f, 3.2f, gD); c.Ellipse(6, 4.6f + b, 3.8f, 2.8f, g); c.Ellipse(11, 5 + b, 3.2f, 3f, K(0xB6F08A)); c.Set(12, 6 + b, K(0x1B1633)); c.Set(13, 5 + b, eyeR);
                    c.Poly(V(6, 7 + b, 4, 11, 9, 8 + b), K(0x5FD35A)); c.Poly(V(8, 7 + b, 12, 11, 10, 8 + b), K(0x5FD35A)); c.Rect(2 + leg, 0, 2, 2, gD); c.Rect(8 - leg, 0, 2, 2, gD);
                    break;
                }
            // ---------------- KHU 3: PHÒNG VẬT LÝ TRÊN MÂY ----------------
            case 20: // Drone xạ thủ
                {
                    Color32 hull = K(0x8E97B8), hullL = K(0xD3DAF0), hullD = K(0x4A5070), cy = K(0x4DE0FF);
                    int rot = f % 2 == 0 ? 0 : 1;
                    c.Ellipse(14, 14 + b, 7, 5.4f, hullD); c.Ellipse(14, 15 + b, 6.4f, 5f, hull); c.Ellipse(12, 17 + b, 3.6f, 1.6f, hullL);
                    c.Blob(17, 14 + b, 2.8f, 2.8f, K(0x1B1633), K(0x4A4E66), K(0x0C0A14)); c.Rect(16, 14 + b, 2, 2, atk ? White : eyeR);
                    c.Rect(14, 2, 1, 4, hullD); c.Rect(10, 2 + b, 8, 2, hullD); c.Rect(10, 3 + b, 8, 1, cy);
                    foreach (var p in new[] { new Vector2(3, 21), new Vector2(23, 21), new Vector2(3, 9), new Vector2(23, 9) })
                    { c.Line(14, 14 + b, p.x + 2, p.y + b, hullD, 1); c.Rect((int)p.x - 1 + rot, (int)p.y + b, 6 - 2 * rot, 1, K(0xDCE2F0)); c.Rect((int)p.x, (int)p.y - 1 + b, 4, 1, hullD); }
                    c.Rect(19, 12 + b, 7, 3, K(0x555C7A)); c.Rect(25, 13 + b, 3, 1, atk ? cy : K(0x2B2D44));
                    break;
                }
            case 21: // Chó máy
                {
                    Color32 hull = K(0x8E97B8), hullL = K(0xD3DAF0), hullD = K(0x4A5070), cy = K(0x4DE0FF);
                    c.Rect(5 + leg, 1, 3, 8, hullD); c.Rect(9 - leg, 1, 3, 8, hullD); c.Rect(15 + leg, 1, 3, 8, hullD); c.Rect(19 - leg, 1, 3, 8, hullD);
                    c.Rect(4 + leg, 1, 5, 2, K(0x2B2D44)); c.Rect(14 + leg, 1, 5, 2, K(0x2B2D44));
                    c.Slab(3, 8 + b, 20, 8, hull, hullL, hullD); c.Rect(6, 11 + b, 12, 1, hullD); c.Rect(4, 10 + b, 3, 3, cy);
                    c.Poly(V(14, 16 + b, 18, 16 + b, 16, atk ? 24 : 20), K(0xC9D2F0)); c.Poly(V(8, 16 + b, 11, 16 + b, 9, atk ? 22 : 19), K(0xC9D2F0));
                    c.Blob(23, 13 + b, 4, 3.6f, hull, hullL, hullD); c.Poly(V(25, 11 + b, 28, 11 + b, 26, 14 + b), hullD); c.Rect(22, 14 + b, 3, 2, atk ? White : cy); c.Rect(26, 10 + b, 2, 1, hullL);
                    c.Rect(0, 11 + b, 4, 2, hullD); c.Set(0, 13 + b + f, cy);
                    break;
                }
            case 22: // Người máy Tesla
                {
                    Color32 hull = K(0x8E97B8), hullL = K(0xD3DAF0), hullD = K(0x4A5070), cy = K(0x4DE0FF), cu = K(0xC87A3A);
                    c.Rect(10 + leg, 1, 3, 8, hullD); c.Rect(15 - leg, 1, 3, 8, hullD); c.Rect(9 + leg, 1, 5, 2, K(0x2B2D44)); c.Rect(14 - leg, 1, 5, 2, K(0x2B2D44));
                    c.Slab(8, 9 + b, 12, 9, hull, hullL, hullD); c.Rect(12, 11 + b, 4, 4, cy); c.Rect(13, 12 + b, 2, 2, White); c.Rect(5, 11 + b, 3, 6, hullD); c.Rect(20, 11 + b, 3, 6, hullD);
                    c.Rect(12, 18 + b, 4, 3, hullD); c.Rect(11, 21 + b, 6, 1, cu);
                    for (int i = 0; i < 4; i++) c.Rect(12, 22 + b + i, 4, 1, i % 2 == 0 ? cu : K(0xE8A060)); c.Ellipse(14, 26 + b, 3, 2, atk ? White : cy);
                    c.Set(10, 25 + b, atk ? White : cy); c.Set(18, 24 + b, atk ? White : cy); c.Set(8, 26 - f, cy); c.Set(20, 27, cy); if (atk) { c.Line(14, 27, 6, 22, cy); c.Line(14, 27, 22, 23, cy); }
                    break;
                }
            case 23: // Robot hai chân (trợ lý lái)
                {
                    Color32 hull = K(0x8E97B8), hullL = K(0xD3DAF0), hullD = K(0x4A5070), cy = K(0x4DE0FF);
                    c.Rect(6 + leg, 1, 5, 9, hullD); c.Rect(17 - leg, 1, 5, 9, hullD); c.Rect(5 + leg, 1, 7, 3, K(0x2B2D44)); c.Rect(16 - leg, 1, 7, 3, K(0x2B2D44));
                    c.Slab(4, 10 + b, 20, 10, hull, hullL, hullD); c.Rect(8, 12 + b, 12, 2, atk ? K(0xFFD84D) : K(0xFF3B4E)); c.Rect(4, 10 + b, 20, 2, hullD);
                    c.Rect(1, 12 + b, 4, 6, hullD); c.Rect(23, 12 + b, 4, 6, hullD); c.Rect(0, 18 + b, 5, 2, cy); c.Rect(23, 18 + b, 5, 2, cy);
                    c.Rect(9, 20 + b, 10, 3, K(0x5A6288)); c.Blob(14, 25 + b, 3.4f, 3f, K(0xE7B999), K(0xFFE0C8), K(0x9A6A4A)); c.Rect(11, 26 + b, 6, 2, cy); c.Set(15, 25 + b, K(0x1B1633));
                    break;
                }
            case 24: // Trợ lý mang hộp drone
                {
                    Color32 skin = K(0xE7B999), skinD = K(0x9A6A4A), coat = K(0xDCE2F0), coatD = K(0x8A90A8), cy = K(0x4DE0FF);
                    c.Rect(10 + leg, 1, 3, 8, K(0x3B405C)); c.Rect(15 - leg, 1, 3, 8, K(0x3B405C)); c.Rect(9 + leg, 1, 5, 2, K(0x23253A)); c.Rect(14 - leg, 1, 5, 2, K(0x23253A));
                    c.Poly(V(8, 9 + b, 20, 9 + b, 19, 20 + b, 9, 20 + b), coat); c.Rect(8, 9 + b, 3, 8, coatD); c.Rect(13, 10 + b, 2, 9, cy);
                    c.Blob(14, 23 + b, 4.4f, 4.2f, skin, K(0xFFE0C8), skinD); c.Rect(10, 24 + b, 9, 3, K(0x3B405C)); c.Rect(12, 23 + b, 5, 1, cy); c.Rect(9, 22 + b, 2, 3, K(0x555C7A)); c.Rect(9, 21 + b, 4, 1, K(0x555C7A));
                    int ar = atk ? 3 : 0; c.Rect(0, 11 + b + ar, 8, 6, K(0x4A5070)); c.Rect(1, 15 + b + ar, 6, 1, atk ? cy : K(0x2B2D44)); c.Rect(2, 12 + b + ar, 4, 2, K(0x2B2D44));
                    c.Rect(6, 15 + b, 3, 2, skin); if (atk) { c.Set(2, 20, cy); c.Set(5, 22, cy); c.Set(3, 24, White); }
                    break;
                }
            default: // Mini drone
                {
                    c = new Px(16, 12); Color32 hull = K(0x8E97B8), hullD = K(0x4A5070), cy = K(0x4DE0FF);
                    c.Ellipse(8, 6 + b, 4, 3.2f, hullD); c.Ellipse(8, 6.6f + b, 3.4f, 2.8f, hull); c.Rect(9, 6 + b, 2, 2, eyeR); c.Rect(2, 10 + b, 5, 1, K(0xDCE2F0)); c.Rect(10, 10 + b, 5, 1, K(0xDCE2F0)); c.Line(6, 8 + b, 4, 10 + b, hullD); c.Line(10, 8 + b, 12, 10 + b, hullD);
                    c.Set(7, 3 + b, cy); c.Set(8, 3 + b, cy);
                    break;
                }
        }
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 1f / c.h));
    }

    static Sprite DrawRider(int biome, int f)
    {
        var c = new Px(18, 22); int leg = f == 0 ? 1 : -1; int b = f;
        Color32 eyeR = K(0xFF3B4E);
        if (biome == 0)
        {   // chuột biến dị đứng bằng hai chân
            Color32 fur = K(0x8A7F96), furL = K(0xB8AEC6), furD = K(0x4E4660), pink = K(0xE59AAE);
            c.Line(4, 4, 0, 2, pink); c.Rect(5 + leg, 1, 3, 5, furD); c.Rect(10 - leg, 1, 3, 5, furD); c.Blob(9, 10 + b, 5, 5, fur, furL, furD); c.Blob(10, 16 + b, 4, 3.6f, fur, furL, furD);
            c.Poly(V(6, 18 + b, 9, 18 + b, 7, 22), pink); c.Poly(V(11, 18 + b, 14, 18 + b, 13, 22), pink); c.Rect(11, 15 + b, 2, 2, eyeR); c.Rect(13, 14 + b, 3, 2, pink); c.Rect(13, 12 + b, 2, 2, K(0xFFF3D0));
            c.Rect(12, 8 + b, 6, 2, K(0x7A808F));
        }
        else
        {
            bool sky = biome == 2; Color32 skin = K(0xE7B999), skinD = K(0x9A6A4A), coat = sky ? K(0xDCE2F0) : K(0x4FB04A), coatD = sky ? K(0x8A90A8) : K(0x276A32), hat = sky ? K(0x4DE0FF) : K(0x4DA0C8);
            c.Rect(6 + leg, 1, 3, 6, K(0x3B405C)); c.Rect(10 - leg, 1, 3, 6, K(0x3B405C)); c.Rect(5 + leg, 1, 5, 2, K(0x23253A)); c.Rect(9 - leg, 1, 5, 2, K(0x23253A));
            c.Poly(V(4, 6, 15, 6, 14, 15 + b, 5, 15 + b), coat); c.Rect(4, 6, 3, 8, coatD); c.Blob(10, 18 + b, 3.8f, 3.6f, skin, K(0xFFE0C8), skinD);
            c.Rect(6, 19 + b, 9, 3, hat); c.Rect(7, 21 + b, 7, 1, Lite(hat)); c.Rect(11, 17 + b, 2, 1, K(0x1B1633)); c.Rect(13, 11 + b, 5, 2, K(0x7A808F));
        }
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 1f / c.h));
    }

    // ======================================================================== BOSS 1: CHUỘT BẠCH KHỔNG LỒ (nhìn thẳng)
    // pose: 0,1 đứng thở, 2 há miệng bắn, 3 giơ tay dậm xuống
    public static Sprite[] RatBoss => GetN("boss_rat", () => new[] { DrawRat(0), DrawRat(1), DrawRat(2), DrawRat(3) });

    static Sprite DrawRat(int pose)
    {
        var c = new Px(72, 72); int b = pose == 1 ? 1 : 0;
        Color32 fur = K(0xE9E4F0), furL = K(0xFFFFFF), furD = K(0xA79FBC), furDD = K(0x6F6788), pink = K(0xEE9DB4), pinkD = K(0xB5607E), glow = K(0x7DFF5A), glowD = K(0x2F9A2E), red = K(0xFF2F48);
        // đuôi
        c.Line(52, 12, 66, 8, pink, 2); c.Line(66, 8, 69, 18, pink, 2); c.Line(66, 8, 69, 18, pinkD, 1);
        // thân
        c.Ellipse(36, 26 + b, 22, 20, furDD); c.Ellipse(36, 27 + b, 21, 19, furD); c.Ellipse(35, 29 + b, 19.5f, 17.5f, fur); c.Ellipse(30, 36 + b, 11, 7, furL);
        c.Ellipse(36, 22 + b, 11, 12, K(0xF4D2DC)); c.Ellipse(36, 22 + b, 9, 10, K(0xFBE4EA));
        // vết khâu & gân phát sáng của vật thí nghiệm
        c.Line(30, 14 + b, 40, 30 + b, furDD, 1); for (int i = 0; i < 5; i++) { int sx = 31 + i * 2; c.Line(sx - 1, 17 + i * 3 + b, sx + 2, 17 + i * 3 + b, furDD); }
        c.Line(18, 22 + b, 24, 34 + b, glowD, 1); c.Line(18, 22 + b, 24, 34 + b, glow, 1); c.Line(50, 20 + b, 46, 32 + b, glow, 1); c.Set(51, 20 + b, glowD);
        // chân
        c.Blob(22, 7, 7, 5.6f, fur, furL, furD); c.Blob(50, 7, 7, 5.6f, fur, furL, furD); for (int i = 0; i < 3; i++) { c.Rect(17 + i * 3, 1, 2, 3, pink); c.Rect(45 + i * 3, 1, 2, 3, pink); }
        // tay: cầm ống tiêm khổng lồ (pose 3 giơ cao)
        int ay = pose == 3 ? 24 : 0;
        c.Blob(12, 24 + ay + b, 6, 7, fur, furL, furD); c.Blob(60, 24 + ay + b, 6, 7, fur, furL, furD);
        for (int i = 0; i < 3; i++) { c.Rect(8 + i * 3, 16 + ay + b, 2, 3, pink); c.Rect(56 + i * 3, 16 + ay + b, 2, 3, pink); }
        if (pose != 3) { c.Rect(3, 8, 5, 24, K(0xBFF6FF)); c.Rect(4, 10, 3, 14, glow); c.Rect(4, 10, 1, 14, K(0xD9FFB8)); c.Rect(2, 32, 7, 2, K(0x7A808F)); c.Rect(5, 33, 1, 7, K(0xDCE2F0)); c.Poly(V(3, 8, 8, 8, 5.5f, 2), K(0xDCE2F0)); }
        // đầu
        c.Ellipse(36, 50 + b, 17, 15, furDD); c.Ellipse(36, 51 + b, 16, 14, furD); c.Ellipse(36, 52 + b, 15, 13, fur); c.Ellipse(31, 58 + b, 8, 4.4f, furL);
        c.Blob(18, 64 + b, 8, 8, fur, furL, furD); c.Blob(54, 64 + b, 8, 8, fur, furL, furD); c.Ellipse(18, 64 + b, 4.6f, 5, pink); c.Ellipse(54, 64 + b, 4.6f, 5, pink); c.Ellipse(18, 65 + b, 2.6f, 3, K(0xFFC0D0)); c.Ellipse(54, 65 + b, 2.6f, 3, K(0xFFC0D0));
        c.Ellipse(36, 44 + b, 7, 5, K(0xF4D2DC));
        // mắt đỏ rực, một bên có vết sẹo
        c.Ellipse(28, 52 + b, 4, 4.4f, K(0x3A0F1B)); c.Ellipse(44, 52 + b, 4, 4.4f, K(0x3A0F1B)); c.Ellipse(28, 52 + b, 3, 3.4f, red); c.Ellipse(44, 52 + b, 3, 3.4f, red); c.Rect(27, 52 + b, 2, 3, K(0xFFD0D8)); c.Rect(43, 52 + b, 2, 3, K(0xFFD0D8)); c.Set(29, 54 + b, White); c.Set(45, 54 + b, White);
        c.Line(23, 58 + b, 33, 47 + b, furDD, 1); c.Line(25, 56 + b, 28, 58 + b, furDD);
        // mũi, râu, răng
        c.Ellipse(36, 45 + b, 3, 2.2f, pinkD); c.Ellipse(36, 46 + b, 2.2f, 1.5f, pink);
        for (int i = 0; i < 3; i++) { c.Line(25, 44 + b - i * 2, 12 - i, 46 + b - i * 3, furDD); c.Line(47, 44 + b - i * 2, 60 + i, 46 + b - i * 3, furDD); }
        int mo = pose == 2 ? 8 : 0;
        if (mo > 0) { c.Ellipse(36, 36 + b, 8, 5 + mo / 2, K(0x2A0A14)); c.Ellipse(36, 34 + b, 5, 3, glow); }
        c.Rect(32, 38 + b - mo, 3, 6 + 0, K(0xFFF8E0)); c.Rect(37, 38 + b - mo, 3, 6, K(0xFFF8E0)); c.Rect(32, 38 + b - mo, 3, 1, K(0xD8D0B0)); c.Rect(37, 38 + b - mo, 3, 1, K(0xD8D0B0));
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 1f / 72f));
    }

    // ======================================================================== BOSS 2: CÁO CHÍN ĐUÔI
    // Thân (3 khung: đứng 0,1 / há miệng 2) + đuôi riêng (xoay quanh gốc bằng code).
    public static Sprite[] FoxBody => GetN("boss_fox", () => new[] { DrawFox(0), DrawFox(1), DrawFox(2) });

    static Sprite DrawFox(int pose)
    {
        var c = new Px(56, 64); int b = pose == 1 ? 1 : 0;
        Color32 fur = K(0xF4F7FF), furL = K(0xFFFFFF), furD = K(0xAEB8D8), furDD = K(0x6C769C), cy = K(0x5FF0E0), mark = K(0xE23B5C), eye = K(0xFFD84D);
        // thân ngồi
        c.Ellipse(28, 18 + b, 16, 17, furDD); c.Ellipse(28, 19 + b, 15, 16, furD); c.Ellipse(27, 21 + b, 14, 14.4f, fur); c.Ellipse(22, 28 + b, 7, 5, furL);
        c.Ellipse(28, 14 + b, 8, 10, K(0xE9EEFF)); c.Rect(26, 6 + b, 3, 12, cy); c.Rect(27, 8 + b, 1, 8, White);   // chuỗi ngọc/dấu hiệu phát sáng trước ngực
        // chân trước
        c.Rect(15, 1, 6, 13, furD); c.Rect(16, 1, 4, 13, fur); c.Rect(35, 1, 6, 13, furD); c.Rect(36, 1, 4, 13, fur);
        c.Rect(14, 1, 8, 3, K(0x3A4064)); c.Rect(34, 1, 8, 3, K(0x3A4064));
        // đầu
        c.Ellipse(28, 42 + b, 15, 12, furDD); c.Ellipse(28, 43 + b, 14, 11, furD); c.Ellipse(28, 44 + b, 13, 10, fur); c.Ellipse(24, 49 + b, 6, 3.4f, furL);
        c.Poly(V(14, 46 + b, 11, 62, 22, 52 + b), furDD); c.Poly(V(42, 46 + b, 45, 62, 34, 52 + b), furDD);
        c.Poly(V(15, 47 + b, 13, 60, 21, 51 + b), fur); c.Poly(V(41, 47 + b, 43, 60, 35, 51 + b), fur); c.Poly(V(16, 49 + b, 15, 57, 20, 51 + b), K(0xC23B5C)); c.Poly(V(40, 49 + b, 41, 57, 36, 51 + b), K(0xC23B5C));
        c.Poly(V(20, 36 + b, 36, 36 + b, 28, 28 + b), furL);   // má
        // vằn đỏ kitsune
        c.Poly(V(14, 41 + b, 21, 44 + b, 14, 46 + b), mark); c.Poly(V(42, 41 + b, 35, 44 + b, 42, 46 + b), mark); c.Rect(27, 51 + b, 2, 4, mark);
        // mắt xếch
        c.Poly(V(18, 42 + b, 25, 44 + b, 25, 46 + b, 18, 46 + b), K(0x1B1633)); c.Poly(V(38, 42 + b, 31, 44 + b, 31, 46 + b, 38, 46 + b), K(0x1B1633));
        c.Rect(21, 43 + b, 4, 2, eye); c.Rect(31, 43 + b, 4, 2, eye); c.Set(22, 44 + b, K(0x1B1633)); c.Set(33, 44 + b, K(0x1B1633));
        // mũi miệng
        c.Rect(27, 38 + b, 3, 2, K(0x1B1633)); c.Set(28, 40 + b, K(0x1B1633));
        if (pose == 2) { c.Ellipse(28, 33 + b, 5, 3.4f, K(0x2A0A24)); c.Ellipse(28, 33 + b, 3, 2, cy); c.Poly(V(24, 36 + b, 26, 36 + b, 25, 33 + b), White); c.Poly(V(31, 36 + b, 33, 36 + b, 32, 33 + b), White); }
        else { c.Line(24, 36 + b, 28, 35 + b, K(0x1B1633)); c.Line(32, 36 + b, 28, 35 + b, K(0x1B1633)); }
        // trán: ấn xanh
        c.Poly(V(28, 55 + b, 31, 51 + b, 28, 48 + b, 25, 51 + b), cy);
        c.Outline(Ink);
        return c.ToSprite(new Vector2(.5f, 1f / 64f));
    }

    /// <summary>Một cái đuôi (gốc ở dưới, ngọn lên trên, đầu đuôi phát sáng) — xoay quanh gốc khi gắn vào Cáo.</summary>
    public static Sprite FoxTail => Get("foxtail", () =>
    {
        var c = new Px(18, 52); Color32 fur = K(0xF4F7FF), furD = K(0xAEB8D8), cy = K(0x5FF0E0), cyD = K(0x20A8B8);
        for (int i = 0; i < 50; i++)
        {
            float t = i / 49f; float x = 9 + Mathf.Sin(t * 3.4f) * 3.2f; float r = Mathf.Lerp(2.6f, 6.2f, Mathf.Sin(Mathf.Min(1f, t * 1.15f) * Mathf.PI * .5f)) * (t > .78f ? Mathf.Lerp(1f, .2f, (t - .78f) / .22f) : 1f);
            Color32 col = t > .6f ? Mix(fur, cy, (t - .6f) / .4f) : fur;
            c.Ellipse(x, 1 + i, r, 1.4f, Shade(col, .28f)); c.Ellipse(x - .5f, 1 + i, Mathf.Max(.6f, r - 1.1f), 1.4f, col);
        }
        c.Ellipse(9, 49, 2.6f, 2.6f, White); c.Set(9, 50, cy);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 1f / 52f));
    });

    // ======================================================================== BOSS 3: NHÀ KHOA HỌC ĐIÊN
    // Dạng người trên bệ bay: 0,1 lơ lửng / 2 vung tay. Dạng Mech: thân, tay, lõi riêng.
    public static Sprite[] Scientist => GetN("boss_sci", () => new[] { DrawScientist(0), DrawScientist(1), DrawScientist(2) });

    static Sprite DrawScientist(int pose)
    {
        var c = new Px(48, 60); int b = pose == 1 ? 1 : 0;
        Color32 skin = K(0xE7B999), skinL = K(0xFFE0C8), skinD = K(0x9A6A4A), coat = K(0xE6EAF6), coatD = K(0x8A90A8), coatDD = K(0x555C7A), hair = K(0xF2F2FA), hairD = K(0xA9AEC8), cy = K(0x4DE0FF), cyD = K(0x1D8CA8), metal = K(0x8E97B8);
        // bệ bay
        c.Ellipse(24, 5, 20, 5, K(0x2B2D44)); c.Ellipse(24, 6, 19, 4.2f, metal); c.Ellipse(20, 8, 12, 1.8f, K(0xD3DAF0)); c.Ellipse(24, 3, 12, 2, cy); c.Ellipse(24, 3, 7, 1, White);
        for (int i = 0; i < 3; i++) c.Rect(10 + i * 11, 0, 3, 2 + (b + i) % 2, cy);
        // áo choàng phòng thí nghiệm
        c.Poly(V(11, 10, 37, 10, 34, 36 + b, 14, 36 + b), coat); c.Poly(V(11, 10, 17, 10, 17, 34, 14, 36 + b), coatD); c.Poly(V(31, 10, 37, 10, 34, 36 + b, 31, 36 + b), coatD);
        c.Rect(23, 12, 2, 24, coatD); c.Rect(14, 22, 7, 5, coatD); c.Rect(15, 25, 5, 1, K(0xD9453C)); c.Rect(28, 22, 6, 5, coatD); c.Rect(29, 24, 2, 4, cy); c.Rect(10, 8, 28, 2, coatDD);
        // tay: một tay thường, một tay máy
        int ar = pose == 2 ? 10 : 0;
        c.Rect(6, 22 + ar, 6, 11, coat); c.Rect(6, 22 + ar, 2, 11, coatD); c.Blob(9, 20 + ar, 3, 3, skin, skinL, skinD);
        c.Rect(36, 22 + ar, 6, 11, metal); c.Rect(37, 22 + ar, 2, 11, K(0xD3DAF0)); c.Rect(36, 28 + ar, 6, 1, coatDD); c.Blob(39, 20 + ar, 3.6f, 3.4f, K(0x4A5070), metal, K(0x2B2D44)); c.Rect(38, 17 + ar, 2, 1, cy);
        if (pose == 2) { c.Ellipse(39, 12 + ar, 4, 4, cy); c.Ellipse(39, 12 + ar, 2.2f, 2.2f, White); }
        // đầu
        c.Blob(24, 42 + b, 8, 8, skin, skinL, skinD); c.Rect(19, 36 + b, 10, 2, K(0x5A3A2A));   // râu lởm chởm
        c.Rect(16, 42 + b, 16, 4, K(0x3B405C)); c.Ellipse(20, 44 + b, 4, 3.6f, cy); c.Ellipse(20, 44 + b, 2.6f, 2.4f, White); c.Ellipse(29, 44 + b, 3, 3, cyD); c.Set(29, 44 + b, cy);   // kính gắn một mắt kính to
        c.Set(19, 45 + b, K(0xE23B5C)); c.Rect(22, 38 + b, 4, 1, K(0x1B1633)); if (pose == 2) c.Rect(21, 38 + b, 6, 2, K(0x2A0A14));
        // tóc bù xù
        for (int i = 0; i < 9; i++) { float a = Mathf.PI * (.04f + i * .115f); c.Poly(V(24, 48 + b, 24 + Mathf.Cos(a) * 11 - 2, 49 + b + Mathf.Sin(a) * 9, 24 + Mathf.Cos(a) * 14, 49 + b + Mathf.Sin(a) * 12, 24 + Mathf.Cos(a) * 11 + 2, 49 + b + Mathf.Sin(a) * 9), i % 2 == 0 ? hair : hairD); }
        c.Ellipse(24, 51 + b, 8, 4, hair);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 1f / 60f));
    }

    public static Sprite MechBody => Get("mech_body", () =>
    {
        var c = new Px(84, 76); Color32 hull = K(0x7F89AC), hullL = K(0xC3CCE8), hullD = K(0x444A68), hullDD = K(0x2B2F46), cy = K(0x4DE0FF), red = K(0xFF3B4E), org = K(0xFF9A3C);
        // chân
        c.Slab(14, 1, 22, 20, hull, hullL, hullD); c.Slab(48, 1, 22, 20, hull, hullL, hullD); c.Rect(12, 1, 26, 5, hullDD); c.Rect(46, 1, 26, 5, hullDD); c.Rect(18, 12, 14, 2, org); c.Rect(52, 12, 14, 2, org);
        // thân
        c.Poly(V(10, 20, 74, 20, 70, 54, 14, 54), hullD); c.Poly(V(12, 22, 72, 22, 68, 53, 16, 53), hull); c.Poly(V(12, 50, 72, 50, 68, 53, 16, 53), hullL); c.Rect(14, 30, 56, 3, hullD); c.Rect(14, 42, 56, 2, hullD);
        for (int i = 0; i < 6; i++) c.Rect(18 + i * 9, 24, 5, 3, i % 2 == 0 ? org : hullDD); c.Rect(26, 34, 32, 6, hullDD); c.Rect(28, 35, 28, 4, red); c.Rect(30, 36, 8, 1, K(0xFFC0C8));
        // vai & bệ tên lửa
        c.Slab(0, 38, 16, 22, hull, hullL, hullD); c.Slab(68, 38, 16, 22, hull, hullL, hullD); for (int i = 0; i < 3; i++) { c.Rect(2 + i * 4, 56, 3, 3, K(0xD9453C)); c.Rect(70 + i * 4, 56, 3, 3, K(0xD9453C)); }
        // buồng lái chứa nhà khoa học
        c.Ellipse(42, 62, 17, 13, hullDD); c.Ellipse(42, 62, 15.5f, 11.5f, K(0x8FE8FF)); c.Ellipse(36, 66, 8, 4, K(0xD9FAFF));
        c.Blob(42, 60, 6, 5.6f, K(0xE7B999), K(0xFFE0C8), K(0x9A6A4A)); c.Rect(36, 61, 12, 3, K(0x3B405C)); c.Ellipse(39, 62, 3, 2.6f, cy); c.Poly(V(34, 64, 50, 64, 46, 71, 38, 71), K(0xF2F2FA));
        c.Rect(26, 72, 32, 2, hullD); c.Rect(40, 52, 4, 6, hullDD);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 1f / 76f));
    });

    public static Sprite MechArm => Get("mech_arm", () =>
    {
        var c = new Px(18, 40); Color32 hull = K(0x7F89AC), hullL = K(0xC3CCE8), hullD = K(0x444A68), cy = K(0x4DE0FF);
        c.Slab(4, 14, 10, 24, hull, hullL, hullD); c.Rect(3, 22, 12, 3, hullD); c.Rect(3, 30, 12, 2, K(0xFF9A3C));
        c.Blob(9, 8, 8, 7, hull, hullL, hullD); c.Rect(4, 2, 3, 6, hullD); c.Rect(8, 1, 3, 7, hullD); c.Rect(12, 2, 3, 6, hullD); c.Rect(7, 9, 4, 2, cy);
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, 33f / 40f));
    });

    public static Sprite MechCore => Get("mech_core", () =>
    {
        var c = new Px(20, 20); c.Ellipse(10, 10, 9, 9, K(0x2B2F46)); c.Ellipse(10, 10, 7.4f, 7.4f, K(0xFF6B3C)); c.Ellipse(10, 10, 5, 5, K(0xFFC24D)); c.Ellipse(9, 11, 2.6f, 2.6f, White);
        for (int i = 0; i < 8; i++) { float a = i * Mathf.PI / 4f; c.Rect(10 + Mathf.RoundToInt(Mathf.Cos(a) * 9.5f), 10 + Mathf.RoundToInt(Mathf.Sin(a) * 9.5f), 1, 1, K(0xFFEFA0)); }
        c.Outline(Ink); return c.ToSprite(new Vector2(.5f, .5f));
    });
}
