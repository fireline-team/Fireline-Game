using UnityEngine;

/// <summary>A world-space ribbon with an advancing front and a far end that trails behind aim.</summary>
public sealed class HoseStreamRibbon
{
    public const int Segments = 32;
    private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
    private readonly Vector3[] normals = new Vector3[(Segments + 1) * 2];
    private float targetRange;
    private float groundClearance = -1f;
    private float fadeAge;
    private bool firing;
    private bool hasPose;
    private Vector3 desiredEnd;
    private Vector3 followedEnd;

    public Mesh Mesh { get; }
    public Vector3 Origin { get; private set; }
    public Vector2 Direction { get; private set; } = Vector2.right;
    public float Length { get; private set; }
    public float Opacity { get; private set; }
    public bool IsVisible => Opacity > 0f;
    public float TargetRange => targetRange;
    public bool GroundAtEnd { get; set; } = true;
    public Vector3 CurveControl { get; private set; }
    public Vector3 CurveEnd { get; private set; }
    public Vector3 UnbentEndpoint => Origin + CurveEnd;

    public HoseStreamRibbon()
    {
        Mesh = new Mesh { name = "Hose stream ribbon (runtime)" };
        Mesh.MarkDynamic();
        Mesh.vertices = vertices;
        var uv = new Vector2[vertices.Length];
        var triangles = new int[Segments * 6];
        for (int i = 0; i <= Segments; i++)
        {
            uv[i * 2] = new Vector2((float)i / Segments, 0f);
            uv[i * 2 + 1] = new Vector2((float)i / Segments, 1f);
            if (i == Segments) continue;
            int v = i * 2;
            int t = i * 6;
            triangles[t] = v; triangles[t + 1] = v + 2; triangles[t + 2] = v + 1;
            triangles[t + 3] = v + 1; triangles[t + 4] = v + 2; triangles[t + 5] = v + 3;
        }
        Mesh.uv = uv;
        Mesh.triangles = triangles;
    }

    public void Aim(Vector3 origin, Vector2 direction, float range, float heightAboveGround)
    {
        desiredEnd = origin + (Vector3)direction.normalized * Mathf.Max(0f, range);
        if (!hasPose || !IsVisible || Vector3.Distance(followedEnd, origin) > Mathf.Max(1f, range * 2f))
            followedEnd = desiredEnd;
        hasPose = true;
        firing = true;
        fadeAge = 0f;
        Opacity = 1f;
        Origin = origin;
        Direction = direction.normalized;
        targetRange = Mathf.Max(0f, range);
        groundClearance = heightAboveGround;
        Length = Mathf.Min(Length, targetRange); // A new obstruction clips the water immediately.
    }

    public void Stop()
    {
        if (!firing) return;
        firing = false;
        fadeAge = 0f;
    }

    public void Tick(float deltaTime, HoseVfxLibrary.StreamSettings settings)
    {
        if (firing)
        {
            float blend = settings.aimResponseTime <= 0f ? 1f
                : 1f - Mathf.Exp(-Mathf.Max(0f, deltaTime) / settings.aimResponseTime);
            followedEnd = Vector3.Lerp(followedEnd, desiredEnd, blend);
            followedEnd = Origin + Vector3.ClampMagnitude(followedEnd - Origin, targetRange);
            Length = Mathf.MoveTowards(Length, targetRange, Mathf.Max(0.1f, settings.extensionSpeed) * deltaTime);
        }
        else
        {
            fadeAge += deltaTime;
            Opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeAge / Mathf.Max(0.02f, settings.releaseFade)));
            if (Opacity <= 0f) Length = 0f;
        }

        // Subdivide at the advancing front. Droplets use these same visible curve offsets.
        float fraction = targetRange > 0.001f ? Mathf.Clamp01(Length / targetRange) : 0f;
        Vector3 fullControl = (Vector3)Direction * (targetRange * 0.5f);
        CurveControl = fullControl * fraction;
        CurveEnd = 2f * (1f - fraction) * fraction * fullControl
            + fraction * fraction * (followedEnd - Origin);
        Vector3 viewForward = TiltedView.StandingRotation * Vector3.forward;
        float halfWidth = settings.beamWidth * 0.5f * Mathf.Lerp(1.25f, 1f, Opacity);
        for (int i = 0; i <= Segments; i++)
        {
            // Reserve the final eight slices for a semicircular cap, even on long beams.
            float capLength = Mathf.Min(halfWidth, Length * 0.25f);
            int bodySegments = Segments - 8;
            float capT = i <= bodySegments ? 0f : (float)(i - bodySegments) / 8;
            float distance = i <= bodySegments
                ? (Length - capLength) * i / bodySegments
                : Length - capLength + capLength * capT;
            float localHalfWidth = halfWidth * Mathf.Sqrt(Mathf.Max(0f, 1f - capT * capT));
            Vector3 center = Offset(distance, settings);
            Vector3 tangent = Offset(Mathf.Min(Length, distance + 0.01f), settings)
                - Offset(Mathf.Max(0f, distance - 0.01f), settings);
            if (tangent.sqrMagnitude < 0.000001f) tangent = Direction;
            Vector3 across = Vector3.Cross(viewForward, tangent).normalized;
            vertices[i * 2] = center - across * localHalfWidth;
            vertices[i * 2 + 1] = center + across * localHalfWidth;
            normals[i * 2] = normals[i * 2 + 1] = -viewForward;
        }
        Mesh.SetVertices(vertices);
        Mesh.SetNormals(normals);
        Mesh.RecalculateBounds();
    }

    public float Drop(float distance, HoseVfxLibrary.StreamSettings settings)
    {
        float limit = Mathf.Max(0f, settings.maximumDrop);
        if (groundClearance >= 0f) limit = Mathf.Min(limit, groundClearance);
        float travelTime = Mathf.Max(0f, distance) / Mathf.Max(0.1f, settings.extensionSpeed);
        float gravityDrop = Mathf.Min(limit, 0.5f * Mathf.Max(0f, settings.beamGravity) * travelTime * travelTime);
        float u = Length > 0.001f ? Mathf.Clamp01(distance / Length) : 0f;
        return Mathf.Max(gravityDrop, GroundDrop(settings) * u * u * u * u);
    }

    public float MaximumDrop(HoseVfxLibrary.StreamSettings settings) => groundClearance >= 0f
        ? Mathf.Min(settings.maximumDrop, groundClearance) : settings.maximumDrop;

    public float GroundDrop(HoseVfxLibrary.StreamSettings settings) => !GroundAtEnd ? 0f
        : groundClearance >= 0f ? groundClearance : Mathf.Max(0f, settings.maximumDrop);

    public Vector3 Endpoint(HoseVfxLibrary.StreamSettings settings) => Origin + Offset(Length, settings);

    private Vector3 Offset(float distance, HoseVfxLibrary.StreamSettings settings)
    {
        float u = Length > 0.001f ? Mathf.Clamp01(distance / Length) : 0f;
        return 2f * (1f - u) * u * CurveControl + u * u * CurveEnd - TiltedView.Up * Drop(distance, settings);
    }

    public void Dispose()
    {
        if (Mesh != null) Object.Destroy(Mesh);
    }

    public void Clear()
    {
        firing = false;
        hasPose = false;
        Length = 0f;
        Opacity = 0f;
    }
}
