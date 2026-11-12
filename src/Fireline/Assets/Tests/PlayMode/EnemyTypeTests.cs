using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.TestTools;

public class EnemyTypeTests
{
    private readonly List<Object> created = new List<Object>();

    private static void Set(object target, string field, object value)
    {
        for (var type = target.GetType(); type != null; type = type.BaseType)
        {
            var info = type.GetField(field, BindingFlags.NonPublic | BindingFlags.Instance);
            if (info == null) continue;
            info.SetValue(target, value);
            return;
        }
        Assert.Fail($"No field {field} on {target.GetType().Name}");
    }

    private T Asset<T>() where T : ScriptableObject
    {
        var asset = ScriptableObject.CreateInstance<T>();
        created.Add(asset);
        return asset;
    }

    private GameObject Make(string name, Vector2 position)
    {
        var go = new GameObject(name);
        go.transform.position = position;
        created.Add(go);
        return go;
    }

    private EnemyDefinition Definition(float rise = 0f, float keepDistance = 0f, EnemyAbility[] abilities = null)
    {
        var definition = Asset<EnemyDefinition>();
        Set(definition, "riseDuration", rise);
        Set(definition, "keepDistance", keepDistance);
        Set(definition, "abilities", abilities ?? new EnemyAbility[0]);
        return definition;
    }

    private HordeEnemy Enemy(Vector2 position, EnemyDefinition definition)
    {
        var go = Make("Test enemy", position);
        go.SetActive(false);
        go.AddComponent<CircleCollider2D>().isTrigger = true;
        var enemy = go.AddComponent<HordeEnemy>();
        enemy.SetDefinition(definition);
        go.SetActive(true);
        return enemy;
    }

    private PlayerHealth Player(Vector2 position)
    {
        var go = Make("Test player", position);
        go.tag = "Player";
        go.AddComponent<PlayerInput>();
        go.AddComponent<Rigidbody2D>().gravityScale = 0;
        go.AddComponent<BoxCollider2D>();
        return go.AddComponent<PlayerHealth>();
    }

    private void Manager() => created.Add(Make("Horde manager", Vector2.zero).AddComponent<HordeManager>().gameObject);

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in created) if (obj != null) Object.Destroy(obj);
        created.Clear();
        // Summons, projectiles and dropped fires made during the test.
        foreach (var e in Object.FindObjectsByType<HordeEnemy>(FindObjectsSortMode.None)) Object.Destroy(e.gameObject);
        foreach (var p in Object.FindObjectsByType<EnemyProjectile>(FindObjectsSortMode.None)) Object.Destroy(p.gameObject);
        foreach (var f in Object.FindObjectsByType<TemporaryFire>(FindObjectsSortMode.None)) Object.Destroy(f.gameObject);
        yield return null;
    }

    private static EnemyProjectile.Settings Shot(float damage = 10f, float reflectDamage = 5f) => new EnemyProjectile.Settings
    {
        Speed = 8f, Damage = damage, Lifetime = 3f, Radius = 0.3f, ReflectSpeedMultiplier = 1.5f, ReflectDamage = reflectDamage,
    };

    // ------------------------------------------------------------------ ranged

    [UnityTest]
    public IEnumerator RangedEnemyKeepsItsDistanceAndShootsThePlayer()
    {
        Manager();
        var ranged = Asset<RangedAttackAbility>();
        Set(ranged, "fireInterval", 0.2f);
        Set(ranged, "aimTime", 0.05f);
        Set(ranged, "range", 10f);
        var player = Player(new Vector2(204, 200));
        float full = player.CurrentHealth;
        var enemy = Enemy(new Vector2(200, 200), Definition(keepDistance: 6f, abilities: new EnemyAbility[] { ranged }));

        yield return new WaitForSeconds(1.5f);
        Assert.That(player.CurrentHealth, Is.LessThan(full), "Shots should land.");
        Assert.That(Vector2.Distance(enemy.transform.position, player.transform.position), Is.GreaterThan(3.5f),
            "A ranged enemy hangs back instead of walking into melee.");
    }

    [UnityTest]
    public IEnumerator WallsStopProjectiles()
    {
        var player = Player(new Vector2(306, 300));
        float full = player.CurrentHealth;
        var wall = Make("Wall", new Vector2(303, 300));
        wall.layer = LayerMask.NameToLayer(Structure.ObstacleLayer);
        wall.AddComponent<BoxCollider2D>().size = new Vector2(0.3f, 4f);
        Physics2D.SyncTransforms();

        EnemyProjectile.Create(null, Color.white, 0.5f, 0.5f).Launch(new Vector2(300, 300), Vector2.right, Shot());
        yield return new WaitForSeconds(1f);
        Assert.That(player.CurrentHealth, Is.EqualTo(full));
        Assert.That(Object.FindAnyObjectByType<EnemyProjectile>(), Is.Null, "The shot is gone after hitting the wall.");
    }

    [UnityTest]
    public IEnumerator ParriedProjectileFliesBackAndHurtsEnemies()
    {
        var shooter = Enemy(new Vector2(400, 400), Definition());
        float full = shooter.CurrentHealth;
        var shot = EnemyProjectile.Create(null, Color.white, 0.5f, 0.5f);
        shot.Launch(new Vector2(402, 400), Vector2.right, Shot(reflectDamage: 4f));

        // The player swings, aiming back at the shooter.
        var swing = new AxeHit(null, new Vector2(403, 400), Vector2.left, Vector2.left, new Vector2(402, 400), 10f, 0f);
        Assert.That(shot.OnAxeHit(swing), Is.EqualTo(AxeHitResult.Parried));
        Assert.That(shot.IsReflected, Is.True);
        Assert.That(shot.Velocity.x, Is.LessThan(0f), "Flies back the way the player aimed.");
        Assert.That(shot.OnAxeHit(swing), Is.EqualTo(AxeHitResult.Ignored), "Can't be parried twice.");

        yield return new WaitForSeconds(0.6f);
        Assert.That(shooter.CurrentHealth, Is.EqualTo(full - 4f));
    }

    [UnityTest]
    public IEnumerator RollingPlayersDodgeShots()
    {
        var player = Player(new Vector2(503, 500));
        float full = player.CurrentHealth;
        typeof(PlayerHealth).GetProperty("RollInvulnerable", BindingFlags.NonPublic | BindingFlags.Instance)
            .SetValue(player, true);
        EnemyProjectile.Create(null, Color.white, 0.5f, 0.5f).Launch(new Vector2(500, 500), Vector2.right, Shot());
        yield return new WaitForSeconds(0.8f);
        Assert.That(player.CurrentHealth, Is.EqualTo(full));
    }

    // ------------------------------------------------------------------ summoner

    [UnityTest]
    public IEnumerator SummonerCallsRisingEnemiesUpToItsLimit()
    {
        Manager();
        var minion = Definition(rise: 0.5f);
        var summon = Asset<SummonAbility>();
        Set(summon, "summonType", minion);
        Set(summon, "countPerSummon", 2);
        Set(summon, "interval", 0.5f);
        Set(summon, "maxAlive", 3);
        Set(summon, "channelTime", 0f);
        Player(new Vector2(608, 600));
        Enemy(new Vector2(600, 600), Definition(keepDistance: 6f, abilities: new EnemyAbility[] { summon }));

        HordeEnemy first = null;
        for (float waited = 0f; waited < 2f && first == null; waited += Time.deltaTime)
        {
            yield return null;
            foreach (var e in Object.FindObjectsByType<HordeEnemy>(FindObjectsSortMode.None))
                if (e.Definition == minion) first = e;
        }
        Assert.That(first, Is.Not.Null, "Should have summoned.");
        Assert.That(first.IsRising, Is.True, "Summons climb out of the ground.");
        Assert.That(first.ContactDamage, Is.Zero, "No contact damage while rising.");

        yield return new WaitForSeconds(2f);
        int count = 0;
        foreach (var e in Object.FindObjectsByType<HordeEnemy>(FindObjectsSortMode.None))
            if (e.Definition == minion && e.isActiveAndEnabled) count++;
        Assert.That(count, Is.EqualTo(3), "Stops at Max Alive.");
        Assert.That(first.IsRising, Is.False);
        Assert.That(first.ContactDamage, Is.GreaterThan(0f));
    }

    [UnityTest]
    public IEnumerator RisingEnemiesStayPutUntilFullyUp()
    {
        Manager();
        Player(new Vector2(705, 700));
        var enemy = Enemy(new Vector2(700, 700), Definition(rise: 0.5f));
        Vector3 start = enemy.transform.position;
        yield return new WaitForSeconds(0.3f);
        Assert.That(enemy.transform.position, Is.EqualTo(start));
        yield return new WaitForSeconds(0.6f);
        Assert.That(enemy.transform.position, Is.Not.EqualTo(start), "Walks once risen.");
    }

    // ------------------------------------------------------------------ fire starter

    [UnityTest]
    public IEnumerator FireStarterDropsShortLivedFiresWithACap()
    {
        Manager();
        var template = Make("Fire template", new Vector2(-900, -900));
        template.AddComponent<BoxCollider2D>();
        var firePrefab = template.AddComponent<FireZone>();

        var trail = Asset<FireTrailAbility>();
        Set(trail, "firePrefab", firePrefab);
        Set(trail, "interval", 0.2f);
        Set(trail, "fireLifetime", 0.8f);
        Set(trail, "minSpacing", 0f);
        Set(trail, "maxPerEnemy", 2);
        Enemy(new Vector2(800, 800), Definition(abilities: new EnemyAbility[] { trail }));

        yield return new WaitForSeconds(0.7f);
        Assert.That(TemporaryFire.ActiveCount, Is.InRange(1, 2), "Drops fires, but no more than Max Per Enemy.");
        var dropped = Object.FindAnyObjectByType<TemporaryFire>().GetComponent<FireZone>();
        Assert.That(dropped.IsBurning, Is.True);
        Assert.That(dropped.transform.localScale.x, Is.LessThan(1f), "Small fires.");

        // Put one out: it goes away shortly after.
        dropped.ApplyWater(100000f);
        yield return new WaitForSeconds(1.2f);
        Assert.That(dropped == null, Is.True);
    }
}
