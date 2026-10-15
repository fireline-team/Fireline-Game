using System.Collections.Generic;
using Unity.Cinemachine;
using UnityEngine;

/// <summary>Keep camera damping/zoom from cutting off standing players.</summary>
public class SharedCameraFramingBounds : CinemachineExtension
{
    private readonly Vector3[] corners = new Vector3[8];
    private readonly List<Vector3> points = new List<Vector3>(32);

    protected override void PostPipelineStageCallback(CinemachineVirtualCameraBase vcam,
        CinemachineCore.Stage stage, ref CameraState state, float deltaTime)
    {
        if (stage != CinemachineCore.Stage.Finalize || vcam.Follow == null) return;
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
        points.Clear();
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
            if (controller != null)
            {
                controller.GetBoundaryCorners(corners);
                foreach (var corner in corners) points.Add(inverse * (corner - position));
            }
            else points.Add(inverse * (member.Object.position - position));
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
        if (!state.Lens.Orthographic)
        {
            var perspectiveLens = state.Lens;
            float fov = Mathf.Clamp(perspectiveLens.FieldOfView, framing.FovRange.x, framing.FovRange.y);
            // Increase FOV only as needed to keep damped framing from clipping a body.
            // ZoomOnly is used by the shared rig, so camera distance stays bounded.
            if (!PerspectiveIntervals(fov, aspect, usableX, usableY, out _, out _))
            {
                float low = fov, high = framing.FovRange.y;
                for (int i = 0; i < 20; i++)
                {
                    float mid = (low + high) * 0.5f;
                    if (PerspectiveIntervals(mid, aspect, usableX, usableY, out _, out _)) high = mid;
                    else low = mid;
                }
                fov = high;
            }
            PerspectiveIntervals(fov, aspect, usableX, usableY, out Vector2 lower, out Vector2 upper);
            Vector2 correction = new Vector2(ClosestZero(lower.x, upper.x), ClosestZero(lower.y, upper.y));
            perspectiveLens.FieldOfView = fov;
            state.Lens = perspectiveLens;
            state.PositionCorrection += orientation * new Vector3(correction.x, correction.y, 0);
            return;
        }
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

    private bool PerspectiveIntervals(float fov, float aspect, float usableX, float usableY, out Vector2 lower, out Vector2 upper)
    {
        float slope = Mathf.Tan(fov * Mathf.Deg2Rad * 0.5f);
        lower = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        upper = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        foreach (var point in points)
        {
            Vector2 half = new Vector2(slope * aspect * usableX, slope * usableY) * Mathf.Max(0.001f, point.z);
            lower = Vector2.Max(lower, (Vector2)point - half);
            upper = Vector2.Min(upper, (Vector2)point + half);
        }
        return lower.x <= upper.x && lower.y <= upper.y;
    }

    private static float ClosestZero(float lower, float upper) =>
        lower <= upper ? Mathf.Clamp(0, lower, upper) : (lower + upper) * 0.5f;
}
