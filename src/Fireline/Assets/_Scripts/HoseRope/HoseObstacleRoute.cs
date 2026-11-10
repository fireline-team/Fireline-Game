using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>Persistent contact corners on the XY ground plane. Queries ignore triggers and other scenes.</summary>
internal sealed class HoseObstacleRoute
{
    public readonly List<Vector3> Points = new List<Vector3>();
    private readonly List<RaycastHit2D> hits = new List<RaycastHit2D>();
    private readonly List<Vector3> nodes = new List<Vector3>();
    private readonly HashSet<Collider2D> expanded = new HashSet<Collider2D>();
    private readonly List<Vector3> detour = new List<Vector3>();
    private ContactFilter2D filter;
    private Scene scene;
    private float radius;
    private float groundZ;
    public bool Valid { get; private set; }
    public float Length { get; private set; }

    public void Clear() => Points.Clear();

    public void Update(Vector3 start, Vector3 end, float clearance, float z, Scene ownerScene)
    {
        scene = ownerScene;
        radius = clearance;
        groundZ = z;
        filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask(Structure.ObstacleLayer));
        if (Points.Count < 2) { Points.Clear(); Points.Add(start); Points.Add(end); }
        Points[0] = start;
        Points[Points.Count - 1] = end;
        // Release a contact only once the hose can pass it without crossing a footprint.
        for (int i = Points.Count - 2; i > 0; i--)
            if (Blocker(Points[i - 1], Points[i + 1]) == null) Points.RemoveAt(i);
        Valid = true;
        for (int i = 0; i < Points.Count - 1; i++)
        {
            if (Blocker(Points[i], Points[i + 1]) == null) continue;
            if (!FindDetour(Points[i], Points[i + 1])) { Valid = false; break; }
            Points.InsertRange(i + 1, detour);
            i += detour.Count;
        }
        Length = 0;
        for (int i = 1; i < Points.Count; i++) Length += Vector3.Distance(Points[i - 1], Points[i]);
    }

    public Collider2D Blocker(Vector3 start, Vector3 end)
    {
        Vector2 offset = end - start;
        float distance = offset.magnitude;
        int count = Physics2D.CircleCast(start, radius, offset.normalized, filter, hits, distance);
        Collider2D nearest = null;
        float best = float.PositiveInfinity;
        for (int i = 0; i < count; i++)
        {
            var hit = hits[i];
            if (hit.collider.gameObject.scene != scene || hit.distance >= best) continue;
            nearest = hit.collider;
            best = hit.distance;
        }
        return nearest;
    }

    private void AddCorners(Collider2D obstacle)
    {
        if (!expanded.Add(obstacle)) return;
        float pad = radius + 0.025f;
        if (obstacle is BoxCollider2D box)
        {
            Vector3 scale = box.transform.lossyScale;
            Vector2 half = box.size * 0.5f + new Vector2(pad / Mathf.Max(0.001f, Mathf.Abs(scale.x)),
                pad / Mathf.Max(0.001f, Mathf.Abs(scale.y)));
            for (int i = 0; i < 4; i++)
            {
                Vector3 p = box.transform.TransformPoint(box.offset + new Vector2(
                    (i & 1) == 0 ? -half.x : half.x, (i & 2) == 0 ? -half.y : half.y));
                p.z = groundZ;
                nodes.Add(p);
            }
        }
        else
        {
            // Non-box blockers get a conservative bounds detour.
            Bounds bounds = obstacle.bounds;
            bounds.Expand(pad * 2);
            for (int i = 0; i < 4; i++) nodes.Add(new Vector3(
                (i & 1) == 0 ? bounds.min.x : bounds.max.x,
                (i & 2) == 0 ? bounds.min.y : bounds.max.y, groundZ));
        }
    }

    private bool FindDetour(Vector3 start, Vector3 end)
    {
        nodes.Clear(); expanded.Clear(); detour.Clear();
        nodes.Add(start); nodes.Add(end);
        // Discover only obstacles met by candidate edges, rather than scanning the entire arena.
        // A bounded graph prevents an embedded endpoint or sealed area stalling the frame.
        while (nodes.Count <= 258)
        {
            int count = nodes.Count;
            var distance = new float[count];
            var parent = new int[count];
            var visited = new bool[count];
            for (int i = 0; i < count; i++) { distance[i] = float.PositiveInfinity; parent[i] = -1; }
            distance[0] = 0;
            for (int step = 0; step < count; step++)
            {
                int current = -1;
                for (int i = 0; i < count; i++)
                    if (!visited[i] && !float.IsPositiveInfinity(distance[i]) &&
                        (current < 0 || distance[i] < distance[current])) current = i;
                if (current < 0) break;
                if (current == 1)
                {
                    for (int i = parent[1]; i > 0; i = parent[i]) detour.Add(nodes[i]);
                    detour.Reverse();
                    return true;
                }
                visited[current] = true;
                for (int next = 0; next < count; next++)
                {
                    if (visited[next]) continue;
                    float candidate = distance[current] + Vector3.Distance(nodes[current], nodes[next]);
                    if (candidate >= distance[next]) continue;
                    var blocker = Blocker(nodes[current], nodes[next]);
                    if (blocker != null) { AddCorners(blocker); continue; }
                    distance[next] = candidate;
                    parent[next] = current;
                }
            }
            if (nodes.Count == count) break;
        }
        return false;
    }
}
