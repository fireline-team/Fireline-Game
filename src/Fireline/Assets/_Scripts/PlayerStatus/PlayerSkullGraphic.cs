using UnityEngine;
using UnityEngine.UI;

/// <summary>Small vector pixel-art skull, independent of font glyph support.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PlayerSkullGraphic : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Quad(vh, 2, 5, 12, 9, color);
        Quad(vh, 1, 7, 14, 5, color);
        Quad(vh, 4, 2, 8, 4, color);
        Quad(vh, 3, 8, 3, 3, Color.black);
        Quad(vh, 10, 8, 3, 3, Color.black);
        Quad(vh, 7, 6, 2, 2, Color.black);
        Quad(vh, 6, 2, 1, 2, Color.black);
        Quad(vh, 9, 2, 1, 2, Color.black);
    }

    private void Quad(VertexHelper vh, float x, float y, float width, float height, Color tint)
    {
        Rect r = GetPixelAdjustedRect();
        int start = vh.currentVertCount;
        Vector2 min = r.min + new Vector2(x * r.width / 16, y * r.height / 16);
        Vector2 max = min + new Vector2(width * r.width / 16, height * r.height / 16);
        vh.AddVert(new Vector3(min.x, min.y), tint, Vector2.zero);
        vh.AddVert(new Vector3(min.x, max.y), tint, Vector2.zero);
        vh.AddVert(new Vector3(max.x, max.y), tint, Vector2.zero);
        vh.AddVert(new Vector3(max.x, min.y), tint, Vector2.zero);
        vh.AddTriangle(start, start + 1, start + 2);
        vh.AddTriangle(start, start + 2, start + 3);
    }
}
