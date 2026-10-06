using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Trung tâm âm thanh: tự khởi tạo khi chạy game (không cần đặt vào scene), sống xuyên các scene.
/// Nhạc nền tự đổi theo tên scene; hiệu ứng gọi bằng GameAudio.Play(Snd.X).
/// Âm thanh lấy từ Resources/GameAudioLibrary.asset - ô nào chưa gán clip thì không phát.
/// </summary>
public class GameAudio : MonoBehaviour
{
    const int Voices = 16;
    static GameAudio inst;
    static GameAudioLibrary lib;

    readonly AudioSource[] mus = new AudioSource[2];
    readonly float[] musTarget = new float[2];
    int cur;
    AudioSource[] pool; AudioSource jingle; int next;
    readonly Dictionary<int, float> lastPlay = new Dictionary<int, float>();

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        if (inst != null) return;
        lib = Resources.Load<GameAudioLibrary>("GameAudioLibrary");
        if (lib == null)
        {
            Debug.LogWarning("[GameAudio] Chưa có Assets/Resources/GameAudioLibrary.asset (vào Tools > KHTN 8 > Âm thanh > Mở bảng âm thanh để tạo). Game sẽ chạy không có âm thanh.");
            lib = ScriptableObject.CreateInstance<GameAudioLibrary>(); lib.Sync();
        }
        var go = new GameObject("GameAudio"); DontDestroyOnLoad(go); inst = go.AddComponent<GameAudio>();
        AudioListener.volume = PlayerPrefs.GetInt("KHTN_Muted", 0) == 1 ? 0f : 1f;   // cùng khoá với nút Âm thanh ở MainMenu
    }

    void Awake()
    {
        for (int i = 0; i < 2; i++) mus[i] = NewSource(true);
        pool = new AudioSource[Voices]; for (int i = 0; i < Voices; i++) pool[i] = NewSource(false);
        jingle = NewSource(false);
        SceneManager.sceneLoaded += OnScene;
    }

    void Start() { OnScene(SceneManager.GetActiveScene(), LoadSceneMode.Single); }
    void OnDestroy() { SceneManager.sceneLoaded -= OnScene; if (inst == this) inst = null; }

    AudioSource NewSource(bool loop)
    {
        var s = gameObject.AddComponent<AudioSource>(); s.playOnAwake = false; s.loop = loop; s.spatialBlend = 0f; return s;
    }

    // ---------- Nhạc nền theo scene ----------
    void OnScene(Scene s, LoadSceneMode mode)
    {
        string n = s.name;
        if (n == "LoginScene" || n == "MainMenu" || n == "DashboardScene") SetMusic(Mus.Menu);
        else if (n == "SubjectSelectScene" || n.StartsWith("ChapterSelectScene") || n == "LessonSelectScene" || n == "BiologyBossScene") SetMusic(Mus.Select);   // boss: nhạc chọn cho tới khi "VÀO TRẬN"
        else if (n == "GameplayScene") SetMusic(Mus.Ly);
        else if (n == "MillionareScene") SetMusic(Mus.None);   // Hóa tự phát nhạc qua MillionaireManager
    }

    public static void PlayMusic(Mus m) { if (inst != null) inst.SetMusic(m); }
    public static void StopMusic() { if (inst != null) inst.SetMusic(Mus.None); }

    void SetMusic(Mus m)
    {
        AudioClip clip = null; float vol = 1f;
        if (m != Mus.None)
        {
            var s = lib.GetMusic(m);
            if ((s == null || s.clip == null) && m == Mus.Select) s = lib.GetMusic(Mus.Menu);   // chưa có nhạc chọn: giữ nhạc menu
            if (s != null && s.clip != null) { clip = s.clip; vol = s.volume; }
        }
        if (clip != null && mus[cur].clip == clip && musTarget[cur] > 0f) { musTarget[cur] = vol; return; }   // đang phát đúng bài này rồi
        musTarget[cur] = 0f;                                                                                  // bài cũ nhỏ dần
        if (clip == null) return;
        cur = 1 - cur;
        var src = mus[cur]; src.clip = clip; src.volume = 0f; src.Play(); musTarget[cur] = vol;
    }

    void Update()
    {
        float dt = Time.unscaledDeltaTime, step = dt / Mathf.Max(.05f, lib.musicFade);
        for (int i = 0; i < 2; i++)
        {
            var s = mus[i]; if (s.clip == null) continue;
            s.volume = Mathf.MoveTowards(s.volume, musTarget[i] * lib.musicVolume, step);
            if (musTarget[i] <= 0f && s.volume <= .001f) { s.Stop(); s.clip = null; }
        }
    }

    // ---------- Hiệu ứng ----------
    /// <summary>Có clip được gán cho ô này không?</summary>
    public static bool Has(Snd id) { var s = lib != null ? lib.GetSfx(id) : null; return s != null && s.clip != null; }

    /// <summary>Phát hiệu ứng. Trả về true nếu ô này có clip (kể cả khi bị bỏ qua vì phát dồn dập), false nếu trống.</summary>
    public static bool Play(Snd id) { return inst != null && id != Snd.None && inst.PlayInternal(id); }

    bool PlayInternal(Snd id)
    {
        var slot = lib.GetSfx(id);
        if (slot == null || slot.clip == null) return false;
        float t = Time.unscaledTime; int k = (int)id;
        if (lastPlay.TryGetValue(k, out float lt) && t - lt < .04f) return true;   // chống chồng tiếng trong cùng một khung hình
        lastPlay[k] = t;

        bool end = id == Snd.Victory || id == Snd.Defeat;
        var s = end ? jingle : pool[next]; if (!end) next = (next + 1) % pool.Length;
        s.pitch = end ? 1f : 1f + Random.Range(-lib.pitchJitter, lib.pitchJitter);
        s.volume = lib.sfxVolume * slot.volume; s.clip = slot.clip; s.Play();
        return true;
    }
}
