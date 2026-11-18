using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using Fireline.Missions;
using Game.Runtime;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.Tilemaps;
using TMPro;
using UnityEngine.UI;
using System.Linq;

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

    [UnityTest]
    public IEnumerator SquareHudRevealsTargetsStrikesCompletedTasksAndAddsExtraction()
    {
        var map = Map(out var camera);
        var fire1 = Make("Fire 1", new Vector3(0, 0)).AddComponent<FireZone>();
        var fire2 = Make("Fire 2", new Vector3(2, 0)).AddComponent<FireZone>();
        var fireStep = Make("Fires").AddComponent<FireObjectiveStep>();
        Set(fireStep, "fires", new[] { fire1, fire2, fire1 });
        var crate1 = Make("Crate 1", new Vector3(30, 0)).AddComponent<Debris>();
        var crate2 = Make("Crate 2", new Vector3(32, 0)).AddComponent<Debris>();
        var crateStep = Make("Crates").AddComponent<DebrisObjectiveStep>();
        Set(crateStep, "debris", new[] { crate1, crate2 });
        var fires = Make("Fire site").AddComponent<MissionObjectiveSite>();
        Set(fires, "steps", new MissionObjectiveStep[] { fireStep });
        var crates = Make("Crate site", new Vector3(30, 0)).AddComponent<MissionObjectiveSite>();
        Set(crates, "steps", new MissionObjectiveStep[] { crateStep });
        var spawn = Make("Spawn", new Vector3(-50, 0)).AddComponent<MissionPoint>();
        var mission = Make("Checklist mission").AddComponent<FireRescueMission>();
        Set(mission, "spawnPoints", new[] { spawn }); Set(mission, "objectivePoints", new[] { fires, crates });
        Set(mission, "objectiveCount", 2);
        var player = Make("Player").AddComponent<PlayerHealth>();
        yield return null;
        player.GetComponent<Rigidbody2D>().simulated = false;
        map.RefreshSources(); map.RenderMap();
        Assert.That(map.MissionHud.Entries.Count, Is.EqualTo(2));
        Assert.That(map.MissionHud.Entries.Any(e => e.Label.Contains("Put out fires")), Is.True);
        Assert.That(map.MissionHud.Entries.Any(e => e.Label.Contains("Break crates")), Is.True);
        var root = map.transform.Find("Pixel minimap HUD");
        var panel = root.Find("Tactical map").GetComponent<RectTransform>();
        Assert.That(panel.Find("Heading"), Is.Null); Assert.That(panel.Find("Legend"), Is.Null);
        Assert.That(panel.sizeDelta.x, Is.EqualTo(panel.sizeDelta.y));
        Set(map, "minimapSizePixels", 200); map.RenderMap(); Canvas.ForceUpdateCanvases();
        Assert.That(panel.sizeDelta, Is.EqualTo(new Vector2(200, 200)));
        var image = panel.Find("Map pixels").GetComponent<RectTransform>();
        Assert.That(image.rect.width, Is.EqualTo(image.rect.height));
        Assert.That(root.Find("Mission countdown").GetComponent<RectTransform>().sizeDelta.x, Is.EqualTo(200));
        player.transform.position = new Vector3(1, 0); map.RenderMap();
        Set(map, "timerFontSize", 60); Set(map, "objectiveFontSize", 34); Set(map, "subtaskFontSize", 29);
        map.RenderMap(); Canvas.ForceUpdateCanvases();
        var countdown = root.Find("Mission countdown/Time remaining").GetComponent<TMP_Text>();
        Assert.That(countdown.fontSize, Is.EqualTo(60));
        Assert.That(countdown.enableAutoSizing, Is.False);
        var parentText = root.GetComponentsInChildren<TMP_Text>(true).First(t => t.text.Contains("Put out fires"));
        var childText = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.text == "Fire 1");
        Assert.That(parentText.fontSize, Is.EqualTo(34));
        Assert.That(childText.fontSize, Is.EqualTo(29));
        Assert.That(parentText.enableAutoSizing || childText.enableAutoSizing, Is.False);
        Assert.That(root.Find("Mission countdown").GetComponent<RectTransform>().rect.height, Is.GreaterThan(60));
        // Editing an Inspector value during play updates the already-created text too.
        Set(map, "timerFontSize", 44); map.RenderMap();
        Assert.That(countdown.fontSize, Is.EqualTo(44));
        Assert.That(map.MissionHud.Entries.Count(e => e.Child), Is.EqualTo(2), "Duplicate target references do not duplicate rows.");
        fire1.ApplyWater(10000); map.RenderMap();
        Assert.That(map.MissionHud.Entries.Single(e => e.Label == "Fire 1").Complete, Is.True);
        var finishedText = root.GetComponentsInChildren<TMP_Text>(true).Single(t => t.text.Contains("<b>Fire 1</b>"));
        finishedText.ForceMeshUpdate(true);
        Assert.That(finishedText.textInfo.characterCount, Is.GreaterThan(0));
        Assert.That(finishedText.textInfo.characterInfo[0].strikethroughColor, Is.EqualTo(new Color32(255, 77, 87, 255)));
        Assert.That((finishedText.textInfo.characterInfo[0].style & FontStyles.Strikethrough) != 0, Is.True);
        fire2.ApplyWater(10000); map.RenderMap();
        Assert.That(map.MissionHud.Entries.Single(e => !e.Child && e.Label.Contains("Put out fires")).Complete, Is.True);
        Assert.That(root.GetComponentsInChildren<TMP_Text>(true).Any(t => t.text.Contains("Put out fires") && t.text.Contains("COMPLETE")), Is.True);
        player.transform.position = new Vector3(31, 0);
        camera.transform.position += Vector3.right * 25;
        map.RenderMap();
        Assert.That(map.MissionHud.Entries.Count(e => e.Child), Is.EqualTo(4), "Visited checklist remains expanded.");
        Assert.That(Count(map, MinimapSymbols.Crate), Is.GreaterThan(0));
        crate1.Break(); map.RenderMap();
        Assert.That(map.MissionHud.Entries.Single(e => e.Label == "Crate 1").Complete, Is.True);
        Assert.That(map.MissionHud.Entries.Single(e => e.Label == "Crate 2").Complete, Is.False);
        crate2.Break(); map.RenderMap();
        Assert.That(mission.State, Is.EqualTo(FireRescueMission.Phase.Evacuate));
        Assert.That(map.MissionHud.Entries.Last().Symbol, Is.EqualTo('E'));
        Assert.That(map.MissionHud.Entries.Last().Complete, Is.False);
        Assert.That(map.MissionHud.Entries.Take(map.MissionHud.Entries.Count - 1).All(e => e.Complete), Is.True);
        var evacIcon = root.GetComponentsInChildren<RawImage>().Last(i => i.name == "Map indicator");
        Assert.That(evacIcon.texture.filterMode, Is.EqualTo(FilterMode.Point));
        yield return new WaitForSeconds(0.05f);
        Assert.That(mission.ElapsedSeconds, Is.GreaterThan(0));
        float elapsed = mission.ElapsedSeconds;
        try
        {
            Time.timeScale = 0;
            yield return new WaitForSecondsRealtime(0.05f);
            Assert.That(mission.ElapsedSeconds, Is.EqualTo(elapsed));
        }
        finally { Time.timeScale = 1; }
        Assert.That(MinimapMissionHud.FormatRemaining(600, 0), Is.EqualTo("10:00"));
        Assert.That(MinimapMissionHud.FormatRemaining(600, 61), Is.EqualTo("08:59"));
        Assert.That(MinimapMissionHud.FormatRemaining(600, 999), Is.EqualTo("00:00"));
        mission.BeginMission(); map.RenderMap();
        Assert.That(map.MissionHud.TimerText, Is.EqualTo("10:00"));
        Assert.That(map.MissionHud.Entries.Count, Is.EqualTo(2));
        Assert.That(map.MissionHud.Entries.Any(e => e.Complete), Is.False);
    }

    [Test]
    public void CiviliansAppearOnMapAndAtEdgesUntilSavedOrLost()
    {
        var map = Map(out _);
        var site = Make("Civilian owner").AddComponent<MissionObjectiveSite>();
        var civilian = Make("Civilian", new Vector3(10, 10)).AddComponent<CivilianEscort>();
        civilian.Initialize(site, null);
        map.RefreshSources(); map.RenderMap();
        Assert.That(Count(map, MinimapSymbols.Civilian), Is.GreaterThan(0));
        civilian.transform.position = new Vector3(100, 100); map.RenderMap();
        Assert.That(Count(map, MinimapSymbols.Civilian), Is.GreaterThan(0), "Off-map civilians remain at the border.");
        civilian.TakeDamage(10000); map.RenderMap();
        Assert.That(civilian.IsResolved, Is.True);
        Assert.That(Count(map, MinimapSymbols.Civilian), Is.Zero);
        var rescued = Make("New survivor", new Vector3(-10, 10)).AddComponent<CivilianEscort>();
        rescued.Initialize(site, null); map.RefreshSources(); map.RenderMap();
        Assert.That(Count(map, MinimapSymbols.Civilian), Is.GreaterThan(0));
        typeof(CivilianEscort).GetMethod("Resolve", BindingFlags.NonPublic | BindingFlags.Instance).Invoke(rescued, new object[] { true });
        map.RenderMap();
        Assert.That(rescued.IsSaved, Is.True);
        Assert.That(Count(map, MinimapSymbols.Civilian), Is.Zero);
    }

    private static int Count(PixelMinimap map, Color32 color)
    {
        int count = 0;
        foreach (var pixel in map.MapTexture.GetPixels32()) if (pixel.Equals(color)) count++;
        return count;
    }
}
