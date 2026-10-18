using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Thông tin credit — SỬA Ở ĐÂY cho đúng tên nhóm làm game. Các dòng "Cảm ơn" và "Tài nguyên bên thứ ba" cũng nên rà lại
/// theo đúng những gì thực sự có trong bản phát hành (ví dụ nếu dùng nhạc nền có sẵn trong Assets/AudioBank thì ghi nguồn và giấy phép của chúng ở đây).
/// </summary>
public static class CreditsData
{
    public static string GameTitle = "KHTN 8 — HÀNH TRÌNH GỠ SƯƠNG";
    public static string[][] Sections =
    {
        new[] { "Thiết kế gameplay & lập trình", "Nhóm phát triển dự án KHTN 8" },
        new[] { "Nội dung học tập", "Câu hỏi tự luận và barem Vật lí, Hóa học, Sinh học", "bám theo chương trình Khoa học tự nhiên lớp 8" },
        new[] { "Đồ họa", "Toàn bộ pixel art (hiệp sĩ, vũ khí, quái, ba boss, môi trường)", "được vẽ bằng mã trong game" },
        new[] { "Âm thanh", "Hiệu ứng và nhạc nền của chế độ Gỡ Sương", "được tổng hợp bằng mã trong game" },
        new[] { "Phông chữ", "Philosopher — Jovanny Lemonad (SIL Open Font License)" },
        new[] { "Công nghệ", "Unity 6 · C#", "Dựng giao diện bằng mã" },
        new[] { "Cảm ơn", "Các thầy cô đã đồng hành", "và những học sinh đã chơi thử, góp ý" },
    };
}

/// <summary>
/// Các cảnh phim trong game: mở đầu (Thầy Cú giao nhiệm vụ), ba đoạn hồi ức sau mỗi boss, và kết thúc (phong ấn vỡ, bình minh, gốc cổ thụ) cùng credit.
/// Dựng bằng UI + sprite pixel của RogueArt, có gõ chữ từng ký tự. Space / Enter / chuột để sang câu tiếp; Esc để bỏ qua cả cảnh.
/// </summary>
public class StoryDirector : MonoBehaviour
{
    RectTransform root, layer; Image fadeImg, topBar, botBar; Text caption, hint, speaker; bool skip, advance; Transform host;

    public void Init(Transform parent)
    {
        host = parent;
        root = UIKit.BuildCanvas(parent, "Story_UI", 100); root.gameObject.SetActive(false);
        layer = UIKit.CBox("Layer", root, new Color(0, 0, 0, 0), Vector2.zero, Vector2.zero); UIKit.Stretch(layer, 0, 0, 0, 0);
        topBar = UIKit.CBox("Top", root, Color.black, new Vector2(0, 490), new Vector2(2100, 200)).GetComponent<Image>();
        botBar = UIKit.CBox("Bot", root, Color.black, new Vector2(0, -490), new Vector2(2100, 200)).GetComponent<Image>();
        speaker = UIKit.Label("Speaker", root, "", 34, GameTheme.EmberHi, TextAnchor.LowerLeft, new Vector2(0, -370), new Vector2(1500, 46), true, true, FontStyle.Bold);
        caption = UIKit.Label("Caption", root, "", 38, GameTheme.Bone, TextAnchor.UpperLeft, new Vector2(0, -455), new Vector2(1500, 150), true, true);
        hint = UIKit.Label("Hint", root, "Space / chuột: tiếp  ·  Esc: bỏ qua", 22, GameTheme.Fog, TextAnchor.LowerRight, new Vector2(700, -515), new Vector2(560, 30));
        fadeImg = UIKit.CBox("Fade", root, new Color(0, 0, 0, 1), Vector2.zero, Vector2.zero).GetComponent<Image>(); UIKit.Stretch(fadeImg.rectTransform, 0, 0, 0, 0); fadeImg.raycastTarget = false;
    }

    // ------------------------------------------------------------------ tiện ích dựng cảnh
    void Begin()
    {
        skip = false; root.gameObject.SetActive(true); Clear(); fadeImg.color = Color.black; caption.text = ""; speaker.text = ""; hint.enabled = true;
    }

    void End(Action done) { Clear(); root.gameObject.SetActive(false); done?.Invoke(); }
    void Clear() { for (int i = layer.childCount - 1; i >= 0; i--) Destroy(layer.GetChild(i).gameObject); }

    Image Img(string n, Sprite s, Vector2 pos, Vector2 size, Color? c = null, bool additive = false)
    {
        var r = UIKit.CBox(n, layer, Color.white, pos, size); var im = r.GetComponent<Image>(); im.sprite = s; im.preserveAspect = true; im.raycastTarget = false; im.color = c ?? Color.white;
        if (additive) im.material = Gfx.Additive; return im;
    }

    Image Rect(string n, Color c, Vector2 pos, Vector2 size) { var r = UIKit.CBox(n, layer, c, pos, size); var im = r.GetComponent<Image>(); im.raycastTarget = false; return im; }

    static Sprite Gradient(Color top, Color bottom)
    {
        var px = new Px(2, 64);
        for (int y = 0; y < 64; y++) { var col = (Color32)Color.Lerp(bottom, top, y / 63f); for (int x = 0; x < 2; x++) px.p[y * 2 + x] = col; }
        return px.ToSprite(new Vector2(.5f, .5f), 16f, FilterMode.Bilinear);
    }

    static Sprite Hills(Color c, float seed, float amp, float freq)
    {
        var px = new Px(256, 64);
        for (int x = 0; x < 256; x++) { int h = Mathf.RoundToInt(10 + amp * (Mathf.PerlinNoise(x * freq + seed, seed) * .8f + Mathf.Sin(x * .03f + seed) * .2f)); for (int y = 0; y < Mathf.Min(63, h); y++) px.p[y * 256 + x] = (Color32)c; }
        return px.ToSprite(new Vector2(.5f, 0f), 16f, FilterMode.Point);
    }

    IEnumerator Wait(float s) { float t = 0f; while (t < s && !skip) { t += Time.unscaledDeltaTime; Poll(); yield return null; } }

    void Poll()
    {
        var k = Keyboard.current; var m = Mouse.current;
        if (k != null && k.escapeKey.wasPressedThisFrame) skip = true;
        if ((k != null && (k.spaceKey.wasPressedThisFrame || k.enterKey.wasPressedThisFrame)) || (m != null && m.leftButton.wasPressedThisFrame)) advance = true;
    }

    IEnumerator Fade(float to, float dur)
    {
        float a0 = fadeImg.color.a, t = 0f; while (t < dur && !skip) { t += Time.unscaledDeltaTime; Poll(); var c = fadeImg.color; c.a = Mathf.Lerp(a0, to, t / dur); fadeImg.color = c; yield return null; }
        var f = fadeImg.color; f.a = to; fadeImg.color = f;
    }

    /// <summary>Gõ chữ từng ký tự; bấm một lần để hiện hết, bấm thêm lần nữa (hoặc chờ) để sang câu sau.</summary>
    IEnumerator Say(string who, string text, Color? whoCol = null, float auto = -1f)
    {
        if (skip) yield break;
        speaker.text = who ?? ""; speaker.color = whoCol ?? GameTheme.EmberHi; caption.text = ""; advance = false; float t = 0f; int shown = 0;
        while (shown < text.Length && !skip)
        {
            Poll(); if (advance) { shown = text.Length; advance = false; break; }
            t += Time.unscaledDeltaTime * 42f; int n = Mathf.Min(text.Length, (int)t); if (n != shown) { shown = n; if (shown % 3 == 0) RogueAudio.Play(RSfx.Tick, .12f, 1.6f + (who == "Thầy Cú" ? .2f : 0f)); }
            caption.text = text.Substring(0, shown); yield return null;
        }
        caption.text = text; advance = false; float hold = auto > 0f ? auto : Mathf.Clamp(text.Length * .045f + 1.4f, 2.2f, 7f); float h = 0f;
        while (h < hold && !skip) { Poll(); if (advance) { advance = false; break; } h += Time.unscaledDeltaTime; yield return null; }
    }

    void Tween(Action<float> set, float dur, Func<float, float> ease = null) { StartCoroutine(TweenCo(set, dur, ease)); }
    IEnumerator TweenCo(Action<float> set, float dur, Func<float, float> ease) { float t = 0f; while (t < dur) { t += Time.unscaledDeltaTime; float k = Mathf.Clamp01(t / dur); try { set(ease != null ? ease(k) : k); } catch (MissingReferenceException) { yield break; } yield return null; } try { set(1f); } catch (MissingReferenceException) { } }
    static float Smooth(float k) => k * k * (3f - 2f * k);

    void Anim(Image im, Sprite[] fr, float fps) { StartCoroutine(AnimCo(im, fr, fps)); }
    IEnumerator AnimCo(Image im, Sprite[] fr, float fps) { float t = 0f; while (im != null) { t += Time.unscaledDeltaTime; im.sprite = fr[(int)(t * fps) % fr.Length]; yield return null; } }

    void Drift(Image im, Vector2 vel, float dur) { Vector2 p0 = im.rectTransform.anchoredPosition; Tween(k => im.rectTransform.anchoredPosition = p0 + vel * (k * dur), dur); }

    // ------------------------------------------------------------------ MỞ ĐẦU
    public void PlayIntro(Action done) { StartCoroutine(IntroCo(done)); }

    IEnumerator IntroCo(Action done)
    {
        Begin(); RogueAudio.PlayTrack(RTrack.Intro);
        var sky = Img("Bg", Gradient(new Color(.05f, .04f, .1f), new Color(.2f, .2f, .3f)), Vector2.zero, new Vector2(2100, 1200)); sky.preserveAspect = false;
        for (int i = 0; i < 7; i++) { var f = Img("Fog" + i, RogueArt.FogPuff, new Vector2(-800 + i * 260, -150 + (i % 3) * 120), new Vector2(900, 900), new Color(.8f, .85f, 1f, .5f)); Drift(f, new Vector2((i % 2 == 0 ? 1 : -1) * 60, 0), 30f); }
        var knight = Img("Knight", RogueArt.KnightIdle[0], new Vector2(-250, -110), new Vector2(300, 380), new Color(.55f, .55f, .7f, 0f)); Anim(knight, RogueArt.KnightIdle, 3f);
        yield return Fade(0f, 1.6f);
        Tween(k => { var c = knight.color; c.a = k; knight.color = c; knight.color = Color.Lerp(new Color(.55f, .55f, .7f, k), Color.white, k); }, 3f);
        yield return Say("", "…Sương. Chỉ có sương, và một thứ gì đó rất lạnh trong lồng ngực.", Color.white);
        yield return Say("", "Mình là ai? Tại sao lại ở đây?", Color.white);
        var owl = Img("Owl", RogueArt.Owl[0], new Vector2(300, -140), new Vector2(240, 300), new Color(1, 1, 1, 0)); Anim(owl, RogueArt.Owl, 1.5f);
        Tween(k => { var c = owl.color; c.a = k; owl.color = c; owl.rectTransform.anchoredPosition = new Vector2(300 - 60 * (1 - k), -140); }, 1.6f, Smooth);
        RogueAudio.Play(RSfx.Heal, .5f, .8f);
        yield return Say("Thầy Cú", "Em tỉnh rồi à, chàng hiệp sĩ nhỏ. Đừng cố nhớ tên mình, màn sương đã nuốt mất nó.");
        yield return Say("Thầy Cú", "Nơi này từng là Học viện Khoa học Tự nhiên. Một thí nghiệm hỏng đã gieo màn sương lên mọi ký ức.");
        yield return Say("Thầy Cú", "Ba khu thí nghiệm: Hóa học, Sinh học và Vật lí vẫn còn đó, canh giữ bởi những kẻ đã quên chính mình.");
        yield return Say("Thầy Cú", "Chỉ cần em còn biết lập luận, biết viết ra lời giải, ký ức sẽ quay về. Mỗi bài giải là một ký ức được gọi trở lại.");
        yield return Say("Thầy Cú", "Đi thôi, hiệp sĩ. Ánh sáng chỉ cần một người chịu bước vào bóng tối.");
        yield return Fade(1f, 1.2f);
        RogueAudio.PlayTrack(RTrack.None); End(done);
    }

    // ------------------------------------------------------------------ HỒI ỨC SAU MỖI BOSS
    public void PlayFlashback(int stage, Action done) { StartCoroutine(FlashbackCo(stage, done)); }

    IEnumerator FlashbackCo(int stage, Action done)
    {
        Begin(); RogueAudio.PlayTrack(RTrack.Altar); RogueAudio.Play(RSfx.Stinger, .7f, .8f);
        Color warm = stage == 1 ? new Color(.95f, .8f, .5f) : stage == 2 ? new Color(.7f, .95f, .6f) : new Color(.6f, .85f, 1f);
        var bg = Img("Bg", Gradient(Color.Lerp(warm, Color.black, .75f), Color.Lerp(warm, Color.black, .35f)), Vector2.zero, new Vector2(2100, 1200)); bg.preserveAspect = false;
        var gl = Img("Glow", RogueArt.Glow, new Vector2(0, 40), new Vector2(1500, 1100), new Color(warm.r, warm.g, warm.b, .45f), true);
        var shard = Img("Shard", RogueArt.MemoryShardFrames[0], new Vector2(0, 360), new Vector2(60, 100), Color.white); Tween(k => shard.rectTransform.anchoredPosition = new Vector2(0, 360 + 14 * Mathf.Sin(k * 40f)), 60f);
        Anim(shard, RogueArt.MemoryShardFrames, 2f);
        var title = UIKit.Label("FbTitle", layer, "Mảnh Ký Ức " + stage + "/3", 44, new Color(.6f, .92f, 1f), TextAnchor.MiddleCenter, new Vector2(0, 430), new Vector2(1200, 60), true, true, FontStyle.Bold);
        var floor = Rect("Floor", new Color(0, 0, 0, .35f), new Vector2(0, -260), new Vector2(2100, 260));
        yield return Fade(0f, 1.2f);
        if (stage == 1)
        {
            var human = Img("Human", RogueArt.RiderFrames(1)[0], new Vector2(-260, -90), new Vector2(240, 290)); Anim(human, RogueArt.RiderFrames(1), 2f);
            var mouse = Img("Mouse", RogueArt.MonsterFrames(0, 5)[0], new Vector2(120, -190), new Vector2(190, 140), new Color(1f, 1f, 1f)); Anim(mouse, RogueArt.MonsterFrames(0, 5), 3f);
            var flask = Img("Flask", RogueArt.MonsterFrames(0, 2)[0], new Vector2(430, -120), new Vector2(190, 190), new Color(1, 1, 1, .8f));
            yield return Say("", "Có một cô trợ giảng hóa học nuôi một chú chuột bạch để thử các dung dịch an toàn. Cô gọi nó là Bạch.", warm);
            yield return Say("", "Hôm ấy cô viết lên bảng: \"Chất không tự mất đi, nó chỉ biến đổi thành chất khác.\"", warm);
            Tween(k => { human.color = new Color(1, 1, 1, 1f - k); }, 3f);
            yield return Say("", "Rồi màn sương tràn vào phòng thí nghiệm. Cô biến mất, còn Bạch ở lại một mình, ôm theo ống tiêm của cô.", warm);
            yield return Say("", "Bạch không còn nhớ ai đã từng dịu dàng với nó.", warm);
        }
        else if (stage == 2)
        {
            var human = Img("Human", RogueArt.RiderFrames(1)[0], new Vector2(-300, -90), new Vector2(240, 290)); Anim(human, RogueArt.RiderFrames(1), 2f);
            var fox = Img("Fox", RogueArt.FoxBody[0], new Vector2(120, -60), new Vector2(240, 275)); Anim(fox, RogueArt.FoxBody, 1.6f); fox.transform.localScale = Vector3.one * .8f;
            var plant = Img("Plant", RogueArt.MonsterFrames(1, 0)[0], new Vector2(450, -120), new Vector2(220, 220), new Color(1, 1, 1, .9f));
            yield return Say("", "Nhà thực vật học tìm thấy một chú cáo con bị thương trong vườn của học viện.", warm);
            yield return Say("", "Bà băng chân cho nó, rồi dạy nó nhận ra từng loài cây, từng bộ phận của cơ thể sống: rễ, thân, lá, hoa.", warm);
            Tween(k => { human.color = new Color(1, 1, 1, 1f - k); }, 3f);
            yield return Say("", "Màn sương làm cáo con quên tên mình, nhưng chín cái đuôi vẫn không chịu ngừng tìm kiếm người đã dạy nó.", warm);
        }
        else
        {
            var sci = Img("Sci", RogueArt.Scientist[0], new Vector2(-120, -60), new Vector2(300, 375)); Anim(sci, RogueArt.Scientist, 1.6f);
            var core = Img("Core", RogueArt.MechCore, new Vector2(330, -40), new Vector2(120, 120), Color.white, false); Tween(k => core.rectTransform.localScale = Vector3.one * (1f + .15f * Mathf.Sin(k * 80f)), 60f);
            yield return Say("", "Nhà vật lí muốn giữ gìn mọi ký ức của học viện mãi mãi. Ông chế ra một cỗ máy ghi nhớ mọi thứ.", warm);
            yield return Say("", "Nhưng cỗ máy ghi nhớ quá nhiều mà không biết buông, nó tràn ra thành màn sương và nuốt chửng tất cả.", warm);
            yield return Say("", "Ông đứng giữa màn sương do chính mình tạo ra, quên cả mục đích ban đầu: giữ ký ức để người ta có thể nhớ lại, chứ không phải để quên đi.", warm);
        }
        yield return Fade(1f, 1.2f); RogueAudio.PlayTrack(RTrack.None); End(done);
    }

    // ------------------------------------------------------------------ KẾT THÚC
    public void PlayEnding(Action done) { StartCoroutine(EndingCo(done)); }

    IEnumerator EndingCo(Action done)
    {
        Begin(); RogueAudio.PlayTrack(RTrack.Ending); hint.enabled = true;

        // 1 · bệ đá sáng rực, rune hiện lên
        var bg = Img("Bg", Gradient(new Color(.03f, .03f, .08f), new Color(.14f, .12f, .24f)), Vector2.zero, new Vector2(2100, 1200)); bg.preserveAspect = false;
        var altar = Img("Altar", RogueArt.Altar, new Vector2(0, -170), new Vector2(880, 600)); 
        Color[] cols = { GameTheme.Ly, GameTheme.Hoa, GameTheme.Sinh }; Image[] glyph = new Image[3];
        for (int i = 0; i < 3; i++) { var st = Img("St" + i, RogueArt.RuneStone, new Vector2(-380 + i * 380, 20 + (i == 1 ? 80 : 0)), new Vector2(150, 320)); glyph[i] = Img("Gl" + i, RogueArt.RuneGlyph(i), new Vector2(-380 + i * 380, 70 + (i == 1 ? 80 : 0)), new Vector2(70, 105), new Color(cols[i].r, cols[i].g, cols[i].b, 0f), true); }
        var light = Img("Light", RogueArt.Glow, new Vector2(0, 40), new Vector2(200, 200), new Color(.8f, .9f, 1f, 0f), true);
        yield return Fade(0f, 1.6f);
        for (int i = 0; i < 3; i++) { int k = i; Tween(t => glyph[k].color = new Color(cols[k].r, cols[k].g, cols[k].b, t), 1.2f); RogueAudio.Play(RSfx.Rune, .8f); yield return Wait(.9f); }
        Tween(t => { light.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(200, 1800, t); light.color = new Color(.85f, .93f, 1f, t); }, 4f, Smooth);
        yield return Say("", "Ba mảnh ký ức chạm vào nhau, và bệ đá cổ bắt đầu rung lên.", Color.white);
        var fl = fadeImg; 
        yield return Say("", "Vết nứt chạy khắp phong ấn, ánh sáng trào ra như thứ đã bị giam giữ quá lâu.", Color.white);

        // 2 · phong ấn vỡ, sương tan
        fadeImg.color = new Color(1, 1, 1, 0f); yield return Fade(1f, .5f); RogueAudio.Play(RSfx.Boom, 1f); RogueAudio.Play(RSfx.Stinger, 1f);
        Clear(); var bg2 = Img("Bg", Gradient(new Color(.55f, .6f, .75f), new Color(.85f, .88f, .95f)), Vector2.zero, new Vector2(2100, 1200)); bg2.preserveAspect = false;
        for (int i = 0; i < 10; i++) { var f = Img("Fog" + i, RogueArt.FogPuff, new Vector2(-900 + i * 200, -250 + (i % 4) * 130), new Vector2(900, 900), new Color(.9f, .92f, 1f, .9f)); Image fi = f; Vector2 p0 = f.rectTransform.anchoredPosition; float dir = i % 2 == 0 ? 1f : -1f; Tween(t => { fi.rectTransform.anchoredPosition = p0 + new Vector2(dir * 700 * t, 120 * t); fi.color = new Color(.9f, .92f, 1f, .9f * (1f - t)); }, 7f); }
        for (int i = 0; i < 26; i++) { var sh = Img("Sh" + i, RogueArt.Shard, Vector2.zero, new Vector2(34, 34), Color.Lerp(cols[i % 3], Color.white, .4f), true); Image si = sh; Vector2 v = UnityEngine.Random.insideUnitCircle.normalized * UnityEngine.Random.Range(300, 900); Tween(t => { si.rectTransform.anchoredPosition = v * t + new Vector2(0, -300 * t * t); si.rectTransform.localEulerAngles = new Vector3(0, 0, t * 700f); si.color = new Color(si.color.r, si.color.g, si.color.b, 1f - t); }, 3f); }
        yield return Fade(0f, .8f);
        yield return Say("", "Phong ấn vỡ tan. Màn sương lan ra khắp học viện rồi tan đi như chưa từng tồn tại.", Color.white);

        // 3 · cảnh toàn cảnh lấy lại màu
        yield return Fade(1f, .8f); Clear();
        var gray = Img("Gray", RogueArt.Landscape(false), Vector2.zero, new Vector2(2000, 1125)); var color = Img("Color", RogueArt.Landscape(true), Vector2.zero, new Vector2(2000, 1125), new Color(1, 1, 1, 0));
        gray.preserveAspect = false; color.preserveAspect = false; gray.rectTransform.localScale = Vector3.one * 1.1f; color.rectTransform.localScale = Vector3.one * 1.1f;
        Tween(t => { gray.rectTransform.localScale = Vector3.one * Mathf.Lerp(1.1f, 1.0f, t); color.rectTransform.localScale = gray.rectTransform.localScale; color.color = new Color(1, 1, 1, Smooth(Mathf.Clamp01((t - .15f) / .6f))); }, 12f);
        yield return Fade(0f, 1f);
        yield return Say("", "Từng cánh đồng, từng dòng sông, từng mái nhà lấy lại màu sắc của nó.", Color.white);
        yield return Wait(.8f);

        // 4 · bình minh
        yield return Fade(1f, .8f); Clear();
        var sky = Img("Sky", Gradient(new Color(.18f, .2f, .5f), new Color(1f, .75f, .45f)), Vector2.zero, new Vector2(2100, 1200)); sky.preserveAspect = false;
        var sun = Img("Sun", RogueArt.Disc, new Vector2(0, -420), new Vector2(240, 240), new Color(1f, .93f, .6f)); var sunGlow = Img("SunGlow", RogueArt.Glow, new Vector2(0, -420), new Vector2(1000, 1000), new Color(1f, .85f, .5f, .8f), true);
        var h1 = Img("H1", Hills(new Color(.42f, .34f, .5f), 3f, 26, .02f), new Vector2(0, -540), new Vector2(2300, 600)); h1.preserveAspect = false;
        var h2 = Img("H2", Hills(new Color(.22f, .26f, .32f), 11f, 22, .03f), new Vector2(0, -560), new Vector2(2300, 540)); h2.preserveAspect = false;
        Tween(t => { float y = Mathf.Lerp(-420, 60, Smooth(t)); sun.rectTransform.anchoredPosition = new Vector2(0, y); sunGlow.rectTransform.anchoredPosition = new Vector2(0, y); sunGlow.rectTransform.sizeDelta = Vector2.one * Mathf.Lerp(700, 1500, t); }, 10f);
        for (int i = 0; i < 5; i++) { int bn = i; var bird = Img("Bird" + i, RogueArt.Shard, new Vector2(-900, 200 + i * 40), new Vector2(26, 14), new Color(.1f, .08f, .15f)); Image bi = bird; float sy = 200 + i * 40; Tween(t => bi.rectTransform.anchoredPosition = new Vector2(-900 + 1900 * t, sy + 40 * Mathf.Sin(t * 14f + bn)), 12f + bn); }
        RogueAudio.Play(RSfx.Heal, .8f, .7f);
        yield return Fade(0f, 1f);
        yield return Say("", "Bình minh đầu tiên sau rất lâu. Ánh sáng chạm vào từng cuốn sách đã ngủ quên.", Color.white);
        yield return Wait(.6f);

        // 5 · gốc cổ thụ
        yield return Fade(1f, .8f); Clear();
        var sky2 = Img("Sky", Gradient(new Color(.45f, .7f, .95f), new Color(1f, .9f, .7f)), Vector2.zero, new Vector2(2100, 1200)); sky2.preserveAspect = false;
        var hh = Img("H", Hills(new Color(.3f, .6f, .35f), 5f, 20, .02f), new Vector2(0, -540), new Vector2(2300, 560)); hh.preserveAspect = false;
        var grass = Rect("Grass", new Color(.28f, .55f, .3f), new Vector2(0, -420), new Vector2(2100, 360));
        var tree = Img("Tree", RogueArt.AncientTree, new Vector2(330, 20), new Vector2(760, 820)); 
        var kn = Img("Knight", RogueArt.KnightIdle[0], new Vector2(-60, -230), new Vector2(240, 304)); Anim(kn, RogueArt.KnightIdle, 3f);
        var ow = Img("Owl", RogueArt.Owl[0], new Vector2(-380, -250), new Vector2(210, 260)); Anim(ow, RogueArt.Owl, 1.5f);
        for (int i = 0; i < 14; i++) { var leaf = Img("Leaf" + i, RogueArt.Leaf, new Vector2(100 + UnityEngine.Random.Range(0, 500), 380), new Vector2(34, 28)); Image li = leaf; float lx = leaf.rectTransform.anchoredPosition.x; float sp = UnityEngine.Random.Range(8f, 14f); Tween(t => li.rectTransform.anchoredPosition = new Vector2(lx - 300 * t + 40 * Mathf.Sin(t * 20f), 380 - 780 * t), sp); }
        yield return Fade(0f, 1f);
        yield return Say("Thầy Cú", "Em nhớ ra chưa? Em từng là một học trò của học viện này.");
        yield return Say("Hiệp sĩ", "Em vẫn chưa nhớ hết… nhưng em nhớ cách đi tìm câu trả lời.", new Color(.8f, .9f, 1f));
        yield return Say("Thầy Cú", "Vậy là đủ rồi. Tri thức không nằm ở chỗ không bao giờ quên, mà ở chỗ ta biết cách nhớ lại.");
        yield return Say("Thầy Cú", "Hôm nay lớp học mới mở cửa trở lại. Em có muốn làm học trò đầu tiên không?");
        yield return Say("Hiệp sĩ", "Dạ, em sẵn sàng.", new Color(.8f, .9f, 1f), 2.4f);

        // 6 · tựa đề và credit
        yield return Fade(1f, 1.6f); Clear(); speaker.text = ""; caption.text = ""; hint.enabled = false; RogueAudio.PlayTrack(RTrack.Credits);
        yield return Credits(); RogueAudio.PlayTrack(RTrack.None); End(done);
    }

    IEnumerator Credits()
    {
        skip = false; Clear(); fadeImg.color = new Color(0, 0, 0, 1f); topBar.enabled = false; botBar.enabled = false;
        var bg = Img("Bg", Gradient(new Color(.02f, .02f, .06f), new Color(.1f, .08f, .2f)), Vector2.zero, new Vector2(2100, 1200)); bg.preserveAspect = false;
        for (int i = 0; i < 60; i++) { var st = Rect("Star" + i, new Color(1, 1, 1, UnityEngine.Random.Range(.2f, .8f)), new Vector2(UnityEngine.Random.Range(-1000, 1000), UnityEngine.Random.Range(-560, 560)), new Vector2(4, 4)); Image si = st; float ph = UnityEngine.Random.value * 6f; Tween(t => { if (si != null) si.color = new Color(1, 1, 1, .3f + .4f * Mathf.Sin(t * 200f + ph)); }, 120f); }
        yield return Fade(0f, 1.2f);
        var titleBlock = new GameObject("Credits").AddComponent<RectTransform>(); titleBlock.SetParent(layer, false); titleBlock.anchorMin = titleBlock.anchorMax = new Vector2(.5f, .5f); titleBlock.anchoredPosition = new Vector2(0, -700); titleBlock.sizeDelta = new Vector2(1600, 10);
        float y = 0f;
        UIKit.Label("T", titleBlock, CreditsData.GameTitle, 80, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 120), true, true, FontStyle.Bold); y -= 150;
        UIKit.Label("T2", titleBlock, "Một hành trình tìm lại ký ức bằng lập luận", 38, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 60), true, true, FontStyle.Italic); y -= 190;
        // trang trí: ba biểu tượng boss
        var ico = new[] { RogueArt.RatBoss[0], RogueArt.FoxBody[0], RogueArt.Scientist[0] };
        for (int i = 0; i < 3; i++) { var im = UIKit.CBox("B" + i, titleBlock, Color.white, new Vector2(-380 + i * 380, y - 90), new Vector2(240, 240)).GetComponent<Image>(); im.sprite = ico[i]; im.preserveAspect = true; im.raycastTarget = false; }
        y -= 340;
        foreach (var sec in CreditsData.Sections)
        {
            UIKit.Label("H", titleBlock, sec[0], 44, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 64), true, true, FontStyle.Bold); y -= 74;
            for (int i = 1; i < sec.Length; i++) { UIKit.Label("L", titleBlock, sec[i], 34, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 50), true); y -= 52; }
            y -= 90;
        }
        UIKit.Label("S0", titleBlock, "Hành trình của bạn", 44, GameTheme.GoldHi, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 64), true, true, FontStyle.Bold); y -= 74;
        string title = Meta.Title; 
        UIKit.Label("S1", titleBlock, "Độ khó: " + RoomRules.DifficultyName(RunState.difficulty) + "   ·   Mảnh ký ức: 3/3   ·   Mực còn lại: " + RunState.ink, 32, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 50), true); y -= 52;
        UIKit.Label("S2", titleBlock, "Số lần gỡ sương thành công: " + (Meta.Clears + 1) + (string.IsNullOrEmpty(title) ? "" : "   ·   Danh hiệu: " + title), 32, GameTheme.Bone, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 50), true); y -= 200;
        UIKit.Label("End", titleBlock, "Cảm ơn bạn đã chơi!", 72, GameTheme.EmberHi, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 110), true, true, FontStyle.Bold); y -= 130;
        var endLabel = UIKit.Label("Hint", titleBlock, "Nhấn Space để về menu", 32, GameTheme.Fog, TextAnchor.MiddleCenter, new Vector2(0, y), new Vector2(1700, 50), true); endLabel.color = new Color(1, 1, 1, 0);
        float total = -y + 760f, pos = -700f, speed = 95f; bool done = false; float t2 = 0f;
        while (!done)
        {
            Poll(); var k = Keyboard.current; float sp = (k != null && (k.downArrowKey.isPressed)) ? speed * 4f : (advance ? speed * 3f : speed); advance = false; if (skip) sp = speed * 8f;
            pos += sp * Time.unscaledDeltaTime; if (pos > total - 560f) { pos = total - 560f; t2 += Time.unscaledDeltaTime; endLabel.color = new Color(.9f, .9f, 1f, .5f + .5f * Mathf.Sin(t2 * 3f)); if (t2 > 1.2f && (Keyboard.current != null && (Keyboard.current.spaceKey.wasPressedThisFrame || Keyboard.current.enterKey.wasPressedThisFrame))) done = true; if (t2 > 25f) done = true; }
            titleBlock.anchoredPosition = new Vector2(0, pos); yield return null;
        }
        yield return Fade(1f, 1.4f);
    }
}
