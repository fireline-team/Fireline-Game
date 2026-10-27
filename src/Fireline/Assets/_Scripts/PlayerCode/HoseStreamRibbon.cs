using UnityEngine;

/// <summary>A single continuous ribbon mesh with an advancing front and a parabolic drop.</summary>
public sealed class HoseStreamRibbon
{
    public const int Segments = 32;
    private readonly Vector3[] vertices = new Vector3[(Segments + 1) * 2];
    private readonly Vector3[] normals = new Vector3[(Segments + 1) * 2];
    private float targetRange;
    private float groundClearance = -1f;
    private float fadeAge;
    private bool firing;

    public Mesh Mesh { get; }
    public Vector3 Origin { get; private set; }
    public Vector2 Direction { get; private set; } = Vector2.right;
    public float Length { get; private set; }
    public float Opacity { get; private set; }
    public bool IsVisible => Opacity > 0f;

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
            Length = Mathf.MoveTowards(Length, targetRange, Mathf.Max(0.1f, settings.extensionSpeed) * deltaTime);
        else
        {
            fadeAge += deltaTime;
            Opacity = 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(fadeAge / Mathf.Max(0.02f, settings.releaseFade)));
            if (Opacity <= 0f) Length = 0f;
        }

        Vector3 viewForward = TiltedView.StandingRotation * Vector3.forward;
        float halfWidth = settings.beamWidth * 0.5f * Mathf.Lerp(1.25f, 1f, Opacity);
        for (int i = 0; i <= Segments; i++)
        {
            float distance = Length * i / Segments;
            Vector3 center = Offset(distance, settings);
            Vector3 tangent = Offset(distance + 0.01f, settings) - center;
            Vector3 across = Vector3.Cross(viewForward, tangent).normalized;
            vertices[i * 2] = center - across * halfWidth;
            vertices[i * 2 + 1] = center + across * halfWidth;
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
        return Mathf.Min(limit, 0.5f * Mathf.Max(0f, settings.beamGravity) * travelTime * travelTime);
    }

    public float MaximumDrop(HoseVfxLibrary.StreamSettings settings) => groundClearance >= 0f
        ? Mathf.Min(settings.maximumDrop, groundClearance) : settings.maximumDrop;

    private Vector3 Offset(float distance, HoseVfxLibrary.StreamSettings settings) =>
        (Vector3)Direction * distance - TiltedView.Up * Drop(distance, settings);

    public void Dispose()
    {
        if (Mesh != null) Object.Destroy(Mesh);
    }

    public void Clear()
    {
        firing = false;
        Length = 0f;
        Opacity = 0f;
    }
}
