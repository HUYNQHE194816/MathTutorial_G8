    using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;

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
        settingsPanel.SetActive(true);
    }

    public void OnClickCloseSettings()
    {
        settingsPanel.SetActive(false);
    }

    public void OnClickExit()
    {
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}