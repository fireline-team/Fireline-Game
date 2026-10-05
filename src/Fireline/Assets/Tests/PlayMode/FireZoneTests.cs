using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class FireZoneTests
{
    private readonly List<Object> created = new List<Object>();

    private GameObject NewObject(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private FireZone Fire(Vector2 position)
    {
        var go = NewObject("Fire zone", position);
        go.SetActive(false);
        var fire = go.AddComponent<FireZone>();
        var visual = new GameObject("Flames");
        visual.transform.SetParent(go.transform, false);
        typeof(FireZone).GetField("fireVisual", BindingFlags.Instance | BindingFlags.NonPublic)
            .SetValue(fire, visual.transform);
        go.SetActive(true);
        return fire;
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in created) if (obj != null) Object.Destroy(obj);
        created.Clear();
        yield return null;
    }

    [UnityTest]
    public IEnumerator WaterReducesVisualsAndExtinguishesOnceWithoutDestroyingEnvironment()
    {
        FireZone fire = Fire(new Vector2(100, 100));
        int extinguished = 0;
        fire.OnExtinguished.AddListener(() => extinguished++);
        Transform visual = fire.transform.GetChild(0);
        fire.ApplyWater(float.NaN);
        fire.ApplyWater(-5);
        Assert.That(fire.RemainingStrength, Is.EqualTo(60));
        fire.ApplyWater(30);
        Assert.That(fire.Intensity, Is.EqualTo(0.5f));
        Assert.That(visual.localScale.x, Is.LessThan(1));
        Assert.That(visual.gameObject.activeSelf, Is.True);
        fire.ApplyWater(1000);
        fire.ApplyWater(1000);
        Assert.That(extinguished, Is.EqualTo(1));
        Assert.That(fire.RemainingStrength, Is.Zero);
        Assert.That(fire.ContactDamage, Is.Zero);
        Assert.That(visual.gameObject.activeSelf, Is.False);
        Assert.That(fire.gameObject.activeSelf, Is.True);
        fire.gameObject.SetActive(false);
        fire.gameObject.SetActive(true);
        Assert.That(fire.IsBurning, Is.False, "Re-enabling must not silently reignite a cleared area.");
        fire.Ignite();
        Assert.That(fire.RemainingStrength, Is.EqualTo(60));
        Assert.That(visual.localScale, Is.EqualTo(Vector3.one));
        fire.ApplyWater(60);
        Assert.That(extinguished, Is.EqualTo(2));
        yield return null;
    }

    [UnityTest]
    public IEnumerator HoseExtinguishesOnlyOverlappingZonesOncePerTickAndCoopAddsWater()
    {
        // HoseWeapon is abstract now; StandardHose extinguishes at the default 30 per second.
        HoseWeapon hose = NewObject("Hose", new Vector2(100, 100)).AddComponent<StandardHose>();
        FireZone hit = Fire(new Vector2(102, 100));
        hit.gameObject.AddComponent<CircleCollider2D>().isTrigger = true;
        var child = new GameObject("Extra hitbox");
        child.transform.SetParent(hit.transform, false);
        child.AddComponent<BoxCollider2D>().isTrigger = true;
        FireZone behind = Fire(new Vector2(98, 100));
        FireZone outside = Fire(new Vector2(107, 100));
        hose.Spray(Vector2.right, 1f);
        Assert.That(hit.RemainingStrength, Is.EqualTo(30), "Multiple colliders must not multiply water.");
        Assert.That(behind.RemainingStrength, Is.EqualTo(60));
        Assert.That(outside.RemainingStrength, Is.EqualTo(60));
        HoseWeapon teammate = NewObject("Second hose", new Vector2(100, 100)).AddComponent<StandardHose>();
        teammate.Spray(Vector2.right, 1f);
        Assert.That(hit.IsBurning, Is.False);
        yield return null;
    }

    [UnityTest]
    public IEnumerator ClearedZoneStopsHurtingPlayerAndCanReigniteWhilePlayerStaysInside()
    {
        var go = NewObject("Player", new Vector2(100, 100));
        var player = go.AddComponent<PlayerHealth>();
        go.GetComponent<Rigidbody2D>().gravityScale = 0;
        go.AddComponent<BoxCollider2D>();
        FireZone fire = Fire(new Vector2(100, 100));
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(player.CurrentHealth, Is.EqualTo(90));
        fire.ApplyWater(60);
        yield return new WaitForSeconds(0.85f);
        Assert.That(player.CurrentHealth, Is.EqualTo(90));
        fire.Ignite();
        yield return new WaitForFixedUpdate();
        yield return null;
        Assert.That(player.CurrentHealth, Is.EqualTo(80));
    }
}