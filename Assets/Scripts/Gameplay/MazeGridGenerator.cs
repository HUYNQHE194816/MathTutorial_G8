using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

public class MazeGridGenerator : MonoBehaviour
{
    [Header("Kích thước lưới")]
    public int rows = 6;
    public int columns = 6;

    [Header("Prefab & container")]
    public GameObject cellPrefab;
    public Transform gridContainer;

    [Header("Điểm bắt đầu / đích")]
    public Vector2Int start = new Vector2Int(0, 0);
    public Vector2Int goal;

    [Header("Số lượng cố định")]
    [Tooltip("> 0: map có ĐÚNG số ô câu hỏi này (bỏ qua các % độ khó bên dưới). Đặt 0 để dùng random theo %.")]
    public int fixedQuestionCount = 15;
    [Tooltip("Số ô đá chặn đường cố định (chỉ dùng khi Fixed Question Count > 0).")]
    public int fixedRockCount = 15;
    [Tooltip("Lưới được chia thành các khối vuông cỡ này; câu hỏi/đá được rải lần lượt qua TỪNG khối nên phân bố đều toàn map.")]
    [Min(1)] public int blockSize = 2;

    [Header("Cấu hình màn chơi (tùy chọn)")]
    [Tooltip("Nếu gán, các giá trị bên dưới sẽ bị LevelConfig này ghi đè lúc Awake().")]
    public LevelConfig config;

    [Header("Độ khó - đường đi chính (bắt buộc phải đi qua để tới rương)")]
    [Range(0, 100)]
    [Tooltip("% số ô trên đường đi chính (không tính ô xuất phát) bắt buộc là câu hỏi.")]
    public int questionRatioOnPath = 70;

    [Header("Độ khó - ô ngoài đường đi chính (bẫy / nhánh phụ)")]
    [Range(0, 100)] public int offPathDirtPercent = 55;
    [Range(0, 100)] public int offPathQuestionPercent = 25; // phần còn lại tự là Rock

    private GridCellData[,] cellData;
    private CellView[,] cellViews;
    private System.Random rnd;

    public Vector2Int StartPosition => start;
    public int Rows => rows;
    public int Columns => columns;

    void Awake()
    {
        ApplyConfig();

        if (goal == Vector2Int.zero)
            goal = new Vector2Int(rows - 1, columns - 1);

        rnd = new System.Random(System.Guid.NewGuid().GetHashCode()); // random thật mỗi lần vào scene

        cellData = new GridCellData[rows, columns];
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
                cellData[r, c] = new GridCellData(r, c);

        if (fixedQuestionCount > 0)
        {
            BuildBalancedLayout();          // đúng N câu hỏi + M đá, rải ĐỀU toàn map
        }
        else
        {
            List<Vector2Int> mainPath = BuildMainPath();
            var pathSet = new HashSet<Vector2Int>(mainPath);
            AssignPathTypes(mainPath);      // ép tỉ lệ Question trên đường đi chính
            FillOffPathCells(pathSet);      // random Dirt/Question/Rock cho phần còn lại
            AddExtraConnections(pathSet);   // mở thêm vài lối rẽ phụ cho đỡ nhàm
        }

        cellData[goal.x, goal.y].type = CellType.Goal;

        BuildView();
    }

    // Map cân bằng: chia lưới thành các khối blockSize x blockSize rồi rải câu hỏi / đá lần lượt
    // qua TỪNG khối (round-robin, khối đang ít ô đặc biệt nhất được ưu tiên) nên mỗi khu vực chỉ
    // chênh nhau tối đa 1 ô -> không bao giờ dồn về một phía. Ô còn lại là đất (cũng đều theo).
    // Nếu bố cục làm rương bị chặn kín bởi đá thì rải lại (thường thành công ngay lần đầu).
    void BuildBalancedLayout()
    {
        for (int attempt = 0; attempt < 300; attempt++)
        {
            for (int r = 0; r < rows; r++)
                for (int c = 0; c < columns; c++)
                    cellData[r, c] = new GridCellData(r, c);
            cellData[goal.x, goal.y].type = CellType.Goal;

            ScatterEvenly(fixedQuestionCount, CellType.Question);
            ScatterEvenly(fixedRockCount, CellType.Rock);

            if (HasPathToGoal(start)) return;
        }
        Debug.LogWarning("[Maze] Không tạo được bố cục còn đường tới rương sau 300 lần thử — kiểm tra lại số đá / kích thước lưới.");
    }

    class Block { public List<Vector2Int> free = new List<Vector2Int>(); public int used; }

    void ScatterEvenly(int count, CellType type)
    {
        int bs = Mathf.Max(1, blockSize);
        var map = new Dictionary<Vector2Int, Block>();
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                var p = new Vector2Int(r, c);
                if (p == start || p == goal) continue;
                var key = new Vector2Int(r / bs, c / bs);
                if (!map.TryGetValue(key, out var b)) map[key] = b = new Block();
                if (cellData[r, c].type == CellType.Dirt) b.free.Add(p); else b.used++;
            }

        var blocks = map.Values.ToList();
        int placed = 0;
        while (placed < count && blocks.Any(b => b.free.Count > 0))
        {
            foreach (var b in blocks.OrderBy(b => b.used).ThenBy(_ => rnd.Next()).ToList())
            {
                if (placed >= count) break;
                if (b.free.Count == 0) continue;
                int i = rnd.Next(b.free.Count);
                var p = b.free[i];
                b.free.RemoveAt(i);
                cellData[p.x, p.y].type = type;
                b.used++;
                placed++;
            }
        }
        if (placed < count)
            Debug.LogWarning($"[Maze] Lưới {rows}x{columns} không đủ chỗ cho {count} ô {type} (chỉ đặt được {placed}).");
    }

    void ApplyConfig()
    {
        if (config == null) return;
        rows = config.rows;
        columns = config.columns;
        questionRatioOnPath = config.questionRatioOnPath;
        offPathDirtPercent = config.offPathDirtPercent;
        offPathQuestionPercent = config.offPathQuestionPercent;
    }

    // ---- SỬA CỐT LÕI #1 ----
    // Trước đây: đường đi bảo đảm chỉ đi phải/xuống (staircase), rất dễ đoán.
    // Giờ: random walk có backtrack, đi được cả 4 hướng, tạo cảm giác mê cung thật,
    // vẫn LUÔN tìm ra đường vì lưới ở bước này chưa có vật cản.
    List<Vector2Int> BuildMainPath()
    {
        var path = new List<Vector2Int> { start };
        var visited = new HashSet<Vector2Int> { start };
        var current = start;
        int guard = rows * columns * 8; // an toàn tuyệt đối, tránh vòng lặp vô hạn nếu có lỗi

        while (current != goal && guard-- > 0)
        {
            Vector2Int? next = PickNextStep(current, visited);
            if (next.HasValue)
            {
                visited.Add(next.Value);
                path.Add(next.Value);
                current = next.Value;
            }
            else if (path.Count > 1)
            {
                // Ngõ cụt tạm thời -> lùi lại 1 ô, thử hướng khác
                path.RemoveAt(path.Count - 1);
                current = path[path.Count - 1];
            }
            else
            {
                break; // không thể xảy ra trên lưới liền mạch, chỉ phòng hờ
            }
        }
        return path;
    }

    Vector2Int? PickNextStep(Vector2Int from, HashSet<Vector2Int> visited)
    {
        var dirs = new[] { Vector2Int.up, Vector2Int.down, Vector2Int.left, Vector2Int.right };
        var candidates = dirs
            .Select(d => from + d)
            .Where(p => InBounds(p) && !visited.Contains(p))
            // thiên nhẹ về phía đích để đường không quá dài, vẫn đủ ngẫu nhiên để không đi thẳng tuột
            .OrderBy(p => Vector2Int.Distance(p, goal) - rnd.NextDouble() * 2.5)
            .ToList();

        return candidates.Count > 0 ? candidates[0] : (Vector2Int?)null;
    }

    bool InBounds(Vector2Int p) => p.x >= 0 && p.x < rows && p.y >= 0 && p.y < columns;

    // ---- SỬA CỐT LÕI #2 ----
    // Trước đây: ô trên path chỉ đổi từ Rock -> Dirt/Question, còn ô vốn đã random ra Dirt
    // thì GIỮ NGUYÊN Dirt -> có thể toàn bộ đường đi chỉ là Dirt, không phải trả lời câu hỏi nào.
    // Giờ: ép cứng theo tỉ lệ questionRatioOnPath, không phụ thuộc random % ban đầu nữa.
    void AssignPathTypes(List<Vector2Int> path)
    {
        var middle = path.Skip(1).ToList(); // bỏ ô xuất phát, ô goal sẽ bị set riêng sau
        int questionCount = Mathf.CeilToInt(middle.Count * (questionRatioOnPath / 100f));

        var questionCells = new HashSet<Vector2Int>(
            middle.OrderBy(_ => rnd.Next()).Take(questionCount));

        foreach (var p in path)
            cellData[p.x, p.y].type = questionCells.Contains(p) ? CellType.Question : CellType.Dirt;

        cellData[start.x, start.y].type = CellType.Dirt; // ô xuất phát luôn an toàn
    }

    void FillOffPathCells(HashSet<Vector2Int> pathSet)
    {
        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                var p = new Vector2Int(r, c);
                if (pathSet.Contains(p)) continue;

                int roll = rnd.Next(0, 100);
                cellData[r, c].type = roll < offPathDirtPercent ? CellType.Dirt
                                     : roll < offPathDirtPercent + offPathQuestionPercent ? CellType.Question
                                     : CellType.Rock;
            }
    }

    void AddExtraConnections(HashSet<Vector2Int> pathSet)
    {
        int extra = (rows * columns) / 6; // mở thêm ~16% số ô làm lối rẽ phụ
        for (int i = 0; i < extra; i++)
        {
            int r = rnd.Next(rows);
            int c = rnd.Next(columns);
            var p = new Vector2Int(r, c);
            if (pathSet.Contains(p)) continue;
            if (cellData[r, c].type == CellType.Rock)
                cellData[r, c].type = rnd.Next(0, 100) < 60 ? CellType.Dirt : CellType.Question;
        }
    }

    void BuildView()
    {
        cellViews = new CellView[rows, columns];
        var grid = gridContainer.GetComponent<GridLayoutGroup>();
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = columns;

        for (int r = 0; r < rows; r++)
            for (int c = 0; c < columns; c++)
            {
                GameObject go = Instantiate(cellPrefab, gridContainer);
                CellView view = go.GetComponent<CellView>();
                view.Setup(cellData[r, c]);
                cellViews[r, c] = view;
            }

        // Bắt buộc GridLayoutGroup sắp xếp vị trí các ô NGAY LẬP TỨC,
        // nếu không PlayerController.Start() sẽ đọc vị trí ô xuất phát
        // khi lưới chưa kịp layout xong (chạy trước 1 frame), khiến
        // nhân vật bị đặt lệch (thường rơi vào giữa màn hình).
        LayoutRebuilder.ForceRebuildLayoutImmediate(gridContainer.GetComponent<RectTransform>());
    }

    public GridCellData GetCell(int row, int col) => cellData[row, col];
    public CellView GetCellView(int row, int col) => cellViews[row, col];

    // BFS: còn đường tới rương từ vị trí 'from' hay không (gọi sau mỗi lần 1 ô biến thành đá)
    public bool HasPathToGoal(Vector2Int from)
    {
        var visited = new bool[rows, columns];
        var queue = new Queue<Vector2Int>();
        queue.Enqueue(from);
        visited[from.x, from.y] = true;
        Vector2Int[] dirs = { new Vector2Int(1, 0), new Vector2Int(-1, 0), new Vector2Int(0, 1), new Vector2Int(0, -1) };

        while (queue.Count > 0)
        {
            var p = queue.Dequeue();
            if (p == goal) return true;
            foreach (var d in dirs)
            {
                var n = p + d;
                if (n.x < 0 || n.x >= rows || n.y < 0 || n.y >= columns) continue;
                if (visited[n.x, n.y]) continue;
                if (cellData[n.x, n.y].type == CellType.Rock) continue;
                visited[n.x, n.y] = true;
                queue.Enqueue(n);
            }
        }
        return false;
    }
}