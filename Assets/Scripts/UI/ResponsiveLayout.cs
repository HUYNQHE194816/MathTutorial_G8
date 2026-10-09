using UnityEngine;
using UnityEngine.UI;

public class ResponsiveLayout : MonoBehaviour
{
    [Header("Layout & Panel Settings")]
    [SerializeField] private RectTransform mainContainer;
    
    [Header("Landscape Config (Ngang - PC/Tablet)")]
    [SerializeField] private Vector2 landscapeReference = new Vector2(1920, 1080);
    
    [Header("Portrait Config (Dọc - Phone)")]
    [SerializeField] private Vector2 portraitReference = new Vector2(1080, 1920);

    private bool isLandscape;

    void Start()
    {
        CheckAndApplyLayout();
    }

    void Update()
    {
        // Tự động kiểm tra mỗi khi kích thước màn hình thay đổi (xoay điện thoại hoặc co giãn cửa sổ PC)
        bool currentIsLandscape = Screen.width > Screen.height;
        if (currentIsLandscape != isLandscape)
        {
            CheckAndApplyLayout();
        }
    }

    public void CheckAndApplyLayout()
    {
        isLandscape = Screen.width > Screen.height;

        if (mainContainer != null)
        {
            // Tự cập nhật lại layout theo hướng màn hình hiện tại
            LayoutRebuilder.ForceRebuildLayoutImmediate(mainContainer);
        }

        Canvas.ForceUpdateCanvases();
    }
}
