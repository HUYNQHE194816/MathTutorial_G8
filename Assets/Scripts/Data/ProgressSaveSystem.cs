using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

public struct BadgeDef
{
    public string id;
    public string title;
    public string desc;
    public Color color;

    public BadgeDef(string id, string title, string desc, Color color)
    {
        this.id = id; this.title = title; this.desc = desc; this.color = color;
    }
}

/// <summary>
/// Lưu tiến độ học tập của từng học sinh (theo Tên + Lớp) vào Application.persistentDataPath.
/// Mỗi học sinh một file JSON. Tự đồng bộ các số tổng hợp sang StudentProfileData.
/// </summary>
public static class ProgressSaveSystem
{
    public const string SubjectLy = "Lý";
    public const string SubjectHoa = "Hóa";
    public const string SubjectSinh = "Sinh";
    public static readonly string[] Subjects = { SubjectLy, SubjectHoa, SubjectSinh };

    private const int MaxSessions = 30;

    public static readonly BadgeDef[] Badges =
    {
        new BadgeDef("first_play", "Khởi động",      "Chơi màn đầu tiên",            new Color(1f, 0.78f, 0.25f)),
        new BadgeDef("first_win",  "Chiến thắng",    "Thắng một màn chơi",           new Color(0.35f, 0.85f, 0.55f)),
        new BadgeDef("diligent",   "Chăm chỉ",       "Chơi 5 lượt",                  new Color(0.4f, 0.65f, 1f)),
        new BadgeDef("sharp",      "Chính xác",      "Đúng từ 80% (≥10 câu)",        new Color(0.95f, 0.5f, 0.7f)),
        new BadgeDef("high",       "Cao thủ",        "Đạt 100 điểm trở lên",         new Color(1f, 0.55f, 0.3f)),
        new BadgeDef("explorer",   "Khám phá",       "Chơi từ 2 môn khác nhau",      new Color(0.6f, 0.5f, 0.95f)),
        new BadgeDef("scholar",    "Học bá",         "Hoàn thành 10 bài học",        new Color(0.3f, 0.8f, 0.85f)),
    };

    private static ProgressData current;

    /// <summary>Tiến độ của học sinh hiện tại (tự nạp lại nếu đã đổi học sinh).</summary>
    public static ProgressData Current
    {
        get
        {
            if (current == null ||
                current.studentName != StudentProfileData.studentName ||
                current.studentClass != StudentProfileData.studentClass)
            {
                Load(StudentProfileData.studentName, StudentProfileData.studentClass);
            }
            return current;
        }
    }

    // ---------- Đường dẫn ----------
    private static string PathFor(string name, string cls)
    {
        var sb = new StringBuilder("progress_");
        foreach (char c in (name ?? "") + "_" + (cls ?? ""))
            sb.Append(char.IsLetterOrDigit(c) ? char.ToLowerInvariant(c) : '_');
        return Path.Combine(Application.persistentDataPath, sb + ".json");
    }

    // ---------- Nạp / Lưu ----------
    public static ProgressData Load(string name, string cls)
    {
        name = name ?? "";
        cls = cls ?? "";

        ProgressData data = null;
        string path = PathFor(name, cls);
        try
        {
            if (File.Exists(path))
                data = JsonUtility.FromJson<ProgressData>(File.ReadAllText(path, Encoding.UTF8));
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Progress] Không đọc được " + path + ": " + e.Message);
        }

        if (data == null) data = new ProgressData();
        data.studentName = name;
        data.studentClass = cls;
        if (data.sessions == null) data.sessions = new List<SessionRecord>();
        if (data.subjects == null) data.subjects = new List<SubjectStats>();
        if (data.badges == null) data.badges = new List<string>();
        if (data.seenBadges == null) data.seenBadges = new List<string>();

        current = data;
        Refresh(data);
        return data;
    }

    public static void Save()
    {
        if (current == null) return;
        string path = PathFor(current.studentName, current.studentClass);
        string tmp = path + ".tmp";
        try
        {
            File.WriteAllText(tmp, JsonUtility.ToJson(current, true), Encoding.UTF8);
            if (File.Exists(path)) File.Delete(path);
            File.Move(tmp, path);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Progress] Không lưu được " + path + ": " + e.Message);
        }
    }

    // ---------- Ghi kết quả một lượt chơi ----------
    public static void RecordSession(string subject, int score, int correct, int wrong, bool won, float seconds)
    {
        var d = Current;

        d.sessions.Add(new SessionRecord
        {
            subject = subject,
            score = score,
            correct = correct,
            wrong = wrong,
            won = won,
            seconds = Mathf.RoundToInt(seconds),
            date = DateTime.Now.ToString("yyyy-MM-dd HH:mm")
        });
        if (d.sessions.Count > MaxSessions)
            d.sessions.RemoveRange(0, d.sessions.Count - MaxSessions);

        var s = GetOrCreateSubject(d, subject);
        s.attempts++;
        if (won) s.wins++;
        s.bestScore = Mathf.Max(s.bestScore, score);
        s.totalCorrect += correct;
        s.totalAnswered += correct + wrong;
        d.totalPlaySeconds += Mathf.RoundToInt(seconds);

        Refresh(d);
        Save();
    }

    // ---------- Truy vấn ----------
    public static SubjectStats FindSubject(ProgressData d, string subject)
    {
        foreach (var s in d.subjects) if (s.subject == subject) return s;
        return null;
    }

    private static SubjectStats GetOrCreateSubject(ProgressData d, string subject)
    {
        var s = FindSubject(d, subject);
        if (s != null) return s;
        s = new SubjectStats { subject = subject };
        d.subjects.Add(s);
        return s;
    }

    public static int Attempts(ProgressData d) { int n = 0; foreach (var s in d.subjects) n += s.attempts; return n; }
    public static int Wins(ProgressData d) { int n = 0; foreach (var s in d.subjects) n += s.wins; return n; }
    public static int BestScore(ProgressData d) { int n = 0; foreach (var s in d.subjects) n = Mathf.Max(n, s.bestScore); return n; }
    public static int SubjectsPlayed(ProgressData d) { int n = 0; foreach (var s in d.subjects) if (s.attempts > 0) n++; return n; }

    /// <summary>Tỉ lệ trả lời đúng 0..1.</summary>
    public static float Accuracy(ProgressData d)
    {
        int c = 0, a = 0;
        foreach (var s in d.subjects) { c += s.totalCorrect; a += s.totalAnswered; }
        return a > 0 ? (float)c / a : 0f;
    }

    public static float Accuracy(SubjectStats s)
    {
        return s != null && s.totalAnswered > 0 ? (float)s.totalCorrect / s.totalAnswered : 0f;
    }

    /// <summary>Số bài học hoàn thành = số lượt thắng (tối đa totalLessons).</summary>
    public static int CompletedLessons(ProgressData d)
    {
        return Mathf.Min(StudentProfileData.totalLessons, Wins(d));
    }

    // ---------- Huy hiệu + đồng bộ profile ----------
    private static bool IsUnlocked(string id, ProgressData d)
    {
        switch (id)
        {
            case "first_play": return Attempts(d) >= 1;
            case "first_win":  return Wins(d) >= 1;
            case "diligent":   return Attempts(d) >= 5;
            case "sharp":
                int answered = 0;
                foreach (var s in d.subjects) answered += s.totalAnswered;
                return answered >= 10 && Accuracy(d) >= 0.8f;
            case "high":       return BestScore(d) >= 100;
            case "explorer":   return SubjectsPlayed(d) >= 2;
            case "scholar":    return CompletedLessons(d) >= 10;
            default:           return false;
        }
    }

    private static void Refresh(ProgressData d)
    {
        foreach (var b in Badges)
            if (!d.badges.Contains(b.id) && IsUnlocked(b.id, d))
                d.badges.Add(b.id);

        StudentProfileData.totalAttempts = Attempts(d);
        StudentProfileData.highestScore = BestScore(d);
        StudentProfileData.lastScore = d.sessions.Count > 0 ? d.sessions[d.sessions.Count - 1].score : 0;
        StudentProfileData.completedLessons = CompletedLessons(d);
        StudentProfileData.totalBadges = d.badges.Count;
    }

    // ---------- Tiện ích Editor / thử nghiệm ----------
    public static void ResetCurrent()
    {
        var d = Current;
        string path = PathFor(d.studentName, d.studentClass);
        try { if (File.Exists(path)) File.Delete(path); } catch (Exception e) { Debug.LogWarning(e.Message); }
        Load(d.studentName, d.studentClass);
    }

    /// <summary>Thêm vài lượt chơi mẫu để xem dashboard khi chưa chơi thật.</summary>
    public static void DebugAddDemoData()
    {
        RecordSession(SubjectLy, 40, 4, 1, false, 300f);
        RecordSession(SubjectHoa, 60, 6, 1, false, 240f);
        RecordSession(SubjectLy, 90, 9, 1, true, 520f);
        RecordSession(SubjectHoa, 100, 10, 0, true, 410f);
        RecordSession(SubjectLy, 120, 12, 2, true, 480f);
        RecordSession(SubjectHoa, 30, 3, 1, false, 150f);
        Debug.Log("[Progress] Đã thêm dữ liệu mẫu cho " + Current.studentName);
    }
}
