using UnityEngine;

/// <summary>
/// Holds an object a set height above its parent's spot on the ground, e.g. the player's
/// AimPivot at waist height so the hands and hose orbit the torso instead of the feet.
///
/// Unlike StandUpright, this doesn't rotate anything, so it's safe on objects that spin
/// to aim. It controls this object's position, so edit the settings here instead of the Transform.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
public class HeightAboveGround : MonoBehaviour
{
    [Tooltip("How far above the ground, in world units. For the player's hands, try around waist height.")]
    [SerializeField] private float height = 0.6f;
    [Tooltip("Offset along the ground, relative to the parent (X = right, Y = up the screen).")]
    [SerializeField] private Vector2 groundOffset;
    
    public float Height
    {
        get => height;
        set { height = value; Apply(); }
    }
    public Vector2 GroundOffset
    {
        get => groundOffset;
        set { groundOffset = value; Apply(); }
    }

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif

    public void Apply()
    {
        if (transform.parent == null) return;
        transform.localPosition = TiltedView.RaisedLocalPosition(transform.parent, groundOffset, height);
    }

#if UNITY_EDITOR
    [UnityEditor.MenuItem("Fireline/Tilted View/Raise Selected Off Ground", priority = 1)]
    private static void RaiseSelected()
    {
        foreach (GameObject go in UnityEditor.Selection.gameObjects)
            if (go.GetComponent<HeightAboveGround>() == null)
                UnityEditor.Undo.AddComponent<HeightAboveGround>(go);
    }

    [UnityEditor.MenuItem("Fireline/Tilted View/Raise Selected Off Ground", validate = true)]
    private static bool CanRaiseSelected() => UnityEditor.Selection.gameObjects.Length > 0;
#endif
}
