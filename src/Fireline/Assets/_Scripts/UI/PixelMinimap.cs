using System.Collections.Generic;
using Fireline.Missions;
using Game.Runtime;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Tilemaps;
using UnityEngine.UI;

/// <summary>North-up, XY-ground tactical map. Draws data, not tilted art or temporary debris.</summary>
[DefaultExecutionOrder(12000), DisallowMultipleComponent]
public sealed class PixelMinimap : MonoBehaviour
{
    [Header("Map")]
    [SerializeField, Range(64, 256)] private int resolution = 128;
    [SerializeField, Min(10)] private float worldSpan = 60;
    [Tooltip("Outer width and height of the square minimap in screen pixels.")]
    [SerializeField, Range(128, 512)] private int minimapSizePixels = 268;
    [Header("Mission panels")]
    [SerializeField, Min(1)] private float countdownSeconds = 600;
    [Tooltip("Reveal a site checklist when a standing teammate comes this close to the site or one of its targets.")]
    [SerializeField, Min(1)] private float objectiveRevealDistance = 8;
    [SerializeField, Min(0.05f)] private float refreshInterval = 0.1f;
    [Tooltip("Optional explicit ground tilemaps. Empty discovers non-obstacle tilemaps in this scene.")]
    [SerializeField] private Tilemap[] groundTilemaps;
    [SerializeField] private LayerMask obstacleLayers = 1 << 7;
    [Header("Enemy density")]
    [SerializeField, Min(1)] private float densityCellSize = 5;
    [SerializeField, Min(2)] private int denseEnemyCount = 20;
    [SerializeField] private Vector2 pulseFrequency = new Vector2(0.6f, 2f);

    private static readonly Color32 Ground = new Color32(53, 68, 62, 255);
    private static readonly Color32 Void = new Color32(18, 25, 29, 255);
    private static readonly Color32 Building = new Color32(124, 137, 139, 255);
    private static readonly Color32 Outline = new Color32(10, 15, 21, 255);
    private Texture2D texture;
    private Color32[] pixels;
    private Canvas canvas;
    private RectTransform panel;
    private MinimapMissionHud missionHud;
    private readonly List<ObjectiveChecklistItem> objectiveItems = new List<ObjectiveChecklistItem>();
    public MinimapMissionHud MissionHud => missionHud;
    private Camera view;
    private float nextRefresh, nextDiscovery;
    private Collider2D[] obstacles = new Collider2D[0];
    private FireZone[] fires = new FireZone[0];
    private HordeEnemy[] enemies = new HordeEnemy[0];
    private FireRescueMission[] missions = new FireRescueMission[0];
    private bool discoverGround;
    private readonly Dictionary<Vector2Int, Density> density = new Dictionary<Vector2Int, Density>();
    private struct Density { public int count; public Vector2 sum; }
    public Texture2D MapTexture => texture;
    public Vector2 MapCenter { get; private set; }

    private void Awake()
    {
        resolution = Mathf.Clamp(resolution, 64, 256);
        texture = new Texture2D(resolution, resolution, TextureFormat.RGBA32, false)
        { name = "Pixel tactical map", filterMode = FilterMode.Point, wrapMode = TextureWrapMode.Clamp };
        pixels = new Color32[resolution * resolution];
        discoverGround = groundTilemaps == null || groundTilemaps.Length == 0;
        BuildUI();
        RefreshSources();
    }

    private void BuildUI()
    {
        var root = new GameObject("Pixel minimap HUD", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        root.transform.SetParent(transform, false);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.pixelPerfect = true;
        canvas.sortingOrder = 90;
        var group = root.GetComponent<CanvasGroup>();
        group.interactable = group.blocksRaycasts = false;
        panel = MakeRect("Tactical map", root.transform);
        panel.anchorMin = panel.anchorMax = panel.pivot = new Vector2(1, 0);
        panel.anchoredPosition = new Vector2(-18, 18);
        var background = panel.gameObject.AddComponent<Image>();
        background.color = Outline;
        var map = MakeRect("Map pixels", panel).gameObject.AddComponent<RawImage>();
        map.texture = texture;
        map.rectTransform.anchorMin = new Vector2(0, 0);
        map.rectTransform.anchorMax = new Vector2(1, 1);
        map.rectTransform.offsetMin = new Vector2(6, 6);
        map.rectTransform.offsetMax = new Vector2(-6, -6);
        missionHud = new MinimapMissionHud(root.transform);
        foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
        UpdatePanelSize();
    }

    private static RectTransform MakeRect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        return rect;
    }

    private void UpdatePanelSize()
    {
        float limit = Mathf.Max(96, Mathf.Min(Screen.width - 36, Screen.height - 156));
        float size = Mathf.Clamp(minimapSizePixels, 96, limit);
        panel.sizeDelta = new Vector2(size, size);
    }

    private void LateUpdate()
    {
        missionHud?.TickScroll();
        if (Time.unscaledTime < nextRefresh) return;
        nextRefresh = Time.unscaledTime + Mathf.Max(0.05f, refreshInterval);
        if (Time.unscaledTime >= nextDiscovery) RefreshSources();
        RenderMap();
    }

    /// <summary>Includes new spawns, pooled enemies, rebuilt levels, and changed obstacles.</summary>
    public void RefreshSources()
    {
        nextDiscovery = Time.unscaledTime + 0.5f;
        var walls = new List<Collider2D>();
        foreach (var collider in FindObjectsByType<Collider2D>(FindObjectsSortMode.None))
            if (collider.gameObject.scene == gameObject.scene && IsPermanentObstacle(collider, obstacleLayers)) walls.Add(collider);
        obstacles = walls.ToArray();
        fires = FindObjectsByType<FireZone>(FindObjectsSortMode.None);
        enemies = FindObjectsByType<HordeEnemy>(FindObjectsSortMode.None);
        missions = FindObjectsByType<FireRescueMission>(FindObjectsSortMode.None);
        if (discoverGround)
        {
            var ground = new List<Tilemap>();
            foreach (var tilemap in FindObjectsByType<Tilemap>(FindObjectsSortMode.None))
                if (tilemap.gameObject.scene == gameObject.scene && (obstacleLayers.value & (1 << tilemap.gameObject.layer)) == 0
                    && tilemap.GetComponentInParent<Debris>() == null) ground.Add(tilemap);
            groundTilemaps = ground.ToArray();
        }
    }

    public static bool IsPermanentObstacle(Collider2D collider, LayerMask layers) =>
        collider != null && collider.enabled && !collider.isTrigger && collider.gameObject.activeInHierarchy
        && (layers.value & (1 << collider.gameObject.layer)) != 0 && collider.GetComponentInParent<Debris>() == null;

    public void RenderMap()
    {
        view = Camera.main;
        canvas.gameObject.SetActive(view != null);
        if (view == null) return;
        UpdatePanelSize();
        FireRescueMission activeMission = null;
        foreach (var mission in missions)
            if (mission != null && mission.isActiveAndEnabled && mission.gameObject.scene == gameObject.scene)
            { activeMission = mission; break; }
        missionHud.Refresh(activeMission, panel.sizeDelta.x, objectiveRevealDistance, countdownSeconds);
        Ray ray = view.ViewportPointToRay(new Vector3(0.5f, 0.5f));
        var floor = new Plane(Vector3.forward, Vector3.zero);
        MapCenter = floor.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : (Vector2)view.transform.position;
        float units = Mathf.Max(10, worldSpan) / resolution;
        // Snap the map center to one texel to avoid shimmering of fixed building edges.
        MapCenter = new Vector2(Mathf.Round(MapCenter.x / units), Mathf.Round(MapCenter.y / units)) * units;
        for (int y = 0; y < resolution; y++)
            for (int x = 0; x < resolution; x++)
            {
                Vector2 world = MapCenter + new Vector2(x + 0.5f - resolution / 2f, y + 0.5f - resolution / 2f) * units;
                bool ground = groundTilemaps == null || groundTilemaps.Length == 0;
                if (!ground)
                    foreach (var tilemap in groundTilemaps)
                        if (tilemap != null && tilemap.isActiveAndEnabled && tilemap.HasTile(tilemap.WorldToCell(world))) { ground = true; break; }
                pixels[y * resolution + x] = ground ? Ground : Void;
            }
        foreach (var wall in obstacles)
        {
            if (!IsPermanentObstacle(wall, obstacleLayers)) continue;
            Bounds bounds = wall.bounds;
            Vector2Int min = Pixel(bounds.min), max = Pixel(bounds.max);
            for (int y = Mathf.Max(0, min.y); y <= Mathf.Min(resolution - 1, max.y); y++)
                for (int x = Mathf.Max(0, min.x); x <= Mathf.Min(resolution - 1, max.x); x++)
                {
                    Vector2 world = MapCenter + new Vector2(x + 0.5f - resolution / 2f, y + 0.5f - resolution / 2f) * units;
                    if (wall.OverlapPoint(world)) Put(x, y, Building);
                }
        }
        DrawDensity();
        foreach (var fire in fires)
            if (fire != null && fire.gameObject.scene == gameObject.scene && fire.isActiveAndEnabled && fire.IsBurning)
                Icon(fire.transform.position, 'F', MinimapSymbols.Fire, false);
        foreach (var mission in missions)
        {
            if (mission == null || !mission.isActiveAndEnabled || mission.gameObject.scene != gameObject.scene) continue;
            if (mission.State == FireRescueMission.Phase.Objective)
                foreach (var site in mission.Objectives)
                    if (site != null && mission.GetCurrentStep(site) != null)
                    {
                        // Mission crates are icons, never terrain footprints; ordinary debris stays excluded.
                        if (mission.GetCurrentStep(site) is DebrisObjectiveStep debrisStep)
                        {
                            objectiveItems.Clear(); debrisStep.CollectChecklist(objectiveItems);
                            foreach (var item in objectiveItems)
                                if (!item.Complete) Icon(item.Position, item.Symbol, item.Color, false);
                        }
                        Icon(site.transform.position, 'O', MinimapSymbols.Objective, true);
                    }
            if (mission.ShowEvacuationGuidance && mission.Spawn != null)
                Icon(mission.Spawn.transform.position, 'E', MinimapSymbols.Evac, true);
        }
        foreach (var player in PlayerHealth.ActivePlayers)
        {
            if (player == null || !player.isActiveAndEnabled || player.gameObject.scene != gameObject.scene) continue;
            var input = player.GetComponent<PlayerInput>();
            int index = input != null ? input.playerIndex : 0;
            var rules = HoseWaterRules.ForPlayer(player.gameObject);
            Color color = rules != null ? rules.GetHoseColor(index) : PlayerHoseConnection.ColorForPlayer(index);
            Icon(player.transform.position, player.IsDowned ? 'X' : 'A', color, true);
        }
        texture.SetPixels32(pixels);
        texture.Apply(false, false);
    }

    private Vector2Int Pixel(Vector2 world)
    {
        Vector2 map = (world - MapCenter) / Mathf.Max(10, worldSpan) * resolution + Vector2.one * (resolution / 2f);
        return new Vector2Int(Mathf.FloorToInt(map.x), Mathf.FloorToInt(map.y));
    }

    public Vector2 DensityAppearance(int count)
    {
        float t = Mathf.Clamp01((count - 1f) / Mathf.Max(1, denseEnemyCount - 1));
        return new Vector2(Mathf.Lerp(1, 5, t), Mathf.Lerp(pulseFrequency.x, pulseFrequency.y, t));
    }

    private void DrawDensity()
    {
        density.Clear();
        foreach (var enemy in enemies)
        {
            if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0 || enemy.gameObject.scene != gameObject.scene) continue;
            Vector2 position = enemy.transform.position;
            float cell = Mathf.Max(1, densityCellSize);
            var key = new Vector2Int(Mathf.FloorToInt(position.x / cell), Mathf.FloorToInt(position.y / cell));
            density.TryGetValue(key, out Density value);
            value.count++; value.sum += position;
            density[key] = value;
        }
        foreach (var cell in density.Values)
        {
            Vector2 appearance = DensityAppearance(cell.count);
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * appearance.y * Mathf.PI * 2);
            Color32 color = Color.Lerp(new Color(0.35f, 0.04f, 0.08f), new Color(1, 0.15f, 0.22f), pulse);
            Vector2Int p = Pixel(cell.sum / cell.count);
            int radius = Mathf.RoundToInt(appearance.x);
            for (int y = -radius; y <= radius; y++)
                for (int x = -radius; x <= radius; x++)
                    if (x * x + y * y <= radius * radius) Put(p.x + x, p.y + y, color);
        }
    }

    private void Icon(Vector2 world, char symbol, Color32 color, bool edge)
    {
        Vector2Int p = Pixel(world);
        bool outside = p.x < 4 || p.y < 4 || p.x >= resolution - 4 || p.y >= resolution - 4;
        if (outside && !edge) return;
        if (outside)
        {
            Vector2 direction = (world - MapCenter).normalized;
            float scale = (resolution / 2f - 5) / Mathf.Max(Mathf.Abs(direction.x), Mathf.Abs(direction.y), 0.001f);
            p = Vector2Int.RoundToInt(Vector2.one * (resolution / 2f) + direction * scale);
            // A small outward stem distinguishes off-map markers from local ones.
            for (int i = 3; i <= 4; i++) Put(p.x + Mathf.RoundToInt(direction.x * i), p.y + Mathf.RoundToInt(direction.y * i), color);
        }
        for (int y = -3; y <= 3; y++)
            for (int x = -3; x <= 3; x++) Put(p.x + x, p.y + y, Outline);
        for (int y = -2; y <= 2; y++)
            for (int x = -2; x <= 2; x++)
            {
                if (MinimapSymbols.Contains(symbol, x, y)) Put(p.x + x, p.y + y, color);
            }
    }

    private void Put(int x, int y, Color32 color)
    { if (x >= 0 && y >= 0 && x < resolution && y < resolution) pixels[y * resolution + x] = color; }
    private void OnEnable() { nextRefresh = nextDiscovery = 0; if (canvas != null) canvas.gameObject.SetActive(true); }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    private void OnDestroy()
    {
        missionHud?.Dispose();
        if (texture != null) Destroy(texture);
        if (canvas != null) Destroy(canvas.gameObject);
    }
}
