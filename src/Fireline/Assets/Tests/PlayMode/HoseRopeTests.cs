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
    public IEnumerator ChainFallsToGroundWithPinnedEndsAndLimitedSegmentStretch()
    {
        var source = Make("Source", new Vector3(100, 100, -0.2f)).transform;
        var end = Make("Nozzle", new Vector3(105, 100, -0.6f)).transform;
        var rope = Make("Rope", Vector3.zero).AddComponent<HoseRope>();
        rope.Bind(source, end, new Vector3(100, 100, 0), Vector3.back);
        for (int step = 0; step < 180; step++) rope.Simulate(0.02f);
        Assert.That(rope.GetPoint(0), Is.EqualTo(source.position));
        Assert.That(rope.GetPoint(rope.PointCount - 1), Is.EqualTo(end.position));
        int grounded = 0;
        for (int i = 1; i < rope.PointCount - 1; i++)
        {
            Assert.That(rope.GetPoint(i).z, Is.LessThanOrEqualTo(-rope.Radius + 0.001f));
            if (Mathf.Abs(rope.GetPoint(i).z + rope.Radius) < 0.005f) grounded++;
        }
        Assert.That(grounded, Is.GreaterThan(rope.PointCount / 2));
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

    private static void CheckStretch(HoseRope rope)
    {
        float spacing = rope.PaidOutLength / (rope.PointCount - 1);
        for (int i = 1; i < rope.PointCount; i++)
            Assert.That(Vector3.Distance(rope.GetPoint(i - 1), rope.GetPoint(i)),
                Is.LessThan(spacing * 1.2f + 0.005f), $"Segment {i} stretched too far");
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
