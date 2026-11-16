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
