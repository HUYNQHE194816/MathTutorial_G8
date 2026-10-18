using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>Nhạc nền. Hóa (Triệu Phú) có hệ thống âm thanh riêng trong MillionaireManager nên không nằm ở đây.</summary>
public enum Mus { None = 0, Menu = 1, Select = 2, Ly = 3, BossFight = 4 }

/// <summary>Hiệu ứng âm thanh. Số id cố định để đổi thứ tự/thêm mới không làm mất clip đã gán.</summary>
public enum Snd
{
    None = 0,
    // Menu / chọn
    UiHover = 10, UiClick = 11, UiBack = 12, CardSelect = 13, CardLocked = 14, ChapterSelect = 15, LessonSelect = 16,
    // Câu hỏi + kết thúc (dùng chung Lý & Sinh)
    QuizOpen = 20, QuizCorrect = 21, QuizWrong = 22, Victory = 23, Defeat = 24,
    // Game Lý
    LyStep = 30,
    // Trận boss: luồng + buff
    FightStart = 40, BuffOpen = 41, BuffPick = 42, IceCast = 43, Thunder = 44, SwordReflect = 45, AllySpawn = 46, AllyShoot = 47,
    // Hiệp sĩ
    PlayerDash = 50, PlayerSlash = 51, PlayerHurt = 52, ShieldBlock = 53,
    // Boss rồng
    BossHit = 60, BossHitCrit = 61, BossImmune = 62, BossHeal = 63, BossFreeze = 64, BossUnfreeze = 65, BossRoar = 66, BossFireball = 67,
    BossMeteorWarn = 68, BossChargeWarn = 69, BossLaserCharge = 70, BossLaserFire = 71, BossWingFlap = 72, BossWingQuake = 73, BossSummon = 74, BossDeath = 75,
    // Đệ tử của rồng + chung
    MinionSpawn = 80, MinionShoot = 81, MinionHit = 82, MinionDie = 83, Explosion = 90,
}

[Serializable]
public class SoundSlot
{
    public int id;
    public string label;
    public AudioClip clip;
    [Range(0f, 1f)] public float volume = 1f;
}

/// <summary>
/// Bảng âm thanh của cả game (Assets/Resources/GameAudioLibrary.asset, tự tạo khi mở Unity).
/// Chọn asset này rồi kéo file âm thanh vào từng ô. Ô trống = không phát (hiệu ứng của boss vẫn dùng tiếng bíp tổng hợp cũ).
/// </summary>
public class GameAudioLibrary : ScriptableObject
{
    [Header("Âm lượng chung")]
    [Range(0f, 1f)] public float musicVolume = .5f;
    [Range(0f, 1f)] public float sfxVolume = 1f;
    [Tooltip("Lệch cao độ ngẫu nhiên mỗi lần phát để tiếng chém / trúng đòn đỡ nhàm.")]
    [Range(0f, .3f)] public float pitchJitter = .05f;
    [Tooltip("Thời gian chuyển nhạc (giây).")]
    public float musicFade = .8f;

    [Header("Nhạc nền (lặp)")]
    [NonReorderable] public List<SoundSlot> music = new List<SoundSlot>();
    [Header("Hiệu ứng âm thanh")]
    [NonReorderable] public List<SoundSlot> sfx = new List<SoundSlot>();

    static readonly (int id, string label)[] MusicDefs =
    {
        ((int)Mus.Menu,      "Nhạc nền: Đăng nhập / Menu / Dashboard"),
        ((int)Mus.Select,    "Nhạc nền: Chọn môn / chương / bài (trống = dùng nhạc Menu)"),
        ((int)Mus.Ly,        "Nhạc nền: Game Lý (mê cung kho báu)"),
        ((int)Mus.BossFight, "Nhạc nền: Đánh boss rồng (Sinh)"),
    };

    static readonly (int id, string label)[] SfxDefs =
    {
        ((int)Snd.UiHover,       "[Menu] Rê chuột vào nút / thẻ"),
        ((int)Snd.UiClick,       "[Menu] Bấm nút"),
        ((int)Snd.UiBack,        "[Menu] Bấm nút Quay lại / Về"),
        ((int)Snd.CardSelect,    "[Chọn môn] Chọn thẻ môn học"),
        ((int)Snd.CardLocked,    "[Chọn môn/chương] Thẻ chưa mở (Sắp ra mắt)"),
        ((int)Snd.ChapterSelect, "[Chọn chương] Chọn chương"),
        ((int)Snd.LessonSelect,  "[Chọn bài] Chọn bài / Ôn cả chương"),

        ((int)Snd.QuizOpen,      "[Câu hỏi] Hiện câu hỏi (Lý + Sinh)"),
        ((int)Snd.QuizCorrect,   "[Câu hỏi] Trả lời đúng (Lý + Sinh)"),
        ((int)Snd.QuizWrong,     "[Câu hỏi] Trả lời sai (Lý + Sinh)"),
        ((int)Snd.Victory,       "[Kết thúc] Thắng (Lý + Sinh)"),
        ((int)Snd.Defeat,        "[Kết thúc] Thua (Lý + Sinh)"),

        ((int)Snd.LyStep,        "[Game Lý] Nhân vật bước sang ô khác"),

        ((int)Snd.FightStart,    "[Boss] Bắt đầu trận đấu"),
        ((int)Snd.BuffOpen,      "[Buff] Hiện bảng chọn buff"),
        ((int)Snd.BuffPick,      "[Buff] Chọn buff"),
        ((int)Snd.IceCast,       "[Buff] Kiếm Băng tung 3 hàng băng"),
        ((int)Snd.Thunder,       "[Buff] Sấm Sét đánh rồng"),
        ((int)Snd.SwordReflect,  "[Buff] Chém Phản hất đạn lửa"),
        ((int)Snd.AllySpawn,     "[Buff] Đệ Hiệp Sĩ xuất hiện"),
        ((int)Snd.AllyShoot,     "[Buff] Đệ Hiệp Sĩ bắn phép"),

        ((int)Snd.PlayerDash,    "[Hiệp sĩ] Lướt (Shift)"),
        ((int)Snd.PlayerSlash,   "[Hiệp sĩ] Kiếm chém"),
        ((int)Snd.PlayerHurt,    "[Hiệp sĩ] Trúng đòn / mất máu"),
        ((int)Snd.ShieldBlock,   "[Hiệp sĩ] Khiên chặn đòn"),

        ((int)Snd.BossHit,        "[Boss] Rồng trúng đòn thường"),
        ((int)Snd.BossHitCrit,    "[Boss] Rồng trúng đòn chí mạng"),
        ((int)Snd.BossImmune,     "[Boss] Rồng miễn nhiễm (lúc nổi giận)"),
        ((int)Snd.BossHeal,       "[Boss] Rồng hồi máu (khi trả lời sai)"),
        ((int)Snd.BossFreeze,     "[Boss] Rồng bị đóng băng"),
        ((int)Snd.BossUnfreeze,   "[Boss] Rồng tan băng"),
        ((int)Snd.BossRoar,       "[Boss] Gầm (thức tỉnh / cuồng nộ / nổi giận)"),
        ((int)Snd.BossFireball,   "[Boss] Phun đạn lửa (quạt + vòng lửa)"),
        ((int)Snd.BossMeteorWarn, "[Boss] Mưa thiên thạch: bắt đầu"),
        ((int)Snd.BossChargeWarn, "[Boss] Báo trước cú lao"),
        ((int)Snd.BossLaserCharge,"[Boss] Laze gồng năng lượng (miệng + lưới laze)"),
        ((int)Snd.BossLaserFire,  "[Boss] Laze bắn"),
        ((int)Snd.BossWingFlap,   "[Boss] Vỗ cánh"),
        ((int)Snd.BossWingQuake,  "[Boss] Sóng chấn từ cánh"),
        ((int)Snd.BossSummon,     "[Boss] Gọi đệ tử"),
        ((int)Snd.BossDeath,      "[Boss] Rồng chết"),

        ((int)Snd.MinionSpawn,   "[Đệ tử] Xuất hiện"),
        ((int)Snd.MinionShoot,   "[Đệ tử] Bắn đạn"),
        ((int)Snd.MinionHit,     "[Đệ tử] Trúng đòn"),
        ((int)Snd.MinionDie,     "[Đệ tử] Chết"),
        ((int)Snd.Explosion,     "[Chung] Nổ (thiên thạch, kỹ năng...)"),
    };

    Dictionary<int, SoundSlot> mapM, mapS;

    void OnEnable() { mapM = mapS = null; }
    void Reset() { Sync(); }
    void OnValidate() { Sync(); mapM = mapS = null; }

    /// <summary>Đồng bộ danh sách ô theo enum (giữ nguyên clip đã gán). Gọi tự động khi mở asset.</summary>
    public void Sync() { Merge(music, MusicDefs); Merge(sfx, SfxDefs); }

    static void Merge(List<SoundSlot> list, (int id, string label)[] defs)
    {
        var old = new Dictionary<int, SoundSlot>();
        foreach (var s in list) if (s != null) old[s.id] = s;
        var res = new List<SoundSlot>(defs.Length);
        foreach (var d in defs)
        {
            if (!old.TryGetValue(d.id, out var s)) s = new SoundSlot { id = d.id, volume = 1f };
            s.label = d.label; res.Add(s);
        }
        bool same = res.Count == list.Count;
        for (int i = 0; same && i < res.Count; i++) same = ReferenceEquals(res[i], list[i]);
        if (same) return;
        list.Clear(); list.AddRange(res);
    }

    public SoundSlot GetMusic(Mus m) { if (mapM == null) mapM = Index(music); mapM.TryGetValue((int)m, out var s); return s; }
    public SoundSlot GetSfx(Snd id) { if (mapS == null) mapS = Index(sfx); mapS.TryGetValue((int)id, out var s); return s; }

    static Dictionary<int, SoundSlot> Index(List<SoundSlot> l)
    {
        var d = new Dictionary<int, SoundSlot>();
        foreach (var s in l) if (s != null) d[s.id] = s;
        return d;
    }
}
