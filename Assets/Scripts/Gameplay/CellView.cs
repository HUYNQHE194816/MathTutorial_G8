using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

[RequireComponent(typeof(Image))]
public class CellView : MonoBehaviour, IPointerClickHandler
{
    [Header("Sprites")]
    public Sprite rockSprite;   // đá chặn đường (theo ảnh mẫu)
    public Sprite dirtSprite;   // nền đất - ô đi được

    [Header("Icon con")]
    public GameObject questionMarkIcon; // icon dấu "?" màu đỏ
    public GameObject goalIcon;         // icon rương báu vật

    private Image background;
    private GridCellData data;

    void Awake()
    {
        background = GetComponent<Image>();
    }

    public void Setup(GridCellData cellData)
    {
        data = cellData;
        background.sprite = data.type == CellType.Rock ? rockSprite : dirtSprite;

        if (questionMarkIcon != null)
            questionMarkIcon.SetActive(data.type == CellType.Question && !data.isVisited);

        if (goalIcon != null)
            goalIcon.SetActive(data.type == CellType.Goal);
    }

    public void MarkVisited()
    {
        data.isVisited = true;
        if (questionMarkIcon != null)
            questionMarkIcon.SetActive(false);
    }

    public GridCellData Data => data;

    public void OnPointerClick(PointerEventData eventData)
    {
        GameManager.Instance?.OnCellClicked(data);
    }
}
