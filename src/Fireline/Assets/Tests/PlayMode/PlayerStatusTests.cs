using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

public class PlayerStatusTests
{
    private readonly List<Object> created = new List<Object>();

    private IEnumerator Load()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Scenes/Isometric HoseRope Jake.unity", new LoadSceneParameters(LoadSceneMode.Single));
#endif
        yield return null;
        Object.FindAnyObjectByType<PlayerInputManager>().DisableJoining();
    }

    private PlayerInput Join(int index)
    {
#if UNITY_EDITOR
        var prefab = UnityEditor.AssetDatabase.LoadAssetAtPath<GameObject>("Assets/_Prefabs/Player.prefab");
        var player = PlayerInput.Instantiate(prefab, playerIndex: index);
        created.Add(player.gameObject);
        player.GetComponent<PlayerController>().enabled = false;
        return player;
#else
        return null;
#endif
    }

    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in created) if (obj != null) Object.Destroy(obj);
        created.Clear();
        var scene = SceneManager.GetActiveScene();
        SceneManager.SetActiveScene(SceneManager.CreateScene("Status test cleanup"));
        yield return SceneManager.UnloadSceneAsync(scene);
    }

    [UnityTest]
    public IEnumerator FourPlayersHaveLabelsAndDeathOverridesOnlyItsOwnMarker()
    {
        yield return Load();
        var players = new PlayerInput[4];
        for (int i = 0; i < 4; i++) players[i] = Join(i);
        yield return null;
        yield return null;
        for (int i = 0; i < 4; i++)
        {
            var marker = players[i].GetComponent<PlayerStatusMarker>();
            Assert.That(marker.PlayerLabel, Is.EqualTo($"P{i + 1}"));
            Assert.That(marker.CurrentStatus, Is.Null);
            Assert.That(marker.IsVisible, Is.True);
        }
        var dead = players[1].GetComponent<PlayerStatusMarker>();
        players[1].GetComponent<PlayerHealth>().TakeDamage(10000);
        Assert.That(dead.CurrentStatus, Is.Not.Null);
        Assert.That(dead.CurrentStatus.useSkull, Is.True);
        yield return null;
        yield return null;
        Canvas.ForceUpdateCanvases();
        var skull = Object.FindObjectsByType<Canvas>()
            .Select(c => c.transform.Find("Status badge/Skull"))
            .First(t => t != null && t.gameObject.activeInHierarchy);
        var mesh = skull.GetComponent<CanvasRenderer>().GetMesh();
        Assert.That(mesh, Is.Not.Null);
        Assert.That(mesh.vertexCount, Is.GreaterThan(0), "The skull must produce visible geometry.");
        Assert.That(players[0].GetComponent<PlayerStatusMarker>().CurrentStatus, Is.Null);
        var low = ScriptableObject.CreateInstance<PlayerStatusDefinition>();
        created.Add(low);
        low.priority = 10;
        dead.SetStatus(players[1], low);
        Assert.That(dead.CurrentStatus.useSkull, Is.True, "Low-priority status cannot hide death.");
        dead.enabled = false;
        Assert.That(dead.IsVisible, Is.False);
        dead.enabled = true;
        yield return null;
        Assert.That(dead.CurrentStatus.useSkull, Is.True);
        Object.Destroy(players[1].gameObject);
        yield return null;
        yield return null;
        Assert.That(Object.FindObjectsByType<Canvas>()
            .Count(c => c.name == "Player overhead UI"), Is.EqualTo(3));
    }

    [UnityTest]
    public IEnumerator StatusPriorityClearAndDestroyedOwnerRestorePreviousStatus()
    {
        yield return Load();
        var player = Join(0);
        yield return null;
        var marker = player.GetComponent<PlayerStatusMarker>();
        var low = ScriptableObject.CreateInstance<PlayerStatusDefinition>();
        var high = ScriptableObject.CreateInstance<PlayerStatusDefinition>();
        created.Add(low); created.Add(high);
        low.priority = 10; high.priority = 20;
        var owner = new GameObject("Status producer"); created.Add(owner);
        marker.SetStatus(player, low);
        marker.SetStatus(owner, high);
        Assert.That(marker.CurrentStatus, Is.EqualTo(high));
        marker.ClearStatus(owner);
        Assert.That(marker.CurrentStatus, Is.EqualTo(low));
        marker.SetStatus(owner, high);
        Object.Destroy(owner);
        yield return null;
        yield return null;
        Assert.That(marker.CurrentStatus, Is.EqualTo(low));
        marker.ClearStatus(player);
        Assert.That(marker.CurrentStatus, Is.Null);
        Assert.That(marker.PlayerLabel, Is.EqualTo("P1"));
    }
}
