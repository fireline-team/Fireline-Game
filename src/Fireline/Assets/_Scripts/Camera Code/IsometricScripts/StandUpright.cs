using UnityEngine;

/// <summary>
/// Makes a sprite stand up and face the tilted camera. Add it to any sprite that should
/// look upright: characters, enemies, flames, trees, the firetruck. Leave it off anything
/// that lies on the ground (tilemaps, puddles, scorch marks) or points along the ground
/// (the hose).
///
/// Put this on a CHILD sprite object, not on the object that holds the Rigidbody2D or
/// collider, so physics stays flat. Use the menu Fireline > Tilted View > Stand Up Selected
/// to add it to several objects at once.
///
/// It controls this object's rotation and (with Feet On Ground) its position, so edit the
/// settings here instead of the Transform.
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
    // Transforms shouldn't be changed during OnValidate itself, so apply right after it.
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif

    /// <summary>Re-applies the standing rotation and feet placement.</summary>
    public void Apply()
    {
        transform.rotation = TiltedView.StandingRotation;

        if (!feetOnGround || transform.parent == null) return;

        // Distance from the sprite's pivot down to its bottom edge, in world units.
        float lift = 0f;
        SpriteRenderer sprite = GetComponent<SpriteRenderer>();
        if (sprite != null && sprite.sprite != null)
            lift = -sprite.sprite.bounds.min.y * transform.lossyScale.y;

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
