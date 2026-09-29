using UnityEngine;
using UnityEngine.UI;

// Small rounded UI surface without an additional texture or material.
[RequireComponent(typeof(CanvasRenderer))]
public sealed class AirSoftPanel : MaskableGraphic
{
    public float Radius = 18;
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear(); var r = rectTransform.rect;
        float radius = Mathf.Min(Radius, Mathf.Min(r.width, r.height) / 2);
        vh.AddVert(r.center, color, Vector2.zero);
        for (int corner = 0; corner < 4; corner++) {
            var center = new Vector2(corner == 0 || corner == 3 ? r.xMax - radius : r.xMin + radius,
                corner < 2 ? r.yMax - radius : r.yMin + radius);
            for (int n = 0; n <= 6; n++) {
                float a = (corner * 90 + n * 15) * Mathf.Deg2Rad;
                vh.AddVert(center + new Vector2(Mathf.Cos(a), Mathf.Sin(a)) * radius, color, Vector2.zero);
            }
        }
        for (int i = 1; i <= 28; i++) vh.AddTriangle(0, i, i == 28 ? 1 : i + 1);
    }
}
