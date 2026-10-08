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
///
/// The collider, layer, top placement and draw order are set from the Footprint, so the
/// only things to fill in by hand are the sprites. Create one with
/// Fireline > Tilted View > Create Structure.
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

    [Tooltip("Ground area this blocks, in world units. X = width, Y = depth going up the screen. Usually whole grid cells.")]
    [SerializeField] private Vector2 footprint = Vector2.one;
    [Tooltip("The upright front face (should have StandUpright).")]
    [SerializeField] private SpriteRenderer front;
    [Tooltip("Optional flat top surface. It's raised to the top of the Front sprite automatically.")]
    [SerializeField] private HeightAboveGround top;

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

        // The SortingGroup draws Front and Top together, sorted by this root's position
        // (the front edge). Without it, a player behind the wall could be drawn over its top.
        if (top != null && front != null && front.sprite != null)
        {
            float frontHeight = front.sprite.bounds.size.y * front.transform.lossyScale.y;
            top.GroundOffset = new Vector2(0f, footprint.y * 0.5f);
            top.Height = frontHeight;
        }
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
        root.AddComponent<FadeWhenBehind>();

        if (UnityEditor.Selection.activeTransform != null)
            root.transform.SetParent(UnityEditor.Selection.activeTransform, false);
        UnityEditor.Selection.activeGameObject = root;
    }
#endif
}
