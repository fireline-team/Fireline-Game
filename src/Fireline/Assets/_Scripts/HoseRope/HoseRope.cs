using UnityEngine;

/// <summary>
/// A chain of Verlet particles constrained to the ground plane, with attached endpoints.
/// GroundUp selects the plane independently of the camera; only the end spans rise to the fittings.
/// </summary>
[RequireComponent(typeof(LineRenderer))]
[DefaultExecutionOrder(200)]
public class HoseRope : MonoBehaviour
{
    [SerializeField, Min(4)] private int segments = 64;
    [SerializeField, Min(0.1f)] private float minimumLength = 12f;
    [SerializeField, Min(0.1f)] private float payoutSlack = 2f;
    [SerializeField, Min(0.01f)] private float radius = 0.06f;
    [Tooltip("Height of the hose center above the source base ground plane, independent of hose thickness.")]
    [SerializeField, Min(0f)] private float groundClearance = 0.01f;
    [SerializeField, Min(0f)] private float airDrag = 1.5f;
    [SerializeField, Min(0f)] private float groundFriction = 12f;
    [SerializeField, Range(1, 8)] private int substeps = 3;
    [SerializeField, Range(4, 64)] private int constraintIterations = 24;
    [Tooltip("Re-lay the hose after a teleport rather than whipping it across the map.")]
    [SerializeField, Min(1f)] private float teleportDistance = 8f;

    private readonly HoseObstacleRoute obstacleRoute = new HoseObstacleRoute();
    private readonly HoseObstacleRoute localRoute = new HoseObstacleRoute();
    private float[] lengthWeights;
    private int[] routeSubdivisions = new int[8];
    private float[] routeSpanLengths = new float[8];
    private Transform source;
    private Transform nozzle;
    private Transform nozzleOwner;
    private Vector3 resolvedNozzle;
    private Vector3 lastClearNozzle;
    private bool hasClearNozzle;
    private Vector3 groundOrigin;
    private Vector3 groundUp = Vector3.back;
    private Vector3[] points;
    private Vector3[] previous;
    private LineRenderer line;
    private float maximumLength;
    private MaterialPropertyBlock colorProperties;
    private HoseVfxLibrary vfxLibrary;
    private Vector3 lastSource;
    private Vector3 lastNozzle;

    public float PaidOutLength { get; private set; }
    public float Radius => radius;
    public float GroundClearance => groundClearance;
    public float GetSegmentRestLength(int index) => PaidOutLength * lengthWeights[index];
    public int PointCount => points != null ? points.Length : 0;
    public Vector3 GetPoint(int index) => points[index];
    public Transform Source => source;
    public Transform Nozzle => nozzle;
    public Vector3 NozzlePosition => resolvedNozzle;

    public void Bind(Transform waterSource, Transform nozzleHead, Vector3 planeOrigin, Vector3 planeUp, Transform playerAnchor = null)
    {
        obstacleRoute.Clear();
        source = waterSource;
        nozzle = nozzleHead;
        nozzleOwner = playerAnchor;
        hasClearNozzle = false;
        groundOrigin = planeOrigin;
        groundUp = planeUp.sqrMagnitude > 0.001f ? planeUp.normalized : Vector3.back;
        line = GetComponent<LineRenderer>();
        vfxLibrary = Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath);
        line.useWorldSpace = true;
        line.widthMultiplier = radius * 2f;
        line.numCapVertices = 4;
        line.numCornerVertices = 4;
        line.enabled = source != null && nozzle != null;
        if (!line.enabled) return;
        segments = Mathf.Max(4, segments);
        points = new Vector3[segments + 1];
        previous = new Vector3[segments + 1];
        lengthWeights = new float[segments];
        for (int i = 0; i < segments; i++) lengthWeights[i] = 1f / segments;
        RefreshNozzlePosition();
        PaidOutLength = Mathf.Max(minimumLength, Vector3.Distance(source.position, resolvedNozzle) + payoutSlack);
        LayOut();
        ResolveObstacles();
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
        // Preserve compatibility with URP Unlit and the shared pixelated water shader.
        var renderer = GetComponent<LineRenderer>();
        var properties = colorProperties ?? (colorProperties = new MaterialPropertyBlock());
        renderer.GetPropertyBlock(properties);
        properties.SetColor("_BaseColor", color);
        properties.SetColor("_Color", color);
        renderer.SetPropertyBlock(properties);
    }


    private void LayOut()
    {
        // A full re-lay (bind/teleport) resets the adaptive local segment lengths.
        for (int i = 0; i < segments; i++) lengthWeights[i] = 1f / segments;
        RefreshNozzlePosition();
        Vector3 start = source.position;
        Vector3 end = resolvedNozzle;
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
        RefreshNozzlePosition();
        // The source is a reel: pay out when taut, but keep the trail when returning.
        PaidOutLength = Mathf.Max(PaidOutLength, Vector3.Distance(source.position, resolvedNozzle) + payoutSlack);
        if (maximumLength > 0f) PaidOutLength = Mathf.Min(PaidOutLength, maximumLength);
        if ((source.position - lastSource).sqrMagnitude > teleportDistance * teleportDistance
            || (resolvedNozzle - lastNozzle).sqrMagnitude > teleportDistance * teleportDistance)
        {
            obstacleRoute.Clear();
            LayOut();
        }
        RefreshObstacleRoute();
        PaidOutLength = Mathf.Max(PaidOutLength, obstacleRoute.Length + payoutSlack);
        if (maximumLength > 0f) PaidOutLength = Mathf.Min(PaidOutLength, maximumLength);
        float dt = Mathf.Min(deltaTime, 0.05f) / substeps;
        float drag = Mathf.Exp(-airDrag * dt);
        for (int step = 0; step < substeps; step++)
        {
            for (int i = 1; i < segments; i++)
            {
                Vector3 current = points[i];
                points[i] += Vector3.ProjectOnPlane(points[i] - previous[i], groundUp) * drag;
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
                    float spacing = GetSegmentRestLength(i);
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
                Vector3 velocity = points[i] - previous[i];
                Vector3 tangent = Vector3.ProjectOnPlane(velocity, groundUp) * Mathf.Exp(-groundFriction * dt);
                // Discard normal velocity as well as position: releasing a corner must not
                // carry an old airborne Verlet impulse into the next step.
                previous[i] = points[i] - tangent;
            }
            PinEnds();
        }
        ResolveObstacles();
        lastSource = source.position;
        lastNozzle = resolvedNozzle;
    }

    private bool UsesFootprints => Mathf.Abs(groundUp.z) > 0.999f;

    private void RefreshNozzlePosition()
    {
        if (source == null || nozzle == null) return;
        resolvedNozzle = nozzle.position;
        if (!UsesFootprints) return;
        obstacleRoute.Configure(radius, groundOrigin.z + groundUp.z * groundClearance, gameObject.scene);
        Vector3 anchor = nozzleOwner != null ? nozzleOwner.position :
            hasClearNozzle ? lastClearNozzle : source.position;
        // Prefer the player's side of the wall, even if a thin wall lies entirely
        // between the player and a tip whose final position happens to be clear.
        bool needsClamp = obstacleRoute.Contains(resolvedNozzle) ||
            (nozzleOwner != null && obstacleRoute.Blocker(anchor, resolvedNozzle) != null);
        if (needsClamp)
        {
            if (obstacleRoute.Contains(anchor) && !obstacleRoute.TryClearAnchor(anchor, out anchor))
                anchor = hasClearNozzle && !obstacleRoute.Contains(lastClearNozzle) ? lastClearNozzle : source.position;
            if (!obstacleRoute.Contains(anchor))
                resolvedNozzle = obstacleRoute.ClampEndpoint(anchor, resolvedNozzle);
        }
        if (!obstacleRoute.Contains(resolvedNozzle))
        {
            lastClearNozzle = resolvedNozzle;
            hasClearNozzle = true;
        }
    }

    private void RefreshObstacleRoute()
    {
        if (!UsesFootprints || source == null || nozzle == null) return;
        RefreshNozzlePosition();
        obstacleRoute.Update(source.position, resolvedNozzle, radius,
            groundOrigin.z + groundUp.z * groundClearance, gameObject.scene);
    }

    /// <summary>The last contact is the tether pivot; earlier spans consume available hose.</summary>
    public void GetTether(out Vector3 pivot, out float usedLength)
    {
        RefreshObstacleRoute();
        pivot = source.position;
        usedLength = 0;
        if (!UsesFootprints || !obstacleRoute.Valid) return;
        var route = obstacleRoute.Points;
        pivot = route[route.Count - 2];
        for (int i = 1; i < route.Count - 1; i++) usedLength += Vector3.Distance(route[i - 1], route[i]);
    }

    private void ResolveObstacles()
    {
        if (!UsesFootprints) return;
        RefreshObstacleRoute();
        if (!obstacleRoute.Valid) return;
        // Only repair the local run that actually intersects a footprint. Unaffected
        // particles keep their positions, velocity and share of the paid-out length.
        int remainingChecks = segments;
        for (int start = 0; start < segments && remainingChecks-- > 0; start++)
        {
            if (!obstacleRoute.Contains(points[start]) && !obstacleRoute.Contains(points[start + 1])
                && obstacleRoute.Blocker(points[start], points[start + 1]) == null) continue;
            int end = start + 1;
            while (start > 0 && obstacleRoute.Contains(points[start])) start--;
            while (end < segments && obstacleRoute.Contains(points[end])) end++;
            if (obstacleRoute.Contains(points[start]) || obstacleRoute.Contains(points[end]))
            {
                start = end - 1; // Backtracking must never retry the same unrepairable run.
                continue;
            }
            localRoute.Clear();
            localRoute.Update(points[start], points[end], radius,
                groundOrigin.z + groundUp.z * groundClearance, gameObject.scene);
            if (!localRoute.Valid) { start = end - 1; continue; }
            int addedSegments = RepairSpan(start, end);
            start += addedSegments - 1;
        }
    }

    private int RepairSpan(int start, int end)
    {
        var route = localRoute.Points;
        int spans = route.Count - 1;
        int oldCount = end - start;
        int count = Mathf.Max(oldCount, spans);
        if (routeSubdivisions.Length < spans)
        {
            routeSubdivisions = new int[spans];
            routeSpanLengths = new float[spans];
        }
        float totalLength = 0;
        for (int i = 0; i < spans; i++)
        {
            routeSubdivisions[i] = 1;
            routeSpanLengths[i] = Vector3.Distance(route[i], route[i + 1]);
            totalLength += routeSpanLengths[i];
        }
        for (int extra = spans; extra < count; extra++)
        {
            int longest = 0;
            for (int i = 1; i < spans; i++)
                if (routeSpanLengths[i] / routeSubdivisions[i] >
                    routeSpanLengths[longest] / routeSubdivisions[longest]) longest = i;
            routeSubdivisions[longest]++;
        }
        float weight = 0;
        for (int i = start; i < end; i++) weight += lengthWeights[i];
        int nextSegments = segments + count - oldCount;
        var nextPoints = new Vector3[nextSegments + 1];
        var nextPrevious = new Vector3[nextSegments + 1];
        var nextWeights = new float[nextSegments];
        System.Array.Copy(points, nextPoints, start + 1);
        System.Array.Copy(previous, nextPrevious, start + 1);
        System.Array.Copy(lengthWeights, nextWeights, start);
        int index = start;
        for (int span = 0; span < spans; span++)
            for (int j = 0; j < routeSubdivisions[span]; j++)
            {
                if (index > start)
                {
                    Vector3 position = Vector3.Lerp(route[span], route[span + 1], (float)j / routeSubdivisions[span]);
                    position += groundUp * (groundClearance - Vector3.Dot(position - groundOrigin, groundUp));
                    nextPoints[index] = nextPrevious[index] = position;
                }
                nextWeights[index] = totalLength > 0.00001f
                    ? weight * routeSpanLengths[span] / (totalLength * routeSubdivisions[span]) : weight / count;
                index++;
            }
        System.Array.Copy(points, end, nextPoints, index, segments - end + 1);
        System.Array.Copy(previous, end, nextPrevious, index, segments - end + 1);
        System.Array.Copy(lengthWeights, end, nextWeights, index, segments - end);
        points = nextPoints;
        previous = nextPrevious;
        lengthWeights = nextWeights;
        segments = nextSegments;
        return count;
    }

    private void ProjectToGround(int i)
    {
        float height = Vector3.Dot(points[i] - groundOrigin, groundUp);
        points[i] += groundUp * (groundClearance - height);
    }

    private void PinEnds()
    {
        points[0] = source.position;
        points[segments] = resolvedNozzle;
        previous[0] = points[0];
        previous[segments] = points[segments];
    }

    private void LateUpdate() => Render();

    private void Render()
    {
        if (line == null) return;
        line.enabled = source != null && nozzle != null && points != null;
        if (!line.enabled) return;
        if (vfxLibrary != null)
        {
            var properties = colorProperties ?? (colorProperties = new MaterialPropertyBlock());
            line.GetPropertyBlock(properties);
            VfxPixelation.Apply(properties, vfxLibrary.SupplyHosePixelation);
            line.SetPropertyBlock(properties);
        }
        // Clamp between physics steps too, so aiming cannot flash the hose inside a wall.
        RefreshNozzlePosition();
        PinEnds();
        ResolveObstacles();
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
