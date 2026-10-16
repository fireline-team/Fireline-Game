using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A solid thing that stands in the world and blocks movement and water: a wall segment,
/// a building, the firetruck, a barricade. Its parts:
///
///   Root (this)  - flat. Holds the footprint collider. Its position is the MIDDLE OF THE
///                  FRONT EDGE of the footprint (the edge nearest the bottom of the screen).
///   Front        - the upright face, with StandUpright.
///   Top          - optional flat roof/top surface, with HeightAboveGround. This script raises
///                  it to exactly the height of the Front sprite, so the two always line up.
///   Side Left /  - optional side faces, so the perspective camera doesn't see a gap down the
///   Side Right     side of the building. Each is a parent object holding a "Sprite" child; this
///                  script skews the parent so the side joins the front's edge to the roof's edge.
///                  Only the side the camera can see is drawn.
///
/// The collider, layer, top/side placement and draw order are set from the Footprint, so the
/// only things to fill in by hand are the sprites. Create one with
/// Fireline > Tilted View > Create Structure, or add sides to existing ones with
/// Fireline > Tilted View > Add Side Faces To Selected.
///
/// Side art: draw it as the building's RIGHT side seen head-on, with the front edge on the
/// LEFT of the image, the back edge on the right, and the ground at the bottom. Put the same
/// sprite on both sides; the left side shows it mirrored automatically.
///
/// Keep the root's scale at (1, 1, 1); size things with Footprint and Height instead.
///
/// Walls: use one-cell segments (footprint 1 x 1) so draw order stays correct along long walls.
/// Buildings: one Structure with a footprint matching the building's base.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(SortingGroup))]
public class Structure : MonoBehaviour
{
    /// <summary>Layer that blocks hose water. Create it in Project Settings > Tags and Layers.</summary>
    public const string ObstacleLayer = "Obstacles";

    public enum SpriteFit
    {
        /// <summary>Leave the Front and Top sprites alone; size them yourself.</summary>
        None,
        /// <summary>Stretch the whole image to fit (no Transform scaling, no repeating).</summary>
        Stretch,
        /// <summary>Repeat the image to fill (good for brick walls, roof tiles).</summary>
        Tile
    }

    [Tooltip("Ground area this blocks, in world units. X = width, Y = depth going up the screen. Usually whole grid cells.")]
    [SerializeField] private Vector2 footprint = Vector2.one;
    [Tooltip("How tall the front face stands, in world units. 0 = keep the Front sprite's current height.")]
    [SerializeField, Min(0f)] private float height = 0f;
    [Tooltip("The upright front face (should have StandUpright).")]
    [SerializeField] private SpriteRenderer front;
    [Tooltip("Optional flat top surface. It's raised to the top of the Front sprite automatically.")]
    [SerializeField] private HeightAboveGround top;
    [Tooltip("How the sprites fill the footprint. Stretch scales the whole image; Tile repeats it. " +
             "Stretch also respects 9-slice borders if the sprite has them, so corners/edges won't distort.")]
    [SerializeField] private SpriteFit spriteFit = SpriteFit.Stretch;

    [Header("Side Faces")]
    [Tooltip("Optional. The Sprite child of the \"Side Left\" object (Add Side Faces sets this up). " +
             "It needs its own parent object because the script skews that parent.")]
    [SerializeField] private SpriteRenderer leftSide;
    [Tooltip("Optional. The Sprite child of the \"Side Right\" object.")]
    [SerializeField] private SpriteRenderer rightSide;
    [Tooltip("Only draw the side the camera can actually see. Leave on: drawing the far side too would " +
             "show it through the building. (With an orthographic camera, both sides are edge-on and hidden.)")]
    [SerializeField] private bool hideSideFacingAway = true;

    private float _frontHeight;

    public Vector2 Footprint => footprint;

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif

    public void Apply()
    {
        // The collider covers the footprint, extending back (up the screen) from the front edge.
        BoxCollider2D box = GetComponent<BoxCollider2D>();
        box.isTrigger = false;
        box.size = footprint;
        box.offset = new Vector2(0f, footprint.y * 0.5f);

        int layer = LayerMask.NameToLayer(ObstacleLayer);
        if (layer >= 0) gameObject.layer = layer;
#if UNITY_EDITOR
        else Debug.LogWarning($"{name}: there's no \"{ObstacleLayer}\" layer, so it won't block water. Add it in Project Settings > Tags and Layers.", this);
#endif

        if (front == null || front.sprite == null) return;

        // Front face: as wide as the footprint, and Height tall.
        if (spriteFit != SpriteFit.None)
        {
            float frontHeight = height > 0f ? height : DrawnHeight(front);
            FitSprite(front, new Vector2(footprint.x, frontHeight), spriteFit);
            // Feet placement depends on the drawn size, so re-seat the front on the ground.
            StandUpright upright = front.GetComponent<StandUpright>();
            if (upright != null) upright.Apply();
        }
        _frontHeight = DrawnHeight(front);

        // The SortingGroup draws all the faces together, sorted by this root's position
        // (the front edge). Without it, a player behind the wall could be drawn over its top.
        if (top != null)
        {
            top.GroundOffset = new Vector2(0f, footprint.y * 0.5f);
            top.Height = _frontHeight;

            // Roof: covers the whole footprint.
            SpriteRenderer topSprite = top.GetComponent<SpriteRenderer>();
            if (spriteFit != SpriteFit.None && topSprite != null && topSprite.sprite != null)
                FitSprite(topSprite, footprint, spriteFit);
        }

        ApplySide(leftSide, -1f);
        ApplySide(rightSide, +1f);
        UpdateSideVisibility();
    }

    private void LateUpdate() => UpdateSideVisibility();

    // ---- Side faces -----------------------------------------------------------------------
    //
    // The front leans back with the camera tilt and the roof sits on top of that lean, so a side
    // face is a parallelogram, not a rectangle: one edge runs back along the ground (depth), the
    // other leans up along TiltedView.Up (height). Transforms can't skew directly, but a parent
    // with non-uniform scale plus a rotated child can: the 2x2 skew is split into
    // rotation * scale * rotation (an SVD), with the first rotation and scale on the parent and
    // the second rotation on the child. The sprite then draws at its normal Size (depth x height),
    // so Stretch and Tile behave exactly like on the front.

    private void ApplySide(SpriteRenderer side, float sign)
    {
        if (side == null) return;

        Transform skew = side.transform.parent;
        if (skew == null || skew == transform)
        {
            Debug.LogWarning($"{name}: side face \"{side.name}\" needs its own parent object under the structure " +
                             "(use Fireline > Tilted View > Add Side Faces To Selected).", side);
            return;
        }

        // Match the front's draw order so the faces sort together.
        side.sortingLayerID = front.sortingLayerID;
        side.sortingOrder = front.sortingOrder - 1;
        if (side.sprite == null) return;

        // Size in the sprite's own units: the skew below maps 1 local unit to 1 world unit
        // along both edges, so no scale compensation (lossyScale is meaningless under skew).
        var size = new Vector2(footprint.y, _frontHeight);
        side.drawMode = spriteFit == SpriteFit.Tile ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;
        side.size = size;

        // The plane of this side: e1 runs back along the ground, e2 is perpendicular to it
        // within the plane, leaning toward the camera.
        Vector3 depthDir = transform.up;
        Vector3 up = TiltedView.Up;
        Vector3 e2 = (up - Vector3.Dot(up, depthDir) * depthDir).normalized;
        Quaternion frame = Quaternion.LookRotation(Vector3.Cross(depthDir, e2), e2); // x = depth, y = e2

        // In that plane, local x -> depth (1, 0) and local y -> up (a, b).
        float a = Vector3.Dot(up, depthDir), b = Vector3.Dot(up, e2);
        Svd2(1f, a, 0f, b, out float phi, out float sx, out float sy, out float theta);

        skew.rotation = frame * Quaternion.Euler(0f, 0f, phi * Mathf.Rad2Deg);
        float rootScale = Mathf.Max(Mathf.Abs(transform.lossyScale.x), 0.0001f);
        skew.localScale = new Vector3(sx, sy, 1f) / rootScale;

        Transform t = side.transform;
        t.localRotation = Quaternion.Euler(0f, 0f, theta * Mathf.Rad2Deg);
        t.localPosition = Vector3.zero;
        t.localScale = Vector3.one;

        // Put the sprite's front-bottom corner on the footprint's front corner. That corner sits
        // at -pivot * size in the sprite's local space, which maps to world as x*depth + y*up.
        Vector2 pivot01 = new Vector2(side.sprite.pivot.x / side.sprite.rect.width,
                                      side.sprite.pivot.y / side.sprite.rect.height);
        Vector2 localCorner = -Vector2.Scale(pivot01, size);
        Vector3 corner = SideFrontCorner(sign);
        skew.position = corner - (localCorner.x * depthDir + localCorner.y * up);
    }

    private Vector3 SideFrontCorner(float sign) => transform.position + transform.right * (sign * footprint.x * 0.5f);

    private void UpdateSideVisibility()
    {
        if (leftSide == null && rightSide == null) return;

        Camera cam = Camera.main;
        if (!hideSideFacingAway || cam == null)
        {
            SetSideVisible(leftSide, true);
            SetSideVisible(rightSide, true);
            return;
        }

        SetSideVisible(leftSide, CameraSees(cam, -1f));
        SetSideVisible(rightSide, CameraSees(cam, +1f));
    }

    // A side is visible when the camera is on its outer side. Orthographic cameras look along
    // the tilt with no sideways component, so both sides come out edge-on (hidden).
    private bool CameraSees(Camera cam, float sign)
    {
        Vector3 outward = transform.right * sign;
        Vector3 center = SideFrontCorner(sign) + transform.up * (footprint.y * 0.5f) + TiltedView.Up * (_frontHeight * 0.5f);
        Vector3 toCamera = cam.orthographic ? -cam.transform.forward : cam.transform.position - center;
        return Vector3.Dot(toCamera, outward) > 0.001f;
    }

    private static void SetSideVisible(SpriteRenderer side, bool visible)
    {
        if (side != null && side.enabled != visible) side.enabled = visible;
    }

    /// <summary>
    /// 2x2 SVD of [[m00, m01], [m10, m11]] as rotation(phi) * scale(sx, sy) * rotation(theta).
    /// Angles in radians. sy is negative only for a mirrored matrix, which never happens here.
    /// </summary>
    private static void Svd2(float m00, float m01, float m10, float m11,
                             out float phi, out float sx, out float sy, out float theta)
    {
        float e = (m00 + m11) * 0.5f, f = (m00 - m11) * 0.5f;
        float g = (m10 + m01) * 0.5f, h = (m10 - m01) * 0.5f;
        float q = Mathf.Sqrt(e * e + h * h), r = Mathf.Sqrt(f * f + g * g);
        sx = q + r;
        sy = q - r;
        float a1 = Mathf.Atan2(g, f), a2 = Mathf.Atan2(h, e);
        theta = (a2 - a1) * 0.5f;
        phi = (a2 + a1) * 0.5f;
    }

    // ---- Sizing helpers -------------------------------------------------------------------

    /// <summary>Resizes a sprite to the given world size without touching its Transform scale.</summary>
    private static void FitSprite(SpriteRenderer sprite, Vector2 worldSize, SpriteFit fit)
    {
        // Sliced draw mode on a sprite with no borders just stretches the whole image to Size,
        // which lets us resize without touching the Transform (so children/colliders aren't scaled).
        sprite.drawMode = fit == SpriteFit.Tile ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;

        Vector3 scale = sprite.transform.lossyScale;
        sprite.size = new Vector2(
            worldSize.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            worldSize.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
    }

    /// <summary>Height of a sprite as actually drawn, in world units (respects Tiled/Sliced size).</summary>
    public static float DrawnHeight(SpriteRenderer sprite)
    {
        float local = sprite.drawMode == SpriteDrawMode.Simple ? sprite.sprite.bounds.size.y : sprite.size.y;
        return local * sprite.transform.lossyScale.y;
    }

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        // Footprint outline on the ground, so level designers can line structures up to the grid.
        Gizmos.color = new Color(1f, 0.4f, 0.2f, 0.8f);
        Vector3 center = transform.position + new Vector3(0f, footprint.y * 0.5f, 0f);
        Gizmos.DrawWireCube(center, new Vector3(footprint.x, footprint.y, 0f));
    }

    [UnityEditor.MenuItem("Fireline/Tilted View/Create Structure", priority = 20)]
    private static void CreateStructure()
    {
        GameObject root = new GameObject("Structure");
        UnityEditor.Undo.RegisterCreatedObjectUndo(root, "Create Structure");

        GameObject frontGo = new GameObject("Front");
        frontGo.transform.SetParent(root.transform, false);
        SpriteRenderer frontSprite = frontGo.AddComponent<SpriteRenderer>();
        frontGo.AddComponent<StandUpright>();

        GameObject topGo = new GameObject("Top");
        topGo.transform.SetParent(root.transform, false);
        topGo.AddComponent<SpriteRenderer>();
        HeightAboveGround topHeight = topGo.AddComponent<HeightAboveGround>();

        Structure structure = root.AddComponent<Structure>();
        structure.front = frontSprite;
        structure.top = topHeight;
        structure.leftSide = CreateSide(root.transform, "Side Left");
        structure.rightSide = CreateSide(root.transform, "Side Right");
        root.AddComponent<FadeWhenBehind>();

        if (UnityEditor.Selection.activeTransform != null)
            root.transform.SetParent(UnityEditor.Selection.activeTransform, false);
        UnityEditor.Selection.activeGameObject = root;
    }

    [UnityEditor.MenuItem("Fireline/Tilted View/Add Side Faces To Selected", priority = 21)]
    private static void AddSideFacesToSelected()
    {
        int added = 0;
        foreach (GameObject go in UnityEditor.Selection.gameObjects)
        {
            Structure structure = go.GetComponent<Structure>();
            if (structure == null) continue;

            UnityEditor.Undo.RecordObject(structure, "Add Side Faces");
            if (structure.leftSide == null) { structure.leftSide = CreateSide(go.transform, "Side Left"); added++; }
            if (structure.rightSide == null) { structure.rightSide = CreateSide(go.transform, "Side Right"); added++; }
            UnityEditor.EditorUtility.SetDirty(structure);
            structure.Apply();
        }
        Debug.Log($"Add Side Faces: created {added} side face(s). Assign the side sprite to each \"Sprite\" child.");
    }

    [UnityEditor.MenuItem("Fireline/Tilted View/Add Side Faces To Selected", validate = true)]
    private static bool CanAddSideFaces()
    {
        foreach (GameObject go in UnityEditor.Selection.gameObjects)
            if (go.GetComponent<Structure>() != null) return true;
        return false;
    }

    // "Side X" (skewed by the script) > "Sprite" (the SpriteRenderer you assign art to).
    private static SpriteRenderer CreateSide(Transform root, string sideName)
    {
        var skew = new GameObject(sideName);
        UnityEditor.Undo.RegisterCreatedObjectUndo(skew, "Create Side Face");
        skew.transform.SetParent(root, false);

        var spriteGo = new GameObject("Sprite");
        spriteGo.transform.SetParent(skew.transform, false);
        return spriteGo.AddComponent<SpriteRenderer>();
    }
#endif
}