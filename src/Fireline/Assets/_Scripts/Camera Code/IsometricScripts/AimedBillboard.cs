using UnityEngine;

/// <summary>Keep an orbiting weapon sprite upright while its parent aims on the XY floor.</summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(SpriteRenderer))]
public class AimedBillboard : MonoBehaviour
{
    [Tooltip("The ground-plane aim pivot. Defaults to this sprite's parent.")]
    [SerializeField] private Transform aimPivot;

    private void OnEnable() => Apply();
    private void LateUpdate() => Apply();

    public void Apply()
    {
        Transform pivot = aimPivot != null ? aimPivot : transform.parent;
        if (pivot == null) return;
        Quaternion standing = TiltedView.StandingRotation;
        // Project floor aiming into the billboard plane before rotating within it.
        // Rotating a local X tilt under the pivot would tip the sprite sideways
        // as the pivot spins. World-space composition keeps its normal constant.
        Vector3 direction = Quaternion.Inverse(standing) * pivot.right;
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        transform.rotation = standing * Quaternion.AngleAxis(angle, Vector3.forward);
        // Position is still controlled by the aim pivot: orbit, waist height and
        // muzzle origin are unaffected by this visual-only orientation.
    }
}
