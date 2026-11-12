using System.Collections;
using System.Collections.Generic;
using Fireline.Missions;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;

public class CivilianAppearanceTests
{
    private readonly List<GameObject> instances = new List<GameObject>();
    private CivilianAppearance Spawn()
    {
        var prefab = Resources.Load<CivilianEscort>("Civilians/Civilian");
        Assert.That(prefab, Is.Not.Null);
        var civilian = Object.Instantiate(prefab);
        instances.Add(civilian.gameObject);
        return civilian.GetComponentInChildren<CivilianAppearance>();
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var instance in instances) Object.Destroy(instance);
        instances.Clear();
        yield return null;
    }

    [Test]
    public void PrefabRandomizesAcrossAllThreeCompleteCivilianAppearances()
    {
        var previous = Random.state;
        try
        {
            Random.InitState(753);
            var selected = new HashSet<int>();
            for (int i = 0; i < 36; i++)
            {
                var appearance = Spawn();
                Assert.That(appearance.VariantCount, Is.EqualTo(3));
                selected.Add(appearance.SelectedVariant);
                var sprite = appearance.Renderer.sprite;
                Assert.That(sprite.rect.height, Is.EqualTo(103));
                Assert.That(sprite.rect.width, Is.GreaterThanOrEqualTo(54), "Full poses must include hands.");
                Assert.That(sprite.texture.name, Is.EqualTo($"fireline_civiliansprites_civilian{appearance.SelectedVariant + 1}"));
            }
            Assert.That(selected.Count, Is.EqualTo(3));
        }
        finally { Random.state = previous; }
    }

    [UnityTest]
    public IEnumerator SwitchingVariantsAndTurningKeepTheChosenCivilianAndOriginalColors()
    {
        var appearance = Spawn();
        for (int variant = 0; variant < 3; variant++)
        {
            appearance.SetVariant(variant);
            foreach (var direction in new[] { Vector3.down, Vector3.left, Vector3.up, Vector3.right })
            {
                var position = appearance.transform.parent.position + direction;
                appearance.transform.parent.position = position;
                appearance.transform.parent.GetComponent<Rigidbody2D>().position = position;
                Physics2D.SyncTransforms();
                yield return null;
                yield return null;
                int expectedPose = direction == Vector3.down ? 0 : direction == Vector3.up ? 4 : 2;
                Assert.That(appearance.Renderer.sprite.name, Is.EqualTo($"CivilianPose_{expectedPose}"));
                Assert.That(appearance.Renderer.flipX, Is.EqualTo(direction == Vector3.right));
                Assert.That(appearance.SelectedVariant, Is.EqualTo(variant));
                Assert.That(appearance.Renderer.sprite.texture.name, Is.EqualTo($"fireline_civiliansprites_civilian{variant + 1}"));
                Assert.That(appearance.Renderer.color, Is.EqualTo(Color.white));
                Assert.That(appearance.Renderer.sprite.pivot.y, Is.Zero);
            }
        }
        appearance.SetVariant(-1);
        Assert.That(appearance.SelectedVariant, Is.EqualTo(2));
    }
}
