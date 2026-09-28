using UnityEngine;

// Chuột phải trong Project window -> Create -> MathTutorial -> Level Config
// để tạo mỗi màn chơi thành 1 asset riêng (Level1, Level2, Level3...),
// rồi kéo vào field "Config" của MazeGridGenerator trong từng scene/prefab.
[CreateAssetMenu(menuName = "MathTutorial/Level Config", fileName = "LevelConfig")]
public class LevelConfig : ScriptableObject
{
    [Header("Thông tin màn")]
    public string levelName = "Level 1";

    [Header("Kích thước lưới")]
    public int rows = 6;
    public int columns = 6;

    [Header("Độ khó")]
    [Range(0, 100)] public int questionRatioOnPath = 70;
    [Range(0, 100)] public int offPathDirtPercent = 55;
    [Range(0, 100)] public int offPathQuestionPercent = 25;

    [Header("Luật chơi")]
    public float timeLimitSeconds = 20 * 60f;
    public int pointsPerCorrectAnswer = 10;
}
