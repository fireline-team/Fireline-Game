using UnityEngine;
using UnityEngine.UI;

/// <summary>Font-independent arrow pointing along local +X.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PlayerRescueArrow : MaskableGraphic
{
    protected override void OnPopulateMesh(VertexHelper vh)
    {
        vh.Clear();
        Rect r = GetPixelAdjustedRect();
        vh.AddVert(new Vector3(r.xMax, r.center.y), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMax), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin + r.width * 0.25f, r.center.y), color, Vector2.zero);
        vh.AddVert(new Vector3(r.xMin, r.yMin), color, Vector2.zero);
        vh.AddTriangle(0, 1, 2);
        vh.AddTriangle(0, 2, 3);
    }
}
