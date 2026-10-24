using System;
using System.Collections;
using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

/// <summary>Chỉ để có chỗ chạy coroutine, sống xuyên mọi scene.</summary>
public class SupabaseAuthRunner : MonoBehaviour { }

/// <summary>
/// Đăng nhập Supabase qua REST (không cần SDK).
///  - Học sinh: đăng nhập ẩn danh (không cần email). Phiên được lưu trên máy nên lần sau dùng lại, không tạo tài khoản mới.
///    Đổi sang học sinh khác (khác Tên hoặc Lớp) thì tạo tài khoản ẩn danh mới.
///  - Giáo viên: email + mật khẩu. Phiên chỉ giữ trong bộ nhớ, tắt game là phải đăng nhập lại (an toàn trên máy dùng chung).
/// Quyền thật sự do Supabase (RLS) kiểm tra. IsTeacher ở đây chỉ để ẩn/hiện nút trong game.
/// </summary>
public static class SupabaseAuth
{
    [Serializable] public class User { public string id; public string email; public bool is_anonymous; }
    [Serializable] public class SessionResponse { public string access_token; public string refresh_token; public int expires_in; public User user; }
    [Serializable] public class ErrorBody { public string msg; public string message; public string error_description; public string error; }
    [Serializable] public class PasswordBody { public string email; public string password; }
    [Serializable] public class RefreshBody { public string refresh_token; }
    [Serializable] public class ProfilePatch { public string display_name; public string student_class; }
    [Serializable] public class ProfileRow { public string role; public string display_name; }
    [Serializable] public class ProfileRows { public ProfileRow[] rows; }
    [Serializable]
    public class Stored
    {
        public string access, refresh, userId, email, displayName, studentClass;
        public string role = "student";
        public bool anonymous;
        public long expiresAt;   // Unix giây
    }

    class Resp { public long code; public string text; public string error; public bool ok; }

    static Stored s;
    static bool loaded, refreshing;
    static SupabaseAuthRunner runner;

    static string FilePath { get { return Path.Combine(Application.persistentDataPath, "sb_session.json"); } }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() { s = null; loaded = false; refreshing = false; runner = null; }

    // ------------------------------------------------------------ trạng thái
    public static bool SignedIn { get { Load(); return s != null; } }
    public static bool IsTeacher { get { Load(); return s != null && !s.anonymous && s.role == "teacher"; } }
    public static string UserId { get { Load(); return s != null ? s.userId : ""; } }
    public static string DisplayName { get { Load(); return s != null && s.displayName != null ? s.displayName : ""; } }
    public static string AccessToken { get { Load(); return s != null && s.access != null ? s.access : ""; } }

    static long Now() { return DateTimeOffset.UtcNow.ToUnixTimeSeconds(); }

    // ------------------------------------------------------------ lưu / nạp phiên
    static Stored ReadFile()
    {
        try
        {
            if (!File.Exists(FilePath)) return null;
            var x = JsonUtility.FromJson<Stored>(File.ReadAllText(FilePath, Encoding.UTF8));
            return x != null && !string.IsNullOrEmpty(x.refresh) && x.anonymous ? x : null;
        }
        catch (Exception e) { Debug.LogWarning("[Auth] Không đọc được phiên đã lưu: " + e.Message); return null; }
    }

    static void Load()
    {
        if (loaded) return;
        loaded = true;
        s = ReadFile();
    }

    /// <summary>Chỉ phiên ẩn danh của học sinh được ghi ra file. Phiên giáo viên không bao giờ ghi ra đĩa.</summary>
    static void Persist()
    {
        if (s == null || !s.anonymous) return;
        try { File.WriteAllText(FilePath, JsonUtility.ToJson(s), Encoding.UTF8); }
        catch (Exception e) { Debug.LogWarning("[Auth] Không lưu được phiên: " + e.Message); }
    }

    /// <summary>Đăng xuất khỏi bộ nhớ (phiên ẩn danh của học sinh trên đĩa vẫn giữ để lần sau dùng lại).</summary>
    public static void SignOut() { loaded = true; s = null; }

    // ------------------------------------------------------------ gửi request
    static SupabaseAuthRunner Host()
    {
        if (runner == null)
        {
            var go = new GameObject("SupabaseAuth");
            UnityEngine.Object.DontDestroyOnLoad(go);
            runner = go.AddComponent<SupabaseAuthRunner>();
        }
        return runner;
    }

    static IEnumerator Send(string method, string path, string json, string bearer, Resp r, string prefer = null)
    {
        using (var req = new UnityWebRequest(CloudConfig.Url.TrimEnd('/') + path, method))
        {
            if (json != null) req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
            req.downloadHandler = new DownloadHandlerBuffer();
            req.SetRequestHeader("apikey", CloudConfig.AnonKey);
            if (!string.IsNullOrEmpty(bearer)) req.SetRequestHeader("Authorization", "Bearer " + bearer);
            else if (CloudConfig.AnonKey.StartsWith("eyJ")) req.SetRequestHeader("Authorization", "Bearer " + CloudConfig.AnonKey);
            if (json != null) req.SetRequestHeader("Content-Type", "application/json");
            if (!string.IsNullOrEmpty(prefer)) req.SetRequestHeader("Prefer", prefer);
            req.timeout = 8;
            yield return req.SendWebRequest();
            r.code = req.responseCode;
            r.text = req.downloadHandler != null ? req.downloadHandler.text : "";
            r.error = req.error;
            r.ok = req.result == UnityWebRequest.Result.Success;
        }
    }

    static string Explain(Resp r)
    {
        if (r.code == 0) return "Không kết nối được máy chủ.";
        string m = null;
        try
        {
            var e = JsonUtility.FromJson<ErrorBody>(r.text);
            if (e != null) m = !string.IsNullOrEmpty(e.error_description) ? e.error_description : !string.IsNullOrEmpty(e.msg) ? e.msg : e.message;
        }
        catch { }
        if (string.IsNullOrEmpty(m)) m = r.error;
        if (m != null && m.IndexOf("Invalid login credentials", StringComparison.OrdinalIgnoreCase) >= 0) return "Sai email hoặc mật khẩu.";
        if (r.code == 429) return "Thử quá nhiều lần, hãy đợi một lát rồi thử lại.";
        return (m ?? "Lỗi không rõ") + " (" + r.code + ")";
    }

    static Stored Make(SessionResponse resp)
    {
        return new Stored
        {
            access = resp.access_token,
            refresh = resp.refresh_token,
            userId = resp.user != null ? resp.user.id : "",
            email = resp.user != null ? resp.user.email : "",
            anonymous = resp.user != null && resp.user.is_anonymous,
            expiresAt = Now() + Math.Max(60, resp.expires_in)
        };
    }

    // ------------------------------------------------------------ làm mới token
    /// <summary>Gọi trước mỗi request có đăng nhập: token sắp hết hạn thì tự làm mới.</summary>
    public static IEnumerator EnsureFresh()
    {
        Load();
        if (s == null || string.IsNullOrEmpty(s.refresh) || !CloudConfig.Configured) yield break;
        if (Now() < s.expiresAt - 60) yield break;
        if (refreshing) { while (refreshing) yield return null; yield break; }

        refreshing = true;
        var r = new Resp();
        yield return Send("POST", "/auth/v1/token?grant_type=refresh_token", JsonUtility.ToJson(new RefreshBody { refresh_token = s.refresh }), null, r);
        if (r.ok)
        {
            var resp = JsonUtility.FromJson<SessionResponse>(r.text);
            if (resp != null && !string.IsNullOrEmpty(resp.access_token))
            {
                var n = Make(resp);
                n.anonymous = s.anonymous; n.role = s.role; n.displayName = s.displayName; n.studentClass = s.studentClass;
                if (string.IsNullOrEmpty(n.userId)) n.userId = s.userId;
                s = n; Persist();
            }
        }
        else if (r.code == 400 || r.code == 401 || r.code == 403)
        {
            Debug.LogWarning("[Auth] Phiên đã hết hạn, cần đăng nhập lại: " + Explain(r));
            s = null;   // mạng lỗi thì giữ nguyên để thử lại sau
        }
        refreshing = false;
    }

    /// <summary>Gắn header cho mọi request tới Supabase (dùng trong CloudSync).</summary>
    public static void ApplyHeaders(UnityWebRequest r)
    {
        r.SetRequestHeader("apikey", CloudConfig.AnonKey);
        Load();
        if (s != null && !string.IsNullOrEmpty(s.access)) r.SetRequestHeader("Authorization", "Bearer " + s.access);
        else if (CloudConfig.AnonKey.StartsWith("eyJ")) r.SetRequestHeader("Authorization", "Bearer " + CloudConfig.AnonKey);   // khóa anon kiểu cũ (JWT)
    }

    // ------------------------------------------------------------ học sinh
    /// <summary>done(ok, lỗi). Lỗi mạng không chặn việc vào game: nơi gọi vẫn cho học sinh chơi (kết quả sẽ gửi sau).</summary>
    public static void StartStudent(string name, string cls, Action<bool, string> done)
    {
        Host().StartCoroutine(StudentRoutine(name, cls, done));
    }

    static IEnumerator StudentRoutine(string name, string cls, Action<bool, string> done)
    {
        if (!CloudConfig.Configured) { done(false, "Chưa cấu hình Supabase."); yield break; }
        Load();
        if (s != null && !s.anonymous) s = ReadFile();                                             // đang là phiên giáo viên -> bỏ, quay về phiên học sinh đã lưu
        if (s != null && (s.displayName != name || s.studentClass != cls)) s = null;               // đổi học sinh -> tài khoản ẩn danh mới
        if (s != null) yield return EnsureFresh();

        if (s == null)
        {
            var r = new Resp();
            yield return Send("POST", "/auth/v1/signup", "{}", null, r);
            if (!r.ok) { done(false, Explain(r)); yield break; }
            var resp = JsonUtility.FromJson<SessionResponse>(r.text);
            if (resp == null || string.IsNullOrEmpty(resp.access_token)) { done(false, "Máy chủ không trả về phiên đăng nhập."); yield break; }
            s = Make(resp);
            s.anonymous = true; s.role = "student"; s.displayName = name; s.studentClass = cls;
            Persist();
            Debug.Log("[Auth] Đã tạo tài khoản ẩn danh cho " + name + " (" + cls + ").");
        }

        // cập nhật Tên + Lớp vào hồ sơ để giáo viên nhìn thấy (lỗi ở đây không nghiêm trọng)
        var pr = new Resp();
        yield return Send("PATCH", "/rest/v1/profiles?id=eq." + s.userId,
            JsonUtility.ToJson(new ProfilePatch { display_name = name, student_class = cls }), s.access, pr, "return=minimal");
        if (!pr.ok) Debug.LogWarning("[Auth] Chưa cập nhật được hồ sơ: " + Explain(pr));
        done(true, null);
    }

    // ------------------------------------------------------------ giáo viên
    public static void StartTeacher(string email, string password, Action<bool, string> done)
    {
        Host().StartCoroutine(TeacherRoutine(email, password, done));
    }

    static IEnumerator TeacherRoutine(string email, string password, Action<bool, string> done)
    {
        if (!CloudConfig.Configured) { done(false, "Chưa cấu hình Supabase."); yield break; }

        var r = new Resp();
        yield return Send("POST", "/auth/v1/token?grant_type=password", JsonUtility.ToJson(new PasswordBody { email = email, password = password }), null, r);
        if (!r.ok) { done(false, Explain(r)); yield break; }
        var resp = JsonUtility.FromJson<SessionResponse>(r.text);
        if (resp == null || string.IsNullOrEmpty(resp.access_token)) { done(false, "Máy chủ không trả về phiên đăng nhập."); yield break; }

        var t = Make(resp);
        t.anonymous = false;

        // role nằm trong bảng profiles, do quản trị cấp bằng SQL
        var pr = new Resp();
        yield return Send("GET", "/rest/v1/profiles?id=eq." + t.userId + "&select=role,display_name", null, t.access, pr);
        string role = "student", dn = "";
        if (pr.ok)
        {
            try
            {
                var rows = JsonUtility.FromJson<ProfileRows>("{\"rows\":" + pr.text + "}");
                if (rows != null && rows.rows != null && rows.rows.Length > 0) { role = rows.rows[0].role; dn = rows.rows[0].display_name; }
            }
            catch (Exception e) { Debug.LogWarning("[Auth] Không đọc được hồ sơ: " + e.Message); }
        }
        if (role != "teacher") { done(false, "Tài khoản này chưa được cấp quyền giáo viên."); yield break; }

        t.role = "teacher"; t.displayName = dn;
        s = t; loaded = true;
        Debug.Log("[Auth] Giáo viên đăng nhập: " + email);
        done(true, null);
    }
}
