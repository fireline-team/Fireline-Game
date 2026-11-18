using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class HoseRopeTests
{
    private readonly List<GameObject> created = new List<GameObject>();
    private GameObject Make(string name, Vector3 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var go in created) if (go != null) Object.Destroy(go);
        created.Clear();
        yield return null;
    }

    [Test]
    public void ConnectingStartsStraightWithoutSlackAndReconnectResetsTrail()
    {
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(103, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        for (int connection = 0; connection < 2; connection++)
        {
            rope.Bind(source, end, Vector3.zero, Vector3.back);
            rope.SetMaximumLength(20);
            float initialLength = Vector3.Distance(source.position, end.position);
            Assert.That(rope.PaidOutLength, Is.EqualTo(initialLength).Within(0.0001f));
            for (int step = 0; step < 30; step++) rope.Simulate(0.02f);
            Assert.That(rope.PaidOutLength, Is.EqualTo(initialLength).Within(0.0001f), "Standing still added starting slack");
            for (int i = 0; i < rope.PointCount; i++)
                Assert.That(rope.GetPoint(i).y, Is.EqualTo(100).Within(0.0001f), "Connection created a sideways slack loop");
            end.position += Vector3.right * 4;
            rope.Simulate(0.02f);
            float paidOut = rope.PaidOutLength;
            end.position -= Vector3.right * 4;
            rope.Simulate(0.02f);
            Assert.That(rope.PaidOutLength, Is.EqualTo(paidOut), "Returning should retain earned slack");
        }
    }

    [TestCase(0.5f)]
    [TestCase(3f)]
    public void ShortConnectionRendersFullWidthWithoutCrowdedJoins(float distance)
    {
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(100 + distance, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        var camera = Make("Mesh verification camera", new Vector3(100 + distance / 2, 96, -8)).AddComponent<Camera>();
        camera.transform.LookAt(new Vector3(100 + distance / 2, 100, 0), Vector3.up);
        camera.orthographic = true;
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        var line = rope.GetComponent<LineRenderer>();
        var render = typeof(HoseRope).GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        var mesh = new Mesh();
        try
        {
            for (int frame = 0; frame < 30; frame++)
            {
                rope.Simulate(0.02f);
                render.Invoke(rope, null);
                Assert.That(line.positionCount, Is.LessThan(rope.PointCount / 2), "Dense straight particles leaked into rounded render joins");
                Assert.That(line.GetPosition(0), Is.EqualTo(source.position));
                Assert.That(line.GetPosition(line.positionCount - 1), Is.EqualTo(end.position));
                line.BakeMesh(mesh, camera, true);
                Assert.That(mesh.vertexCount, Is.GreaterThan(0));
                var vertices = mesh.vertices;
                for (int i = 0; i < vertices.Length; i++) vertices[i] = camera.transform.InverseTransformPoint(vertices[i]);
                var triangles = mesh.triangles;
                // Slice the actual ribbon mesh across its grounded middle, checking
                // visible cross-section width rather than only the width property.
                for (int sample = 1; sample < 10; sample++)
                {
                    float x = camera.transform.InverseTransformPoint(Vector3.Lerp(source.position, end.position, sample / 10f)).x;
                    float min = float.PositiveInfinity, max = float.NegativeInfinity;
                    for (int i = 0; i < triangles.Length; i += 3)
                        for (int edge = 0; edge < 3; edge++)
                        {
                            Vector3 a = vertices[triangles[i + edge]], b = vertices[triangles[i + (edge + 1) % 3]];
                            if (Mathf.Abs(b.x - a.x) < 0.000001f) continue;
                            float t = (x - a.x) / (b.x - a.x);
                            if (t < 0 || t > 1) continue;
                            float y = Mathf.Lerp(a.y, b.y, t);
                            min = Mathf.Min(min, y); max = Mathf.Max(max, y);
                        }
                    Assert.That(max - min, Is.GreaterThanOrEqualTo(rope.Radius * 1.8f), $"Ribbon pinched at sample {sample}, frame {frame}");
                }
            }
        }
        finally { Object.DestroyImmediate(mesh); }
    }

    [UnityTest]
    public IEnumerator ChainStaysOnGroundWithPinnedEndsAndLimitedSegmentStretch()
    {
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(105, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, new Vector3(100, 100, 0), Vector3.back);
        for (int step = 0; step < 180; step++) rope.Simulate(0.02f);
        Assert.That(rope.GetPoint(0), Is.EqualTo(source.position));
        Assert.That(rope.GetPoint(rope.PointCount - 1), Is.EqualTo(end.position));
        CheckGrounded(rope, new Vector3(100, 100, 0), Vector3.back);
        CheckStretch(rope);
        yield return null;
    }

    [UnityTest]
    public IEnumerator MovingNozzleDragsTrailAndPaysOutWithoutReelingBackIn()
    {
        var source = Make("Source", new Vector3(100, 100, -0.15f)).transform;
        var end = Make("Nozzle", new Vector3(103, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, new Vector3(100, 100, 0), Vector3.back);
        for (int step = 0; step < 300; step++)
        {
            end.position += new Vector3(0.05f, 0.01f, 0);
            rope.Simulate(0.02f);
        }
        Assert.That(rope.PaidOutLength, Is.GreaterThan(12));
        Assert.That(rope.GetPoint(rope.PointCount - 1), Is.EqualTo(end.position));
        CheckStretch(rope);
        float paidOut = rope.PaidOutLength;
        end.position = source.position + new Vector3(2, 0, -0.4f); // teleport recovery
        rope.Simulate(0.02f);
        Assert.That(rope.PaidOutLength, Is.EqualTo(paidOut));
        CheckStretch(rope);
        yield return null;
    }

    private BoxCollider2D Building(Vector2 position, Vector2 size, float rotation = 0)
    {
        var go = Make("Hose obstacle", position);
        go.layer = LayerMask.NameToLayer(Structure.ObstacleLayer);
        go.transform.rotation = Quaternion.Euler(0, 0, rotation);
        var box = go.AddComponent<BoxCollider2D>();
        box.size = size;
        Physics2D.SyncTransforms();
        return box;
    }

    private static void CheckClear(HoseRope rope)
    {
        var filter = new ContactFilter2D { useTriggers = false };
        filter.SetLayerMask(LayerMask.GetMask(Structure.ObstacleLayer));
        var hits = new List<RaycastHit2D>();
        for (int i = 1; i < rope.PointCount; i++)
        {
            Vector2 delta = rope.GetPoint(i) - rope.GetPoint(i - 1);
            Assert.That(Physics2D.CircleCast(rope.GetPoint(i - 1), rope.Radius * 0.99f,
                delta.normalized, filter, hits, delta.magnitude), Is.Zero, $"Hose span {i} clips a building: {rope.GetPoint(i - 1)} -> {rope.GetPoint(i)}");
        }
    }

    [Test]
    public void RopeWrapsRotatedBuildingsAndTetherUsesCornerDistance()
    {
        Building(new Vector2(104, 100), new Vector2(2, 4), 25);
        Building(new Vector2(108, 101), new Vector2(2, 4), -15);
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(112, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, new Vector3(100, 100, 0), Vector3.back);
        CheckClear(rope); // Initial attachment must not render even one frame through a wall.
        for (int i = 0; i < 20; i++) { rope.Simulate(0.02f); CheckClear(rope); }
        rope.GetTether(out var pivot, out float used);
        Assert.That(used, Is.GreaterThan(0));
        Assert.That(used + Vector3.Distance(pivot, end.position),
            Is.GreaterThan(Vector3.Distance(source.position, end.position)));
        CheckStretch(rope);
    }

    [Test]
    public void MovingAroundBuildingWrapsAndReturningReleasesCorners()
    {
        Building(new Vector2(104, 100), new Vector2(2, 4));
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(102, 97, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, new Vector3(100, 100, 0), Vector3.back);
        var destinations = new[] { new Vector3(107, 97, -0.6f), new Vector3(107, 101, -0.6f),
            new Vector3(107, 97, -0.6f), new Vector3(102, 97, -0.6f) };
        foreach (var destination in destinations)
        {
            Vector3 start = end.position;
            for (int i = 1; i <= 60; i++)
            {
                end.position = Vector3.Lerp(start, destination, i / 60f);
                rope.Simulate(0.02f);
                CheckClear(rope);
                CheckGrounded(rope, new Vector3(100, 100, 0), Vector3.back);
            }
        }
        rope.GetTether(out var pivot, out float used);
        Assert.That(used, Is.Zero);
        Assert.That(pivot, Is.EqualTo(source.position));
    }

    [Test]
    public void RemovedBuildingAndTeleportDoNotLeaveStaleWraps()
    {
        var building = Building(new Vector2(104, 100), new Vector2(2, 4));
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(108, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        rope.GetTether(out _, out float used);
        Assert.That(used, Is.GreaterThan(0));
        building.enabled = false;
        rope.Simulate(0.02f);
        rope.GetTether(out _, out used);
        Assert.That(used, Is.Zero);
        building.enabled = true;
        end.position = new Vector3(100, 110, -0.6f);
        Physics2D.SyncTransforms();
        rope.Simulate(0.02f);
        CheckClear(rope);
        rope.GetTether(out _, out used);
        Assert.That(used, Is.Zero);
    }

    [Test]
    public void TriggerFootprintsDoNotSnagHose()
    {
        Building(new Vector2(104, 100), new Vector2(2, 4)).isTrigger = true;
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(108, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        rope.GetTether(out var pivot, out float used);
        Assert.That(used, Is.Zero);
        Assert.That(pivot, Is.EqualTo(source.position));
    }

    [Test]
    public void NewCornerPreservesDistantSlackAndLengthBudget()
    {
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(108, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        // Earn slack through movement now that a new connection starts without any.
        for (int i = 0; i < 120; i++)
        {
            end.position += new Vector3(0.025f, 0.025f, 0) * (i < 60 ? 1 : -1);
            rope.Simulate(0.02f);
        }
        Assert.That(rope.PaidOutLength, Is.GreaterThan(Vector3.Distance(source.position, end.position)));
        // Introduce contact near the player, with slack still lying beside the hydrant.
        int contact = rope.PointCount * 3 / 4;
        Vector3 center = (rope.GetPoint(contact) + rope.GetPoint(contact + 1)) * 0.5f;
        Building(center, new Vector2(0.2f, 0.2f));
        var untouched = new Vector3[rope.PointCount / 2];
        for (int i = 0; i < untouched.Length; i++) untouched[i] = rope.GetPoint(i);
        float budget = rope.PaidOutLength;
        typeof(HoseRope).GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(rope, null);
        CheckClear(rope);
        CheckGrounded(rope, Vector3.zero, Vector3.back);
        for (int i = 0; i < untouched.Length; i++)
            Assert.That(rope.GetPoint(i), Is.EqualTo(untouched[i]), "Corner contact erased distant slack");
        float allocated = 0;
        for (int i = 0; i < rope.PointCount - 1; i++) allocated += rope.GetSegmentRestLength(i);
        Assert.That(allocated, Is.EqualTo(budget).Within(0.001f), "Local repairs must conserve the hose budget");
    }

    [Test]
    public void RepeatedCornerPullsKeepSlackSimulationStable()
    {
        Building(new Vector2(104, 100), new Vector2(2, 4));
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(107, 97, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        Vector3 initialSlack = rope.GetPoint(10);
        for (int i = 0; i < 1200; i++)
        {
            end.position = new Vector3(107, 97 + Mathf.PingPong(i * 0.025f, 5), -0.6f);
            rope.Simulate(0.02f);
            CheckClear(rope);
            Assert.That(rope.PointCount, Is.LessThan(256), "Repeated contact keeps subdividing the hose");
        }
        Assert.That(Vector3.Distance(rope.GetPoint(10), initialSlack), Is.GreaterThan(0.01f),
            "Tension should draw slack through the hose instead of leaving the old trail frozen");
        CheckGrounded(rope, Vector3.zero, Vector3.back);
        float budget = 0;
        for (int i = 0; i < rope.PointCount - 1; i++) budget += rope.GetSegmentRestLength(i);
        Assert.That(budget, Is.EqualTo(rope.PaidOutLength).Within(0.001f));
    }

    [TestCase(0f)]
    [TestCase(30f)]
    public void RepeatedNozzleWallEntryKeepsHoseVisibleAndBounded(float rotation)
    {
        var building = Building(new Vector2(104, 100), new Vector2(2, 4), rotation);
        Vector3 At(float x, float y, float z)
        {
            Vector3 result = building.transform.TransformPoint(new Vector3(x, y, 0));
            result.z = z;
            return result;
        }
        var source = Make("Source", At(-5, -2, -0.2f)).transform;
        var player = Make("Player anchor", At(-1.5f, 0, 0)).transform;
        var tip = Make("Nozzle", At(-0.4f, 0, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, tip, Vector3.zero, Vector3.back, player);
        var line = rope.GetComponent<LineRenderer>();
        var render = typeof(HoseRope).GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance);
        Assert.That(line.enabled, Is.True, "Binding with an embedded tip must still show the hose");
        for (int i = 0; i < 1000; i++)
        {
            bool inside = i % 2 == 0;
            Vector3 requested = At(inside ? -0.4f : -1.4f, 0, -0.6f);
            tip.position = requested;
            rope.Simulate(0.02f);
            render.Invoke(rope, null);
            Assert.That(line.enabled, Is.True, $"Hose vanished on wall entry {i}");
            Assert.That(tip.position, Is.EqualTo(requested), "Clamping the rope must not move the weapon transform");
            Assert.That(rope.PointCount, Is.LessThan(256), "Repeated wall entry grew the simulation without bound");
            CheckClear(rope);
            if (inside)
                Assert.That(building.transform.InverseTransformPoint(rope.NozzlePosition).x, Is.LessThan(-1f - rope.Radius));
            else
                Assert.That(rope.NozzlePosition, Is.EqualTo(requested), "The hose must return to the nozzle when clear");
            Assert.That(line.GetPosition(line.positionCount - 1), Is.EqualTo(rope.NozzlePosition));
        }
    }

    [Test]
    public void NozzlePastThinWallRetractsToPlayerSideWithoutPhysicsStep()
    {
        Building(new Vector2(104, 100), new Vector2(0.2f, 4));
        var source = Make("Source", new Vector3(100, 98, -0.2f)).transform;
        var player = Make("Player anchor", new Vector3(103.5f, 100, 0)).transform;
        var tip = Make("Nozzle", new Vector3(103.6f, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, tip, Vector3.zero, Vector3.back, player);
        tip.position = new Vector3(104.5f, 100, -0.6f); // Tip itself is clear, but the wall is in between.
        typeof(HoseRope).GetMethod("Render", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            .Invoke(rope, null);
        Assert.That(rope.NozzlePosition.x, Is.LessThan(103.9f - rope.Radius));
        Assert.That(rope.GetComponent<LineRenderer>().enabled, Is.True);
        CheckClear(rope);
    }

    [TestCase(0f)]
    [TestCase(25f)]
    public void EmbeddedNozzleFollowsPlayerSlidingCloseToLongWall(float rotation)
    {
        var wall = Building(new Vector2(104, 100), new Vector2(2, 80), rotation);
        Vector3 At(float x, float y, float z)
        {
            Vector3 position = wall.transform.TransformPoint(new Vector3(x, y, 0));
            position.z = z;
            return position;
        }
        var source = Make("Source", At(-5, -20, -0.2f)).transform;
        var player = Make("Player", At(-1.04f, -20, 0)).transform;
        var tip = Make("Tip", At(-0.4f, -20, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, tip, Vector3.zero, Vector3.back, player);
        for (int i = 0; i < 200; i++)
        {
            float y = -20 + i * 0.2f;
            player.position = At(-1.04f, y, 0); // Body is clear, but closer than the hose radius.
            tip.position = At(-0.4f, y, -0.6f);
            rope.Simulate(0.02f);
            Vector3 local = wall.transform.InverseTransformPoint(rope.NozzlePosition);
            Assert.That(local.y, Is.EqualTo(y).Within(0.01f), "Visual endpoint stayed at an old wall contact");
            Assert.That(local.x, Is.LessThan(-1f - rope.Radius));
            CheckClear(rope);
        }
    }

    private static void CheckGrounded(HoseRope rope, Vector3 origin, Vector3 up)
    {
        for (int i = 1; i < rope.PointCount - 1; i++)
            Assert.That(Vector3.Dot(rope.GetPoint(i) - origin, up),
                Is.EqualTo(rope.GroundClearance).Within(0.0001f), $"Particle {i} left the ground plane");
        Assert.That(rope.GetPoint(0), Is.EqualTo(rope.Source.position));
        Assert.That(rope.GetPoint(rope.PointCount - 1), Is.EqualTo(rope.Nozzle.position));
    }

    [UnityTest]
    public IEnumerator CornerReleaseAndRenderKeepHoseOnGround()
    {
        var building = Building(new Vector2(104, 100), new Vector2(2, 4));
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(108, 100, -1.5f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        CheckGrounded(rope, Vector3.zero, Vector3.back);
        yield return null;
        yield return null;
        var line = rope.GetComponent<LineRenderer>();
        for (int i = 1; i < line.positionCount - 1; i++)
            Assert.That(line.GetPosition(i).z, Is.EqualTo(-rope.GroundClearance).Within(0.0001f));
        building.enabled = false; // Release every corner at once with elevated fittings.
        for (int i = 0; i < 120; i++)
        {
            end.position += new Vector3(0.01f, 0, Mathf.Sin(i) * 0.03f);
            rope.Simulate(0.02f);
            CheckGrounded(rope, Vector3.zero, Vector3.back);
        }
    }

    [Test]
    public void GroundLockUsesConfiguredPlaneAfterTeleportAndReenable()
    {
        Vector3 origin = new Vector3(100, 100, 3);
        Vector3 up = new Vector3(0, 1, 1).normalized;
        var source = Make("Source", origin + up).transform;
        var end = Make("Nozzle", origin + Vector3.right * 4 + up * 2).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, origin, up);
        CheckGrounded(rope, origin, up);
        end.position += Vector3.right * 15;
        rope.Simulate(0.02f);
        CheckGrounded(rope, origin, up);
        rope.enabled = false;
        rope.enabled = true;
        CheckGrounded(rope, origin, up);
    }

    private static void CheckStretch(HoseRope rope)
    {
        for (int i = 1; i < rope.PointCount; i++)
        {
            Vector3 offset = rope.GetPoint(i) - rope.GetPoint(i - 1);
            // End spans include the rise to the source/nozzle; the interior stays flat.
            if (i == 1 || i == rope.PointCount - 1) offset.z = 0;
            Assert.That(offset.magnitude, Is.LessThan(rope.GetSegmentRestLength(i - 1) * 1.2f + 0.005f), $"Segment {i} stretched too far");
        }
    }

    [UnityTest]
    public IEnumerator MissingEndpointHidesRopeAndReenableRebuildsSafely()
    {
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(105, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, Vector3.zero, Vector3.back);
        rope.enabled = false;
        Assert.That(rope.GetComponent<LineRenderer>().enabled, Is.False);
        rope.enabled = true;
        yield return null;
        yield return null;
        Assert.That(rope.GetComponent<LineRenderer>().enabled, Is.True);
        Object.Destroy(end.gameObject);
        yield return null;
        yield return null;
        Assert.That(rope.GetComponent<LineRenderer>().enabled, Is.False);
    }

#if UNITY_EDITOR
    [UnityTest]
    public IEnumerator TargetSceneConnectsTwoPlayersAndKeepsConnectionAcrossNozzleSwitch()
    {
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Scenes/Isometric HoseRope Jake.unity", new LoadSceneParameters(LoadSceneMode.Single));
        yield return null;
        var manager = Object.FindAnyObjectByType<PlayerInputManager>();
        manager.DisableJoining();
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var first = PlayerInput.Instantiate(prefab, playerIndex: 0);
        var second = PlayerInput.Instantiate(prefab, playerIndex: 1);
        created.Add(first.gameObject);
        created.Add(second.gameObject);
        yield return null;
        yield return null;
        var supply = first.GetComponent<PlayerHoseConnection>().Source;
        Assert.That(supply.ConnectedPlayers, Is.EqualTo(2));
        var rope = first.GetComponentInChildren<HoseRope>();
        Assert.That(rope, Is.Not.Null);
        CheckGrounded(rope, supply.transform.position, Vector3.back);
        Assert.That(rope.GroundClearance, Is.LessThan(rope.Radius));
        Assert.That(second.GetComponentInChildren<HoseRope>(), Is.Not.SameAs(rope));
        var loadout = first.GetComponent<HoseLoadout>();
        loadout.Equip(NozzleType.Jet);
        yield return null;
        Assert.That(rope.Nozzle, Is.EqualTo(loadout.Current.NozzleTransform));
        Assert.That(rope.GetPoint(0), Is.EqualTo(rope.Source.position));
        Object.Destroy(second.gameObject);
        yield return null;
        yield return null;
        Assert.That(supply.ConnectedPlayers, Is.EqualTo(1));
    }
#endif
}
