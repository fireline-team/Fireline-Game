using UnityEngine;

/// <summary>
/// Makes a sprite stand up. Put on a child object that has a sprite, not on a collider.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public class StandUpright : MonoBehaviour
{
    [Tooltip("Lift the sprite so its bottom edge touches the ground, whatever its pivot is. Only works on a child object.")]
    [SerializeField] private bool feetOnGround = true;
    [Tooltip("Extra offset along the ground, relative to the parent (X = right, Y = up the screen).")]
    [SerializeField] private Vector2 groundOffset;

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif
    
    public void Apply()
    {
        transform.rotation = TiltedView.StandingRotation;

        if (!feetOnGround || transform.parent == null) return;

        // Distance from the sprite's pivot down to its bottom edge, in world units.
        float lift = 0f;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null)
        {
            // Tiled/Sliced sprites are drawn at sprite.size around the same normalized pivot,
            // so measure from the drawn size rather than the source image.
            float pivot01 = sprite.sprite.pivot.y / sprite.sprite.rect.height;
            float drawnHeight = sprite.drawMode == SpriteDrawMode.Simple ? sprite.sprite.bounds.size.y : sprite.size.y;
            lift = pivot01 * drawnHeight * transform.lossyScale.y;
        }

        transform.localPosition = TiltedView.RaisedLocalPosition(transform.parent, groundOffset, lift);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Fireline/Tilted View/Stand Up Selected", priority = 0)]
    private static void StandUpSelected()
    {
        int added = 0;
        foreach (GameObject go in UnityEditor.Selection.gameObjects)
        {
            if (go.GetComponent<StandUpright>() != null) continue;
            if (go.GetComponent<Rigidbody2D>() != null || go.GetComponent<Collider2D>() != null)
            {
                Debug.LogWarning($"Skipped {go.name}: it has 2D physics, which must stay flat. Put the sprite on a child object and stand that up instead.", go);
                continue;
            }
            UnityEditor.Undo.AddComponent<StandUpright>(go);
            added++;
        }
        Debug.Log($"Stand Up Selected: added StandUpright to {added} object(s).");
    }

    [UnityEditor.MenuItem("Fireline/Tilted View/Stand Up Selected", validate = true)]
    private static bool CanStandUpSelected() => UnityEditor.Selection.gameObjects.Length > 0;
#endif
}