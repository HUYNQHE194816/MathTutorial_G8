    using UnityEngine;
using UnityEngine.SceneManagement;

public class MainMenuManager : MonoBehaviour
{
    [SerializeField] private GameObject settingsPanel;

    public void OnClickStart()
    {
        SceneManager.LoadScene("GameplayScene");
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