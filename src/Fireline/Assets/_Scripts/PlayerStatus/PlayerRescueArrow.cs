using UnityEngine;
using UnityEngine.UI;

/// <summary>Font-independent arrow pointing along local +X.</summary>
[RequireComponent(typeof(CanvasRenderer))]
public class PlayerRescueArrow : MaskableGraphic
{
    /// <summary>Shared screen-edge placement for teammate and mission guidance.</summary>
    public void PointTo(Camera camera, Vector3 worldPosition, bool show)
    {
        if (camera == null || !show) { gameObject.SetActive(false); return; }
        Vector3 point = camera.WorldToScreenPoint(worldPosition);
        Rect bounds = camera.pixelRect;
        bool offscreen = point.z <= 0f || !bounds.Contains(new Vector2(point.x, point.y));
        gameObject.SetActive(offscreen);
        if (!offscreen) return;
        Vector2 direction = new Vector2(point.x, point.y) - bounds.center;
        if (point.z <= 0f) direction = -direction;
        if (direction.sqrMagnitude < 0.001f) direction = Vector2.down;
        Vector2 halfSize = new Vector2(Mathf.Max(1, bounds.width / 2 - 36), Mathf.Max(1, bounds.height / 2 - 36));
        float scale = 1f / Mathf.Max(Mathf.Abs(direction.x) / halfSize.x, Mathf.Abs(direction.y) / halfSize.y);
        rectTransform.anchoredPosition = bounds.center + direction * scale;
        rectTransform.localRotation = Quaternion.Euler(0, 0, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
    }

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
