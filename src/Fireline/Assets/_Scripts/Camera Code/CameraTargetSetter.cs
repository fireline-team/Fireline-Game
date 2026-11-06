using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Only standing firefighters influence shared camera framing.</summary>
[DefaultExecutionOrder(-100)]
public class CameraTargetSetter : MonoBehaviour
{
    private CinemachineTargetGroup targetGroup;

    private void Awake()
    {
        targetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        if (targetGroup == null) return;
        foreach (var camera in FindObjectsByType<CinemachineCamera>(FindObjectsSortMode.None))
            if (camera.Follow == targetGroup.transform && camera.GetComponent<SharedCameraFramingBounds>() == null)
                camera.gameObject.AddComponent<SharedCameraFramingBounds>();
    }

    public void OnPlayerJoined(PlayerInput playerInput) => SetTarget(playerInput.transform);

    public void SetTarget(Transform target)
    {
        if (targetGroup == null || target == null) return;
        var health = target.GetComponent<PlayerHealth>();
        if (health != null && (!health.isActiveAndEnabled || health.IsDowned)) return;
        if (targetGroup.FindMember(target) < 0) targetGroup.AddMember(target, 1f, 0.75f);
    }

    private void Update()
    {
        if (targetGroup == null) targetGroup = FindAnyObjectByType<CinemachineTargetGroup>();
        if (targetGroup == null) return;
        for (int i = targetGroup.Targets.Count - 1; i >= 0; i--)
        {
            var target = targetGroup.Targets[i].Object;
            var health = target != null ? target.GetComponent<PlayerHealth>() : null;
            if (target == null || !target.gameObject.activeInHierarchy
                || (health != null && (!health.isActiveAndEnabled || health.IsDowned)))
                targetGroup.Targets.RemoveAt(i);
        }
        // Also handles scene-placed players, revival, and players joining before Awake.
        foreach (var player in PlayerHealth.ActivePlayers)
            if (player != null && player.gameObject.scene == gameObject.scene) SetTarget(player.transform);
        // An empty group retains its last position when the whole team is down.
    }
}
