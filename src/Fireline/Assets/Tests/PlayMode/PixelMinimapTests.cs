using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fireline.Missions;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;

public class PixelMinimapTests
{
    private readonly List<Object> created = new List<Object>();
    private GameObject Make(string name, Vector3 position = default)
    {
        var go = new GameObject(name); go.transform.position = position; created.Add(go); return go;
    }
    private static void Set(object obj, string field, object value) => obj.GetType()
        .GetField(field, BindingFlags.NonPublic | BindingFlags.Instance).SetValue(obj, value);
    private PixelMinimap Map(out Camera camera)
    {
        camera = Make("Map test camera", new Vector3(0, -5, -8.660254f)).AddComponent<Camera>();
        camera.tag = "MainCamera";
        camera.transform.rotation = Quaternion.Euler(-30, 0, 0);
        return Make("Minimap test").AddComponent<PixelMinimap>();
    }
    private static Color32 At(PixelMinimap map, Vector2 world)
    {
        Vector2 point = (world - map.MapCenter) / 60 * 128 + Vector2.one * 64;
        return map.MapTexture.GetPixel(Mathf.FloorToInt(point.x), Mathf.FloorToInt(point.y));
    }
    [UnityTearDown]
    public IEnumerator Cleanup()
    {
        foreach (var obj in created) if (obj != null) Object.Destroy(obj);
        created.Clear();
        yield return null;
    }

    [Test]
    public void GroundFootprintsExcludeDebrisAndMapFollowsGroundViewCenter()
    {
        var map = Map(out var camera);
        var wall = Make("Permanent wall", new Vector3(-8, 0));
        wall.layer = LayerMask.NameToLayer("Obstacles");
        wall.AddComponent<BoxCollider2D>().size = new Vector2(5, 5);
        var debris = Make("Temporary debris", new Vector3(8, 0));
        debris.AddComponent<Debris>();
        var collider = debris.GetComponent<Collider2D>();
        Assert.That(PixelMinimap.IsPermanentObstacle(collider, LayerMask.GetMask("Obstacles")), Is.False);
        Physics2D.SyncTransforms();
        map.RefreshSources(); map.RenderMap();
        Assert.That(map.MapTexture.filterMode, Is.EqualTo(FilterMode.Point));
        Assert.That(map.MapTexture.width, Is.EqualTo(128));
        Assert.That(At(map, new Vector2(-8, 0)), Is.EqualTo(new Color32(124, 137, 139, 255)));
        Assert.That(At(map, new Vector2(8, 0)), Is.EqualTo(new Color32(53, 68, 62, 255)));
        Assert.That(map.MapCenter.magnitude, Is.LessThan(0.5f));
        camera.transform.position += new Vector3(12, 7);
        map.RenderMap();
        Assert.That(Vector2.Distance(map.MapCenter, new Vector2(12, 7)), Is.LessThan(0.5f));
        var panel = map.transform.Find("Pixel minimap HUD/Tactical map").GetComponent<RectTransform>();
        Assert.That(panel.anchorMin, Is.EqualTo(new Vector2(1, 0)));
        Assert.That(panel.pivot, Is.EqualTo(new Vector2(1, 0)));
    }

    [Test]
    public void GroundTilesLeaveEmptySpaceDarkAndDensityIsBoundedAndMonotonic()
    {
        var grid = Make("Grid").AddComponent<Grid>();
        var ground = Make("Ground").AddComponent<Tilemap>(); ground.transform.SetParent(grid.transform);
        var tile = ScriptableObject.CreateInstance<Tile>(); created.Add(tile);
        ground.SetTile(Vector3Int.zero, tile);
        var map = Map(out _);
        map.RefreshSources(); map.RenderMap();
        Assert.That(At(map, new Vector2(0.5f, 0.5f)), Is.EqualTo(new Color32(53, 68, 62, 255)));
        Assert.That(At(map, new Vector2(10, 10)), Is.EqualTo(new Color32(18, 25, 29, 255)));
        Vector2 sparse = map.DensityAppearance(1), medium = map.DensityAppearance(10), dense = map.DensityAppearance(20);
        Assert.That(medium.x, Is.GreaterThan(sparse.x)); Assert.That(dense.x, Is.GreaterThan(medium.x));
        Assert.That(medium.y, Is.GreaterThan(sparse.y)); Assert.That(dense.y, Is.GreaterThan(medium.y));
        Assert.That(map.DensityAppearance(1000), Is.EqualTo(dense));
    }

    [UnityTest]
    public IEnumerator FiresAlliesEnemiesAndMissionMarkersRenderAndClear()
    {
        var map = Map(out _);
        var fire = Make("Fire", new Vector3(-10, -10)).AddComponent<FireZone>();
        var player = Make("Ally", new Vector3(10, -10)).AddComponent<PlayerHealth>();
        var enemy = Make("Enemy cluster", new Vector3(-10, 10)).AddComponent<HordeEnemy>();
        var step = Make("Objective step").AddComponent<TestObjectiveStep>();
        var site = Make("Objective", new Vector3(20, 10)).AddComponent<MissionObjectiveSite>();
        Set(site, "steps", new MissionObjectiveStep[] { step });
        var spawn = Make("Evac", new Vector3(-20, -10)).AddComponent<MissionPoint>();
        var mission = Make("Mission").AddComponent<FireRescueMission>();
        Set(mission, "spawnPoints", new[] { spawn }); Set(mission, "objectivePoints", new[] { site });
        yield return null;
        map.RefreshSources(); map.RenderMap();
        Assert.That(Count(map, new Color32(255, 161, 40, 255)), Is.GreaterThan(0));
        Assert.That(Count(map, new Color32(255, 230, 80, 255)), Is.GreaterThan(0));
        Assert.That(Count(map, PlayerHoseConnection.ColorForPlayer(0)), Is.GreaterThan(0));
        Color32 enemyPixel = At(map, enemy.transform.position);
        Assert.That(enemyPixel.r, Is.GreaterThan(enemyPixel.g * 2));
        step.Finish(); map.RenderMap();
        Assert.That(Count(map, new Color32(255, 230, 80, 255)), Is.Zero);
        Assert.That(Count(map, new Color32(70, 230, 240, 255)), Is.GreaterThan(0));
        fire.gameObject.SetActive(false); enemy.gameObject.SetActive(false);
        player.TakeDamage(10000); map.RenderMap();
        Assert.That(Count(map, new Color32(255, 161, 40, 255)), Is.Zero);
        Assert.That(Count(map, PlayerHoseConnection.ColorForPlayer(0)), Is.GreaterThan(0), "Downed allies remain marked.");
        // Capture the actual pixel artifact for visual inspection.
        System.IO.File.WriteAllBytes("/tmp/fireline-minimap-preview.png", map.MapTexture.EncodeToPNG());
    }

    [UnityTest]
    public IEnumerator SharedCameraPrefabCreatesOneMinimapInThePlayableLevel()
    {
#if UNITY_EDITOR
        UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
            "Assets/_Scenes/PlayTestLevel.unity", new UnityEngine.SceneManagement.LoadSceneParameters(UnityEngine.SceneManagement.LoadSceneMode.Single));
        yield return null;
        yield return null;
        var maps = Object.FindObjectsByType<PixelMinimap>(FindObjectsSortMode.None);
        Assert.That(maps.Length, Is.EqualTo(1));
        maps[0].RefreshSources(); maps[0].RenderMap();
        Assert.That(maps[0].MapTexture, Is.Not.Null);
        System.IO.File.WriteAllBytes("/tmp/fireline-minimap-level-preview.png", maps[0].MapTexture.EncodeToPNG());
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        UnityEngine.SceneManagement.SceneManager.SetActiveScene(UnityEngine.SceneManagement.SceneManager.CreateScene("Minimap cleanup"));
        yield return UnityEngine.SceneManagement.SceneManager.UnloadSceneAsync(scene);
#endif
    }

    private static int Count(PixelMinimap map, Color32 color)
    {
        int count = 0;
        foreach (var pixel in map.MapTexture.GetPixels32()) if (pixel.Equals(color)) count++;
        return count;
    }
}
