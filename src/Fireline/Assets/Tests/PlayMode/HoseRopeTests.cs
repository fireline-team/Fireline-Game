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
                delta.normalized, filter, hits, delta.magnitude), Is.Zero, $"Hose span {i} clips a building");
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

    private static void CheckGrounded(HoseRope rope, Vector3 origin, Vector3 up)
    {
        for (int i = 1; i < rope.PointCount - 1; i++)
            Assert.That(Vector3.Dot(rope.GetPoint(i) - origin, up),
                Is.EqualTo(rope.Radius).Within(0.0001f), $"Particle {i} left the ground plane");
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
            Assert.That(line.GetPosition(i).z, Is.EqualTo(-rope.Radius).Within(0.0001f));
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
        float spacing = rope.PaidOutLength / (rope.PointCount - 1);
        for (int i = 1; i < rope.PointCount; i++)
        {
            Vector3 offset = rope.GetPoint(i) - rope.GetPoint(i - 1);
            // End spans include the rise to the source/nozzle; the interior stays flat.
            if (i == 1 || i == rope.PointCount - 1) offset.z = 0;
            Assert.That(offset.magnitude, Is.LessThan(spacing * 1.2f + 0.005f), $"Segment {i} stretched too far");
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
