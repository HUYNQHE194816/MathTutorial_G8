using System.Collections.Generic;
using System.Text.RegularExpressions;
using TMPro;
using UnityEngine;

/// <summary>
/// Sinh các ký hiệu/công thức Khoa học Tự nhiên (Lý, Hóa, Sinh) trôi chậm lên trên làm nền động.
/// Ký hiệu nào font hiện tại không hiển thị được sẽ tự bị loại để tránh ô vuông lỗi.
/// Hỗ trợ rich text của TMP (chỉ số dưới/trên) nên H2O hiển thị đúng dạng H₂O.
/// </summary>
public class FloatingScienceSymbols : MonoBehaviour
{
    [SerializeField] private TMP_FontAsset font;
    [SerializeField] private int count = 22;
    [SerializeField] private Vector2 sizeRange = new Vector2(52f, 120f);
    [SerializeField] private Vector2 speedRange = new Vector2(18f, 46f);
    [SerializeField] private Vector2 alphaRange = new Vector2(0.10f, 0.24f);
    [SerializeField] private Vector2 fallbackArea = new Vector2(1920f, 1080f);

    private static readonly string[] Candidates =
    {
        // Vật lí
        "F=ma", "E=mc<sup>2</sup>", "v=s/t", "P=UI", "U=IR", "Ω", "λ", "Δ", "°C", "g", "J", "W", "N", "Hz",
        // Hóa học
        "H<sub>2</sub>O", "CO<sub>2</sub>", "O<sub>2</sub>", "NaCl", "H<sup>+</sup>", "pH", "Fe", "Cu", "Na", "C", "→",
        // Sinh học
        "ADN", "ATP", "DNA", "RNA", "O<sub>2</sub>", "CO<sub>2</sub>"
    };

    private static readonly Regex Tags = new Regex("<[^>]+>");

    private RectTransform[] items;
    private float[] baseX, speed, swayAmp, swayFreq, phase, rotSpeed;
    private Vector2 area;
    private const float Margin = 160f;

    private void Start()
    {
        var self = (RectTransform)transform;
        area = self.rect.width > 10f ? self.rect.size : fallbackArea;

        var symbols = new List<string>();
        foreach (var s in Candidates)
            if (CanRender(s)) symbols.Add(s);
        if (symbols.Count == 0) return;

        items = new RectTransform[count];
        baseX = new float[count]; speed = new float[count];
        swayAmp = new float[count]; swayFreq = new float[count];
        phase = new float[count]; rotSpeed = new float[count];

        for (int i = 0; i < count; i++)
        {
            var go = new GameObject("Symbol", typeof(RectTransform), typeof(CanvasRenderer), typeof(TextMeshProUGUI));
            var rt = (RectTransform)go.transform;
            rt.SetParent(self, false);
            rt.sizeDelta = new Vector2(480f, 220f);
            items[i] = rt;

            var tmp = go.GetComponent<TextMeshProUGUI>();
            if (font != null) tmp.font = font;
            tmp.richText = true;
            tmp.text = symbols[Random.Range(0, symbols.Count)];
            tmp.fontSize = Random.Range(sizeRange.x, sizeRange.y);
            tmp.fontStyle = FontStyles.Bold;
            tmp.alignment = TextAlignmentOptions.Center;
            tmp.textWrappingMode = TextWrappingModes.NoWrap;
            tmp.raycastTarget = false;
            tmp.color = new Color(1f, 1f, 1f, Random.Range(alphaRange.x, alphaRange.y));

            baseX[i] = Random.Range(-area.x * 0.5f, area.x * 0.5f);
            speed[i] = Random.Range(speedRange.x, speedRange.y);
            swayAmp[i] = Random.Range(10f, 40f);
            swayFreq[i] = Random.Range(0.4f, 1.1f);
            phase[i] = Random.value * Mathf.PI * 2f;
            rotSpeed[i] = Random.Range(-10f, 10f);

            // rải đều theo chiều dọc ngay từ đầu để không bị "đổ" một loạt
            float y = Random.Range(-area.y * 0.5f - Margin, area.y * 0.5f + Margin);
            rt.anchoredPosition = new Vector2(baseX[i], y);
        }
    }

    private bool CanRender(string s)
    {
        if (font == null) return true;
        foreach (char c in Tags.Replace(s, ""))
            if (!font.HasCharacter(c, true, true)) return false;
        return true;
    }

    private void Update()
    {
        if (items == null) return;

        float dt = Time.unscaledDeltaTime;
        float t = Time.unscaledTime;
        float top = area.y * 0.5f + Margin;

        for (int i = 0; i < items.Length; i++)
        {
            var rt = items[i];
            Vector2 p = rt.anchoredPosition;
            p.y += speed[i] * dt;
            if (p.y > top)
            {
                p.y = -top;
                baseX[i] = Random.Range(-area.x * 0.5f, area.x * 0.5f);
            }
            p.x = baseX[i] + Mathf.Sin(t * swayFreq[i] + phase[i]) * swayAmp[i];
            rt.anchoredPosition = p;
            rt.Rotate(0f, 0f, rotSpeed[i] * dt);
        }
    }
}
