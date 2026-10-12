using UnityEngine;

/// <summary>
/// The one place the camera tilt is defined. The camera, standing sprites, and anything
/// raised off the ground all read from here, so changing the angle is a one-number edit.
///
/// The ground and all gameplay stay flat on the XY plane. The camera leans back by
/// TiltDegrees, and sprites that should look upright lean the same amount to face it.
/// </summary>
public static class TiltedView
{
    /// <summary>Camera tilt in degrees. Edit this to change the angle everywhere.</summary>
    public const float TiltDegrees = 30f;

    /// <summary>Rotation that makes a sprite face the tilted camera head-on.</summary>
    public static Quaternion StandingRotation => Quaternion.Euler(-TiltDegrees, 0f, 0f);

    /// <summary>World direction that points "up off the ground" for a standing sprite.</summary>
    public static Vector3 Up => StandingRotation * Vector3.up;

    /// <summary>
    /// The point on the ground directly below <paramref name="point"/>, for things raised
    /// off the ground (like a hose held at waist height). Gameplay should use this.
    /// </summary>
    public static Vector3 ToGround(Vector3 point, float groundZ)
    {
        Vector3 up = Up;
        if (Mathf.Abs(up.z) < 0.0001f) return new Vector3(point.x, point.y, groundZ);
        float stepsUp = (groundZ - point.z) / up.z;
        return point + up * stepsUp;
    }

    /// <summary>
    /// Local position (in <paramref name="parent"/>'s space) that sits <paramref name="height"/>
    /// world units above the parent's ground point, plus an offset along the ground.
    /// </summary>
    public static Vector3 RaisedLocalPosition(Transform parent, Vector2 groundOffset, float height)
    {
        Vector3 lift = parent.InverseTransformVector(Up * height);
        return new Vector3(groundOffset.x, groundOffset.y, 0f) + lift;
    }
}
