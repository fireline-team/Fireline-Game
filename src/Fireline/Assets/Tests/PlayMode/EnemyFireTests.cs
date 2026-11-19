#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Game.Runtime;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;

public class EnemyFireTests
{
    private readonly List<GameObject> _created = new List<GameObject>();

    private HordeEnemy Spawn(int index = 0)
    {
        var prefab = AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/HordeEnemy.prefab");
        var root = Object.Instantiate(prefab, new Vector3(100f + index * 0.1f, 100f, 0f), Quaternion.identity);
        _created.Add(root);
        return root.GetComponent<HordeEnemy>();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var root in _created) if (root != null) Object.Destroy(root);
        _created.Clear();
        yield return null;
    }

    private static void FinishRise(HordeEnemy enemy)
    {
        var rise = typeof(HordeEnemy).GetField("_rise", BindingFlags.NonPublic | BindingFlags.Instance).GetValue(enemy);
        rise.GetType().GetMethod("Finish").Invoke(rise, null);
    }

    [UnityTest]
    public IEnumerator HealthShrinksArtAxeMakesNoSteamAndReuseRestoresSize()
    {
        var enemy = Spawn();
        FinishRise(enemy);
        Vector3 full = enemy.Sprite.transform.localScale;
        Vector3 body = enemy.transform.localScale;
        var collider = enemy.GetComponent<Collider2D>();
        Physics2D.SyncTransforms(); // Include the definition's randomized spawn size in the baseline.
        Vector3 colliderSize = collider.bounds.size;
        enemy.OnAxeHit(new AxeHit(null, Vector2.zero, Vector2.right, Vector2.right,
            enemy.transform.position, enemy.CurrentHealth * 0.5f, 0f));
        yield return new WaitForSeconds(0.25f);
        Assert.That(enemy.Sprite.transform.localScale.x, Is.EqualTo(full.x * 0.675f).Within(0.01f));
        Assert.That(enemy.Sprite.transform.localScale.y, Is.EqualTo(full.y * 0.675f).Within(0.01f));
        Assert.That(enemy.transform.localScale, Is.EqualTo(body));
        Assert.That(collider.bounds.size, Is.EqualTo(colliderSize));
        Assert.That(enemy.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>().Length, Is.Zero);
        enemy.gameObject.SetActive(false);
        enemy.gameObject.SetActive(true);
        FinishRise(enemy);
        Assert.That(enemy.Sprite.transform.localScale, Is.EqualTo(full));
        enemy.TakeDamage(enemy.CurrentHealth);
        Assert.That(enemy.gameObject.activeSelf, Is.False, "Gameplay despawns immediately.");
        var dying = Object.FindFirstObjectByType<EnemyDeathVisual>();
        Assert.That(dying, Is.Not.Null, "The visible sprite survives to shrink away.");
        _created.Add(dying.gameObject);
        Assert.That(dying.GetComponentInChildren<Collider2D>(), Is.Null);
        yield return new WaitForSeconds(0.08f);
        Assert.That(dying.transform.localScale.x, Is.InRange(0.01f, 0.99f));
        yield return new WaitForSeconds(0.25f);
        Assert.That(dying == null, Is.True);
    }

    [UnityTest]
    public IEnumerator EveryWaterNozzleCreatesLibrarySteamEvenOnLethalHits()
    {
        if (!SystemInfo.supportsComputeShaders) Assert.Ignore("Requires VFX Graph support.");
        var library = Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath);
        foreach (var type in new[] { typeof(StandardHose), typeof(MistHose), typeof(JetHose) })
        {
            var enemy = Spawn();
            FinishRise(enemy);
            enemy.TakeDamage(enemy.CurrentHealth - 0.1f);
            var owner = new GameObject(type.Name);
            _created.Add(owner);
            owner.transform.position = enemy.transform.position - Vector3.right;
            var hose = (HoseWeapon)owner.AddComponent(type);
            Physics2D.SyncTransforms();
            hose.Spray(Vector2.right, 0.1f);
            Assert.That(enemy.gameObject.activeSelf, Is.False, type.Name + " must damage the target.");
            var graph = owner.GetComponentInChildren<HoseGraphVisual>();
            var steam = graph.transform.Find("Fire contact steam");
            Assert.That(steam, Is.Not.Null, type.Name + " must emit steam before the enemy despawns.");
            var effect = steam.GetComponent<UnityEngine.VFX.VisualEffect>();
            Assert.That(effect.visualEffectAsset, Is.EqualTo(library.Steam));
            Assert.That(effect.GetFloat("PixelSize"), Is.EqualTo(library.SteamAppearance.pixelation.enabled
                ? library.SteamAppearance.pixelation.pixelSize : 1));
            hose.StopSpraying();
            yield return new WaitForSeconds(0.3f);
        }
    }

    [UnityTest]
    public IEnumerator PrefabRetainsItsSpriteAndRestoresEmissionAfterHitAndReuse()
    {
        HordeEnemy enemy = Spawn();
        var sprite = enemy.Sprite;
        Sprite art = sprite.sprite;
        var material = sprite.sharedMaterial;
        Assert.That(material.shader.name, Is.EqualTo("Fireline/EnemyFire"));
        Assert.That(art, Is.Not.Null);
        enemy.TakeDamage(0.1f);
        Assert.That(enemy.IsFlashing, Is.True);
        Assert.That(sprite.sharedMaterial, Is.Not.SameAs(material));
        typeof(HordeEnemy).GetMethod("UpdateFlash", BindingFlags.Instance | BindingFlags.NonPublic)
            .Invoke(enemy, new object[] { Time.time + 1f });
        Assert.That(sprite.sharedMaterial, Is.SameAs(material));
        yield return new WaitForSeconds(0.13f);
        enemy.TakeDamage(0.1f);
        Assert.That(enemy.IsFlashing, Is.True);
        enemy.gameObject.SetActive(false);
        Assert.That(enemy.GetComponentInChildren<Light>(true).enabled, Is.False, "Pooled fire must stop lighting the world.");
        enemy.gameObject.SetActive(true);
        Assert.That(enemy.IsFlashing, Is.False);
        Assert.That(sprite.sharedMaterial, Is.SameAs(material));
        Assert.That(sprite.sprite, Is.SameAs(art));
        yield return null;
        Assert.That(enemy.GetComponentInChildren<Light>().enabled, Is.True);
    }

    [UnityTest]
    public IEnumerator RealtimeLightFollowsTheSpriteAndFlickers()
    {
        HordeEnemy enemy = Spawn();
        var light = enemy.GetComponentInChildren<Light>();
        yield return null;
        Assert.That(light.type, Is.EqualTo(LightType.Point));
        Assert.That(light.lightmapBakeType, Is.EqualTo(LightmapBakeType.Realtime));
        Assert.That(light.shadows, Is.EqualTo(LightShadows.None));
        float minimum = float.PositiveInfinity;
        float maximum = 0f;
        for (int i = 0; i < 12; i++)
        {
            enemy.transform.position += Vector3.right * 0.1f;
            yield return null;
            Assert.That(Vector3.Distance(light.transform.position, enemy.Sprite.bounds.center + Vector3.back * 0.65f), Is.LessThan(0.01f));
            minimum = Mathf.Min(minimum, light.intensity);
            maximum = Mathf.Max(maximum, light.intensity);
        }
        Assert.That(maximum - minimum, Is.GreaterThan(0.001f));
    }

    [UnityTest]
    public IEnumerator LargeHordesRespectLightBudgetAndReuseVacatedSlots()
    {
        var enemies = new List<HordeEnemy>();
        for (int i = 0; i < EnemyFireLight.MaxRealtimeLights + 8; i++) enemies.Add(Spawn(i));
        yield return new WaitForSecondsRealtime(0.2f);
        int CountLights()
        {
            int count = 0;
            foreach (var enemy in enemies)
                if (enemy.gameObject.activeInHierarchy && enemy.GetComponentInChildren<Light>().enabled) count++;
            return count;
        }
        Assert.That(CountLights(), Is.EqualTo(EnemyFireLight.MaxRealtimeLights));
        foreach (var enemy in enemies)
        {
            if (!enemy.GetComponentInChildren<Light>().enabled) continue;
            enemy.gameObject.SetActive(false);
            break;
        }
        yield return new WaitForSecondsRealtime(0.2f);
        Assert.That(CountLights(), Is.EqualTo(EnemyFireLight.MaxRealtimeLights));
        foreach (var enemy in enemies)
            Assert.That(enemy.Sprite.sharedMaterial.shader.name, Is.EqualTo("Fireline/EnemyFire"), "Every enemy remains emissive, including those outside the light budget.");
    }
}
#endif
