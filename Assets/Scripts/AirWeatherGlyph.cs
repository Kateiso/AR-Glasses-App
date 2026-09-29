using UnityEngine;
using UnityEngine.UI;

// Vector strokes stay sharp in stereo; no emoji/font-dependent weather symbols.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AirWeatherGlyph : MaskableGraphic
{
    int code = -1;
    bool day = true;
    public void SetWeather(int value, bool isDay) { code = value; day = isDay; SetVerticesDirty(); }
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        if (code < 0 || AirEnvironmentData.Condition(code) == "天气未知")
        { Line(vh, new Vector2(30, 50), new Vector2(70, 50)); return; }
        if (code <= 2)
        {
            if (day)
            {
                Circle(vh, 45, 63, 16, 0, 360);
                for (int i = 0; i < 8; i++)
                {
                    float a = i * Mathf.PI / 4;
                    Vector2 d = new Vector2(Mathf.Cos(a), Mathf.Sin(a));
                    Line(vh, new Vector2(45, 63) + d * 23, new Vector2(45, 63) + d * 29);
                }
            }
            else { Circle(vh, 45, 63, 24, 70, 300); Line(vh, new Vector2(57, 42), new Vector2(40, 56)); Line(vh, new Vector2(40, 56), new Vector2(53, 85)); }
            if (code == 0) return;
        }
        Circle(vh, 27, 40, 13, 90, 270);
        Circle(vh, 47, 47, 19, 0, 170);
        Circle(vh, 72, 39, 12, -90, 100);
        Line(vh, new Vector2(27, 27), new Vector2(72, 27));
        if (code == 45 || code == 48)
        { Line(vh, new Vector2(22, 18), new Vector2(78, 18)); Line(vh, new Vector2(32, 9), new Vector2(68, 9)); }
        else if ((code >= 71 && code <= 77) || code == 85 || code == 86)
        {
            for (int i = 0; i < 3; i++) { float x = 30 + i * 20; Line(vh, new Vector2(x - 4, 12), new Vector2(x + 4, 12)); Line(vh, new Vector2(x, 8), new Vector2(x, 16)); }
        }
        else if (code >= 95)
        { Line(vh, new Vector2(53, 26), new Vector2(44, 15)); Line(vh, new Vector2(44, 15), new Vector2(55, 15)); Line(vh, new Vector2(55, 15), new Vector2(46, 3)); }
        else if (code >= 51)
            for (int i = 0; i < 3; i++) Line(vh, new Vector2(34 + i * 18, 18), new Vector2(29 + i * 18, 7));
    }
    void Circle(VertexHelper vh, float x, float y, float r, float start, float end)
    {
        Vector2 last = new Vector2(x, y) + new Vector2(Mathf.Cos(start * Mathf.Deg2Rad), Mathf.Sin(start * Mathf.Deg2Rad)) * r;
        for (int i = 1; i <= 32; i++)
        {
            float a = Mathf.Lerp(start, end, i / 32f) * Mathf.Deg2Rad;
            Vector2 next = new Vector2(x, y) + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * r;
            Line(vh, last, next); last = next;
        }
    }
    void Line(VertexHelper vh, Vector2 a, Vector2 b)
    {
        Vector2 size = rectTransform.rect.size;
        a = Vector2.Scale(a / 100f - Vector2.one * 0.5f, size);
        b = Vector2.Scale(b / 100f - Vector2.one * 0.5f, size);
        Vector2 n = new Vector2(-(b - a).y, (b - a).x).normalized * 1.4f;
        int at = vh.currentVertCount;
        vh.AddVert(a - n, color, Vector2.zero); vh.AddVert(a + n, color, Vector2.zero);
        vh.AddVert(b + n, color, Vector2.zero); vh.AddVert(b - n, color, Vector2.zero);
        vh.AddTriangle(at, at + 1, at + 2); vh.AddTriangle(at, at + 2, at + 3);
    }
}
