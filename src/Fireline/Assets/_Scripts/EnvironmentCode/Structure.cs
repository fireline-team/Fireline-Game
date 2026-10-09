using UnityEngine;
using UnityEngine.Rendering;

/// <summary>
/// A solid thing that stands in the world and blocks movement and water: a wall segment,
/// a building, the firetruck, a barricade. Its parts:
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(BoxCollider2D), typeof(SortingGroup))]
public class Structure : MonoBehaviour
{
    public const string ObstacleLayer = "Obstacles";

    [Tooltip("Ground area this blocks, in world units. X = width, Y = depth going up the screen. Usually whole grid cells.")]
    [SerializeField] private Vector2 footprint = Vector2.one;
    [Tooltip("How tall the front face stands, in world units. 0 = keep the Front sprite's current height.")]
    [SerializeField, Min(0f)] private float height = 0f;
    [Tooltip("The upright front face (should have StandUpright).")]
    [SerializeField] private SpriteRenderer front;
    [Tooltip("Optional flat top surface. It's raised to the top of the Front sprite automatically.")]
    [SerializeField] private HeightAboveGround top;
    public enum SpriteFit
    {
        None,
        Stretch,
        Tile
    }

    [Tooltip("How the Front and Top sprites fill the footprint. Stretch scales the whole image; Tile repeats it. " +
             "Stretch also respects 9-slice borders if the sprite has them, so corners/edges won't distort.")]
    [SerializeField] private SpriteFit spriteFit = SpriteFit.Stretch;

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
            StandUpright upright = front.GetComponent<StandUpright>();
            if (upright != null) upright.Apply();
        }
        
        if (top != null)
        {
            top.GroundOffset = new Vector2(0f, footprint.y * 0.5f);
            top.Height = DrawnHeight(front);
            
            SpriteRenderer topSprite = top.GetComponent<SpriteRenderer>();
            if (spriteFit != SpriteFit.None && topSprite != null && topSprite.sprite != null)
                FitSprite(topSprite, footprint, spriteFit);
        }
    }
    
    private static void FitSprite(SpriteRenderer sprite, Vector2 worldSize, SpriteFit fit)
    {
        sprite.drawMode = fit == SpriteFit.Tile ? SpriteDrawMode.Tiled : SpriteDrawMode.Sliced;

        Vector3 scale = sprite.transform.lossyScale;
        sprite.size = new Vector2(
            worldSize.x / Mathf.Max(Mathf.Abs(scale.x), 0.0001f),
            worldSize.y / Mathf.Max(Mathf.Abs(scale.y), 0.0001f));
    }
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
        root.AddComponent<FadeWhenBehind>();

        if (UnityEditor.Selection.activeTransform != null)
            root.transform.SetParent(UnityEditor.Selection.activeTransform, false);
        UnityEditor.Selection.activeGameObject = root;
    }
#endif
}