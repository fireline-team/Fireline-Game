using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.VFX;

public sealed class HoseVfxTests
{
    private Scene _scene;
    private readonly List<GameObject> _objects = new List<GameObject>();

    [UnitySetUp]
    public IEnumerator Setup()
    {
        if (!SystemInfo.supportsComputeShaders) Assert.Ignore("VFX Graph requires compute shader support.");
        Assert.That(Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath), Is.Not.Null,
            "Run Fireline > VFX > Create Missing Hose Graphs before testing.");
        _scene = SceneManager.CreateScene("Hose VFX tests");
        yield return null;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (GameObject go in _objects) if (go != null) Object.Destroy(go);
        _objects.Clear();
        if (_scene.IsValid()) yield return SceneManager.UnloadSceneAsync(_scene);
    }

    private GameObject NewObject(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        SceneManager.MoveGameObjectToScene(go, _scene);
        _objects.Add(go);
        return go;
    }

    [UnityTest]
    public IEnumerator StreamUsesWallDistanceAndStopsOnReleaseAndDisable()
    {
        GameObject go = NewObject("Standard", new Vector2(100, 100));
        go.SetActive(false);
        var hose = go.AddComponent<StandardHose>();
        typeof(HoseWeapon).GetField("blockingLayers", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(hose, (LayerMask)(1 << 0));
        var wall = NewObject("Wall", new Vector2(102, 100)).AddComponent<BoxCollider2D>();
        wall.size = new Vector2(0.2f, 2f);
        go.SetActive(true);
        Physics2D.SyncTransforms();
        hose.Spray(Vector2.right, 0.02f);
        Assert.That(hose.IsStreamActive, Is.True);
        var graph = go.GetComponentInChildren<HoseGraphVisual>();
        Assert.That(graph, Is.Not.Null);
        VisualEffect water = graph.transform.Find("Water").GetComponent<VisualEffect>();
        Assert.That(water.GetFloat("Range"), Is.EqualTo(1.9f).Within(0.02f));
        hose.StopSpraying();
        Assert.That(hose.IsStreamActive, Is.False);
        hose.Spray(Vector2.right, 0.02f);
        hose.enabled = false;
        Assert.That(hose.IsStreamActive, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator WaterCreatesSteamOnlyForBurningContactAndReusesTheEffect()
    {
        var hose = NewObject("Standard", new Vector2(100, 100)).AddComponent<StandardHose>();
        var fire = NewObject("Fire", new Vector2(102, 100)).AddComponent<FireZone>();
        hose.Spray(Vector2.right, 0.02f);
        int count = hose.GetComponentsInChildren<VisualEffect>().Length;
        Assert.That(count, Is.EqualTo(3), "Water, splash, and one fire-contact steam instance.");
        for (int i = 0; i < 10; i++) hose.Spray(Vector2.right, 0.02f);
        Assert.That(hose.GetComponentsInChildren<VisualEffect>().Length, Is.EqualTo(count));
        Assert.That(fire.RemainingStrength, Is.LessThan(60f));
        fire.ApplyWater(1000f);
        hose.Spray(Vector2.right, 0.02f);
        Assert.That(hose.GetComponentsInChildren<VisualEffect>().Length, Is.EqualTo(count));
        yield return null;
    }

    [UnityTest]
    public IEnumerator NozzlesUseDistinctAssetsAndJetDoesNotBecomeAContinuousStream()
    {
        var library = Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath);
        var standard = NewObject("Standard", new Vector2(100, 100)).AddComponent<StandardHose>();
        var mist = NewObject("Mist", new Vector2(100, 110)).AddComponent<MistHose>();
        var jet = NewObject("Jet", new Vector2(100, 120)).AddComponent<JetHose>();
        standard.Spray(Vector2.right, 0.02f);
        mist.Spray(Vector2.right, 0.02f);
        jet.Spray(Vector2.right, 0.02f);
        Assert.That(standard.GetComponentInChildren<HoseGraphVisual>().transform.Find("Water")
            .GetComponent<VisualEffect>().visualEffectAsset, Is.SameAs(library.Stream));
        Assert.That(mist.GetComponentInChildren<HoseGraphVisual>().transform.Find("Water")
            .GetComponent<VisualEffect>().visualEffectAsset, Is.SameAs(library.Mist));
        Assert.That(jet.GetComponentInChildren<HoseGraphVisual>().transform.Find("Water")
            .GetComponent<VisualEffect>().visualEffectAsset, Is.SameAs(library.Burst));
        Assert.That(jet.IsStreamActive, Is.False);
        Assert.That(jet.IsReady, Is.False);
        jet.StopSpraying();
        yield return null;
    }
}
