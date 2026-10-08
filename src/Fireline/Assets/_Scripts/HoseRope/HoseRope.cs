using UnityEngine;

/// <summary>
/// A chain of Verlet particles with maximum-distance constraints. The simulation is
/// three-dimensional; GroundUp selects the ground plane independently of the camera.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[DefaultExecutionOrder(200)]
public class HoseRope : MonoBehaviour
{
    [SerializeField, Min(4)] private int segments = 64;
    [SerializeField, Min(0.1f)] private float minimumLength = 12f;
    [SerializeField, Min(0.1f)] private float payoutSlack = 2f;
    [SerializeField, Min(0.01f)] private float radius = 0.06f;
    [SerializeField, Min(0f)] private float gravity = 9.81f;
    [SerializeField, Min(0f)] private float airDrag = 1.5f;
    [SerializeField, Min(0f)] private float groundFriction = 12f;
    [SerializeField, Range(1, 8)] private int substeps = 3;
    [SerializeField, Range(4, 64)] private int constraintIterations = 24;
    [Tooltip("Re-lay the hose after a teleport rather than whipping it across the map.")]
    [SerializeField, Min(1f)] private float teleportDistance = 8f;

    private Transform source;
    private Transform nozzle;
    private Vector3 groundOrigin;
    private Vector3 groundUp = Vector3.back;
    private Vector3[] points;
    private Vector3[] previous;
    private LineRenderer line;
    private float maximumLength;
    private MaterialPropertyBlock colorProperties;
    private Vector3 lastSource;
    private Vector3 lastNozzle;

    public float PaidOutLength { get; private set; }
    public float Radius => radius;
    public int PointCount => points != null ? points.Length : 0;
    public Vector3 GetPoint(int index) => points[index];
    public Transform Source => source;
    public Transform Nozzle => nozzle;

    public void Bind(Transform waterSource, Transform nozzleHead, Vector3 planeOrigin, Vector3 planeUp)
    {
        source = waterSource;
        nozzle = nozzleHead;
        groundOrigin = planeOrigin;
        groundUp = planeUp.sqrMagnitude > 0.001f ? planeUp.normalized : Vector3.back;
        line = GetComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = radius * 2f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.enabled = source != null && nozzle != null;
        if (!line.enabled) return;
        segments = Mathf.Max(4, segments);
        points = new Vector3[segments + 1];
        previous = new Vector3[segments + 1];
        PaidOutLength = Mathf.Max(minimumLength, Vector3.Distance(source.position, nozzle.position) + payoutSlack);
        LayOut();
        Render();
    }

    public void SetMaximumLength(float length)
    {
        float next = Mathf.Max(0f, length);
        if (Mathf.Approximately(maximumLength, next)) return;
        maximumLength = next;
        if (maximumLength > 0f) PaidOutLength = Mathf.Min(PaidOutLength, maximumLength);
        if (points != null && source != null && nozzle != null) LayOut();
    }

    public void SetWarning(float strain)
    {
        if (line != null)
            line.widthMultiplier = radius * 2f * (1f + Mathf.Clamp01(strain) * 0.5f);
    }

    public void SetNozzle(Transform value) => nozzle = value;

    public void SetColor(Color color)
    {
        // URP Unlit uses _BaseColor rather than LineRenderer vertex colors.
        var renderer = GetComponent<LineRenderer>();
        var properties = colorProperties ?? (colorProperties = new MaterialPropertyBlock());
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }


    private void LayOut()
    {
        Vector3 start = source.position;
        Vector3 end = nozzle.position;
        Vector3 alongGround = Vector3.ProjectOnPlane(end - start, groundUp);
        if (alongGround.sqrMagnitude < 0.001f)
            alongGround = Vector3.ProjectOnPlane(Vector3.right, groundUp);
        if (alongGround.sqrMagnitude < 0.001f)
            alongGround = Vector3.ProjectOnPlane(Vector3.forward, groundUp);
        Vector3 sideways = Vector3.Cross(groundUp, alongGround).normalized;
        // A shallow S distributes spare length on the ground instead of stacking particles.
        float amplitude = Mathf.Sqrt(Mathf.Max(0f, PaidOutLength * PaidOutLength - (end - start).sqrMagnitude)) * 0.2f;
        for (int i = 0; i < points.Length; i++)
        {
            float t = (float)i / segments;
            points[i] = Vector3.Lerp(start, end, t) + sideways * (Mathf.Sin(t * Mathf.PI * 2f) * amplitude);
            if (i > 0 && i < segments) ProjectToGround(i);
            previous[i] = points[i];
        }
        PinEnds();
        lastSource = start;
        lastNozzle = end;
    }

    private void FixedUpdate() => Simulate(Time.fixedDeltaTime);

    /// <summary>Public for deterministic tests; normally called only by FixedUpdate.</summary>
    public void Simulate(float deltaTime)
    {
        if (source == null || nozzle == null || points == null || deltaTime <= 0f
            || float.IsNaN(deltaTime) || float.IsInfinity(deltaTime)) return;
        // The source is a reel: pay out when taut, but keep the trail when returning.
        PaidOutLength = Mathf.Max(PaidOutLength, Vector3.Distance(source.position, nozzle.position) + payoutSlack);
        if (maximumLength > 0f) PaidOutLength = Mathf.Min(PaidOutLength, maximumLength);
        if ((source.position - lastSource).sqrMagnitude > teleportDistance * teleportDistance
            || (nozzle.position - lastNozzle).sqrMagnitude > teleportDistance * teleportDistance)
            LayOut();
        float dt = Mathf.Min(deltaTime, 0.05f) / substeps;
        float drag = Mathf.Exp(-airDrag * dt);
        float spacing = PaidOutLength / segments;
        for (int step = 0; step < substeps; step++)
        {
            for (int i = 1; i < segments; i++)
            {
                Vector3 current = points[i];
                points[i] += (points[i] - previous[i]) * drag - groundUp * (gravity * dt * dt);
                previous[i] = current;
            }
            for (int iteration = 0; iteration < constraintIterations; iteration++)
            {
                PinEnds();
                // Alternate sweep direction so neither end consistently wins the solve.
                for (int n = 0; n < segments; n++)
                {
                    int i = iteration % 2 == 0 ? n : segments - 1 - n;
                    Vector3 offset = points[i + 1] - points[i];
                    float distance = offset.magnitude;
                    if (distance <= spacing || distance < 0.00001f) continue;
                    Vector3 correction = offset * ((distance - spacing) / distance);
                    if (i == 0) points[i + 1] -= correction;
                    else if (i + 1 == segments) points[i] += correction;
                    else
                    {
                        points[i] += correction * 0.5f;
                        points[i + 1] -= correction * 0.5f;
                    }
                }
                for (int i = 1; i < segments; i++) ProjectToGround(i);
            }
            for (int i = 1; i < segments; i++)
            {
                if (Vector3.Dot(points[i] - groundOrigin, groundUp) > radius + 0.001f) continue;
                Vector3 velocity = points[i] - previous[i];
                Vector3 tangent = Vector3.ProjectOnPlane(velocity, groundUp) * Mathf.Exp(-groundFriction * dt);
                float outward = Mathf.Max(0f, Vector3.Dot(velocity, groundUp));
                previous[i] = points[i] - tangent - groundUp * outward;
            }
            PinEnds();
        }
        lastSource = source.position;
        lastNozzle = nozzle.position;
    }

    private void ProjectToGround(int i)
    {
        float height = Vector3.Dot(points[i] - groundOrigin, groundUp);
        if (height < radius) points[i] += groundUp * (radius - height);
    }

    private void PinEnds()
    {
        points[0] = source.position;
        points[segments] = nozzle.position;
        previous[0] = points[0];
        previous[segments] = points[segments];
    }

    private void LateUpdate() => Render();

    private void Render()
    {
        if (line == null) return;
        line.enabled = source != null && nozzle != null && points != null;
        if (!line.enabled) return;
        // Follow the held nozzle exactly between physics steps, including aim rotation.
        PinEnds();
        line.positionCount = points.Length;
        line.SetPositions(points);
    }

    private void OnDisable()
    {
        if (line != null) line.enabled = false;
    }

    private void OnEnable()
    {
        if (points != null && source != null && nozzle != null) LayOut();
    }

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        if (points == null) return;
        Gizmos.color = Color.cyan;
        for (int i = 0; i < points.Length; i++) Gizmos.DrawWireSphere(points[i], radius);
    }
#endif
}
