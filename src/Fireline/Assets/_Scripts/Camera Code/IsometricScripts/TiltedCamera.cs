using Unity.Cinemachine;
using UnityEngine;

/// <summary>
/// Put this on the Cinemachine Camera. It sets the camera's tilt and its Follow Offset
/// from TiltedView.TiltDegrees, so nobody has to do the trigonometry by hand.
/// </summary>
[ExecuteAlways, DisallowMultipleComponent]
[RequireComponent(typeof(CinemachineFollow))]
public class TiltedCamera : MonoBehaviour
{
    [Tooltip("How far the camera sits from the players. With an orthographic camera this mostly affects clipping, not zoom.")]
    [SerializeField, Min(1f)] private float distance = 10f;

    private void OnEnable() => Apply();

#if UNITY_EDITOR
    private void OnValidate() => UnityEditor.EditorApplication.delayCall += () => { if (this != null) Apply(); };
#endif

    public void Apply()
    {
        Quaternion tilt = TiltedView.StandingRotation;
        transform.rotation = tilt;

        // Sit behind the target along the camera's view direction, so the players stay centered.
        CinemachineFollow follow = GetComponent<CinemachineFollow>();
        follow.FollowOffset = -(tilt * Vector3.forward) * distance;
    }
}
