using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class PlayerBurningTests
{
    private readonly List<GameObject> objects = new List<GameObject>();
    private PlayerHealth Player()
    {
        var go = new GameObject("Burning test player");
        objects.Add(go);
        go.AddComponent<Rigidbody2D>().gravityScale = 0;
        return go.AddComponent<PlayerHealth>();
    }
    private static void Set(PlayerHealth player, string field, object value) =>
        typeof(PlayerHealth).GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(player, value);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var go in objects) Object.Destroy(go);
        objects.Clear();
        yield return null;
    }

    [Test]
    public void OnlyAcceptedEnemyDamageBuildsFireAndIgnitionNotifiesOnce()
    {
        var player = Player();
        int ignitions = 0;
        float buildup = 0;
        player.Ignited += () => ignitions++;
        player.FireBuildupChanged += (value, maximum) => buildup = value;
        player.TakeDamage(1);
        Assert.That(player.FireBuildup, Is.Zero);
        player.TakeEnemyDamage(10);
        player.TakeEnemyDamage(10); // shared contact immunity
        Assert.That(buildup, Is.EqualTo(10));
        Set(player, "nextContactTime", 0f);
        Set(player, "<RollInvulnerable>k__BackingField", true);
        player.TakeEnemyDamage(20);
        Assert.That(buildup, Is.EqualTo(10));
        Set(player, "<RollInvulnerable>k__BackingField", false);
        player.TakeEnemyDamage(20);
        Assert.That(player.IsBurning, Is.True);
        Assert.That(player.FireBuildupProgress, Is.EqualTo(1));
        Assert.That(ignitions, Is.EqualTo(1));
    }

    [Test]
    public void TeammatesCombineWaterAndExtinguishingResetsBothMeters()
    {
        var player = Player(); var first = Player(); var second = Player();
        player.TakeEnemyDamage(30);
        int extinguished = 0;
        player.Extinguished += () => extinguished++;
        player.ApplyExtinguishingWater(player, 100);
        player.ApplyExtinguishingWater(first, float.NaN);
        Assert.That(player.ExtinguishProgress, Is.Zero);
        player.ApplyExtinguishingWater(first, 20);
        Assert.That(player.ExtinguishProgress, Is.EqualTo(20f / 45f).Within(0.001f));
        player.ApplyExtinguishingWater(second, 24);
        Assert.That(player.IsBurning, Is.True);
        player.ApplyExtinguishingWater(first, 1);
        Assert.That(player.IsBurning, Is.False);
        Assert.That(player.ExtinguishProgress, Is.Zero);
        Assert.That(player.FireBuildup, Is.Zero);
        player.Extinguish();
        Assert.That(extinguished, Is.EqualTo(1));
    }

    [UnityTest]
    public IEnumerator BurningHurtsUntilExtinguishedAndDeathClearsIt()
    {
        var player = Player();
        player.TakeEnemyDamage(30);
        float before = player.CurrentHealth;
        yield return new WaitForSeconds(0.15f);
        Assert.That(player.CurrentHealth, Is.LessThan(before));
        player.Extinguish();
        before = player.CurrentHealth;
        yield return new WaitForSeconds(0.15f);
        Assert.That(player.CurrentHealth, Is.EqualTo(before));
        Set(player, "nextContactTime", 0f);
        player.TakeEnemyDamage(30);
        player.TakeDamage(1000);
        Assert.That(player.IsDead, Is.True);
        Assert.That(player.IsBurning, Is.False);
        Assert.That(player.FireBuildup, Is.Zero);
    }
}
