using Unity.Cinemachine;
using UnityEngine;

/// <summary>Keep camera damping/zoom from cutting off standing players.</summary>
public class SharedCameraFramingBounds : CinemachineExtension
{
    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || !state.Lens.Orthographic || vcam.Follow == null) return;
        var group = vcam.Follow.GetComponent<CinemachineTargetGroup>();
        var framing = vcam.GetComponent<CinemachineGroupFraming>();
        if (group == null || framing == null) return;
        Quaternion orientation = state.GetFinalOrientation();
        Quaternion inverse = Quaternion.Inverse(orientation);
        Vector3 position = state.GetFinalPosition();
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        float paddingPixels = 0f;
        bool found = false;
        foreach (var member in group.Targets)
        {
            if (member.Object == null || member.Weight <= 0f) continue;
            var health = member.Object.GetComponent<PlayerHealth>();
            if (health == null || !health.isActiveAndEnabled || health.IsDowned) continue;
            var controller = member.Object.GetComponent<PlayerController>();
            Vector2 point = inverse * (member.Object.position - position);
            Rect bounds = controller != null ? controller.GetCameraPlaneBounds(position, orientation) : new Rect(point, Vector2.zero);
            if (controller != null) paddingPixels = Mathf.Max(paddingPixels, controller.ScreenEdgePaddingPixels);
            min = Vector2.Min(min, bounds.min);
            max = Vector2.Max(max, bounds.max);
            found = true;
        }
        if (!found) return;
        float aspect = Mathf.Max(0.01f, state.Lens.Aspect);
        var outputCamera = Camera.main;
        float pixelHeight = outputCamera != null ? Mathf.Max(1, outputCamera.pixelHeight) : Mathf.Max(1, Screen.height);
        float pixelWidth = outputCamera != null ? Mathf.Max(1, outputCamera.pixelWidth) : Mathf.Max(1, Screen.width);
        paddingPixels = Mathf.Min(paddingPixels, Mathf.Min(pixelWidth, pixelHeight) * 0.49f);
        float usableX = 1f - 2f * paddingPixels / pixelWidth;
        float usableY = 1f - 2f * paddingPixels / pixelHeight;
        float requiredSize = Mathf.Max((max.y - min.y) / (2f * usableY),
            (max.x - min.x) / (2f * usableX * aspect));
        // Damped zoom-in must never outrun the players moving back together.
        var lens = state.Lens;
        lens.OrthographicSize = Mathf.Clamp(Mathf.Max(lens.OrthographicSize, requiredSize),
            framing.OrthoSizeRange.x, framing.OrthoSizeRange.y);
        state.Lens = lens;
        float halfHeight = lens.OrthographicSize * usableY;
        float halfWidth = lens.OrthographicSize * aspect * usableX;
        Vector2 shift = new Vector2(PlayerController.ContainmentShift(min.x, max.x, halfWidth), PlayerController.ContainmentShift(min.y, max.y, halfHeight));
        state.PositionCorrection += orientation * new Vector3(shift.x, shift.y, 0);
    }

}
