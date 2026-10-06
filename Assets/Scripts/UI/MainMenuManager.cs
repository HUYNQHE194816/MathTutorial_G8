using TMPro;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Điều khiển scene MainMenu: Bắt đầu / Tiến độ / Cài đặt / Thoát.
/// Panel Cài đặt có nút bật/tắt âm thanh (lưu trong PlayerPrefs) và nút Đóng.
/// </summary>
public class MainMenuManager : MonoBehaviour
{
    private const string MutedKey = "KHTN_Muted";

    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private TMP_Text soundLabel;
    [SerializeField] private AnimatedButton startButtonFx;

    private bool muted;

    private void Start()
    {
        muted = PlayerPrefs.GetInt(MutedKey, 0) == 1;
        ApplySound();

        if (settingsPanel != null) settingsPanel.SetActive(false);
        if (startButtonFx != null) startButtonFx.Pulse = true;   // nút Bắt đầu đập nhịp nhẹ để gọi chú ý
    }

    public void OnClickStart()
    {
        SceneManager.LoadScene("SubjectSelectScene");
    }

    public void OnClickDashboard()
    {
        if (!Application.CanStreamedLevelBeLoaded("DashboardScene"))
        {
            Debug.LogError("[MainMenu] DashboardScene chưa có trong Build Settings (Tools > KHTN 8 > Tạo DashboardScene).");
            return;
        }
        SceneManager.LoadScene("DashboardScene");
    }

    public void OnClickSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(true);
    }

    public void OnClickCloseSettings()
    {
        if (settingsPanel != null) settingsPanel.SetActive(false);
    }

    public void OnClickToggleSound()
    {
        muted = !muted;
        PlayerPrefs.SetInt(MutedKey, muted ? 1 : 0);
        PlayerPrefs.Save();
        ApplySound();
    }

    public void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void ApplySound()
    {
        AudioListener.volume = muted ? 0f : 1f;
        if (soundLabel != null) soundLabel.text = muted ? "ÂM THANH: TẮT" : "ÂM THANH: BẬT";
    }
}
