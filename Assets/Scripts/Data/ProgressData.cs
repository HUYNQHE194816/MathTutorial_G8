using System;
using System.Collections.Generic;

/// <summary>Một lượt chơi đã kết thúc.</summary>
[Serializable]
public class SessionRecord
{
    public string subject;
    public int score;
    public int correct;
    public int wrong;
    public bool won;
    public int seconds;
    public string date;   // "yyyy-MM-dd HH:mm"
}

/// <summary>Thống kê cộng dồn theo môn.</summary>
[Serializable]
public class SubjectStats
{
    public string subject;
    public int attempts;
    public int wins;
    public int bestScore;
    public int totalCorrect;
    public int totalAnswered;
}

/// <summary>Toàn bộ tiến độ của một học sinh (lưu thành 1 file JSON).</summary>
[Serializable]
public class ProgressData
{
    public string studentName = "";
    public string studentClass = "";
    public int totalPlaySeconds;
    public List<SessionRecord> sessions = new List<SessionRecord>();
    public List<SubjectStats> subjects = new List<SubjectStats>();
    public List<string> badges = new List<string>();       // huy hiệu đã mở
    public List<string> seenBadges = new List<string>();   // huy hiệu đã được hiện trên dashboard
}
