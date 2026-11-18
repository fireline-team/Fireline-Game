using System.Collections.Generic;
using UnityEngine;

namespace Fireline.Missions
{
    [DefaultExecutionOrder(11000)]
    [RequireComponent(typeof(FireRescueMission))]
    public sealed class MissionGuidance : MonoBehaviour
    {
        [Header("Objective and evacuation arrows")]
        [SerializeField] private Vector2 nearArrowSize = new Vector2(96, 80);
        [SerializeField] private Vector2 farArrowSize = new Vector2(144, 120);
        [Tooltip("Ground distance from the nearest standing player where arrow growth starts/ends.")]
        [SerializeField] private Vector2 arrowDistanceRange = new Vector2(10, 60);
        [SerializeField, Min(12)] private int objectiveLabelFontSize = 18;

        private FireRescueMission mission;
        private GUIStyle panel, marker;
        private float markerTop = 140f;
        private LineRenderer evacuationRing;
        private Material ringMaterial;
        private Canvas arrowCanvas;
        private readonly List<PlayerRescueArrow> objectiveArrows = new List<PlayerRescueArrow>();
        private PlayerRescueArrow evacuationArrow;
        private readonly List<CivilianEscort> civilians = new List<CivilianEscort>();
        private readonly List<PlayerRescueArrow> civilianArrows = new List<PlayerRescueArrow>();
        private readonly List<ObjectiveMarker> targetMarkers = new List<ObjectiveMarker>();
        private void LateUpdate()
        {
            var camera = Camera.main;
            civilians.Clear();
            foreach (var site in mission.Objectives)
                foreach (var civilian in site.Civilians)
                    if (civilian != null && civilian.isActiveAndEnabled && !civilian.IsResolved) civilians.Add(civilian);
            while (civilianArrows.Count < civilians.Count)
                civilianArrows.Add(CreateArrow("Civilian direction", new Color(0.7f, 0.9f, 1)));
            for (int i = 0; i < civilianArrows.Count; i++)
                civilianArrows[i].PointTo(camera, i < civilians.Count ? civilians[i].transform.position : Vector3.zero,
                    i < civilians.Count);
            while (objectiveArrows.Count < mission.Objectives.Count)
                objectiveArrows.Add(CreateArrow($"Objective {objectiveArrows.Count + 1} direction", Color.yellow));
            for (int i = 0; i < objectiveArrows.Count; i++)
            {
                var site = i < mission.Objectives.Count ? mission.Objectives[i] : null;
                UpdateArrowSize(objectiveArrows[i], camera, site != null ? site.transform.position : Vector3.zero);
                objectiveArrows[i].PointTo(camera, site != null ? site.transform.position : Vector3.zero,
                    mission.State == FireRescueMission.Phase.Objective && site != null && mission.GetCurrentStep(site) != null);
            }
            UpdateArrowSize(evacuationArrow, camera, mission.Spawn != null ? mission.Spawn.transform.position : Vector3.zero);
            evacuationArrow.PointTo(camera, mission.Spawn != null ? mission.Spawn.transform.position : Vector3.zero,
                mission.Spawn != null && mission.ShowEvacuationGuidance);
            if (mission == null || mission.Spawn == null) return;
            if (evacuationRing == null)
            {
                var go = new GameObject("Active evacuation boundary");
                go.transform.SetParent(transform, false);
                evacuationRing = go.AddComponent<LineRenderer>();
                ringMaterial = new Material(Shader.Find("Sprites/Default"));
                evacuationRing.sharedMaterial = ringMaterial;
                evacuationRing.loop = true;
                evacuationRing.useWorldSpace = true;
                evacuationRing.widthMultiplier = 0.12f;
                evacuationRing.positionCount = 64;
                evacuationRing.startColor = evacuationRing.endColor = Color.cyan;
                evacuationRing.sortingOrder = 20;
            }
            evacuationRing.enabled = mission.State == FireRescueMission.Phase.Evacuate;
            for (int i = 0; i < 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                evacuationRing.SetPosition(i, mission.Spawn.transform.position +
                    new Vector3(Mathf.Cos(angle) * mission.EvacuationRadius, Mathf.Sin(angle) * mission.EvacuationRadius, -0.08f));
            }
        }
        private void UpdateArrowSize(PlayerRescueArrow arrow, Camera camera, Vector3 destination)
        {
            float distance = float.PositiveInfinity;
            foreach (var player in PlayerHealth.ActivePlayers)
                if (player != null && player.isActiveAndEnabled && !player.IsDowned && player.gameObject.scene == gameObject.scene)
                    distance = Mathf.Min(distance, Vector2.Distance(player.transform.position, destination));
            if (float.IsPositiveInfinity(distance))
            {
                Vector3 origin = camera != null ? camera.transform.position : destination;
                if (camera != null)
                {
                    var ground = new Plane(Vector3.forward, destination);
                    Ray ray = camera.ViewportPointToRay(new Vector3(0.5f, 0.5f));
                    if (ground.Raycast(ray, out float depth)) origin = ray.GetPoint(depth);
                }
                distance = Vector2.Distance(origin, destination);
            }
            float t = Mathf.InverseLerp(arrowDistanceRange.x, Mathf.Max(arrowDistanceRange.x + 0.01f, arrowDistanceRange.y), distance);
            arrow.rectTransform.sizeDelta = Vector2.Lerp(nearArrowSize, farArrowSize, Mathf.SmoothStep(0, 1, t));
        }

        private void OnDestroy() { if (ringMaterial != null) Destroy(ringMaterial); }
        private void Awake()
        {
            mission = GetComponent<FireRescueMission>();
            var root = new GameObject("Mission direction arrows", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
            root.transform.SetParent(transform, false);
            arrowCanvas = root.GetComponent<Canvas>();
            arrowCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            arrowCanvas.sortingOrder = 99; // Teammate rescue indicators remain above mission guidance.
            var group = root.GetComponent<CanvasGroup>();
            group.interactable = group.blocksRaycasts = false;
            evacuationArrow = CreateArrow("Evacuation direction", Color.cyan);
        }
        private PlayerRescueArrow CreateArrow(string name, Color color)
        {
            var arrow = new GameObject(name, typeof(RectTransform)).AddComponent<PlayerRescueArrow>();
            arrow.transform.SetParent(arrowCanvas.transform, false);
            arrow.rectTransform.anchorMin = arrow.rectTransform.anchorMax = Vector2.zero;
            arrow.rectTransform.sizeDelta = new Vector2(68, 56);
            arrow.raycastTarget = false;
            arrow.color = color;
            arrow.gameObject.SetActive(false);
            return arrow;
        }
        private void OnEnable() { if (arrowCanvas != null) arrowCanvas.gameObject.SetActive(true); }
        private void OnDisable() { if (arrowCanvas != null) arrowCanvas.gameObject.SetActive(false); }
        private void OnGUI()
        {
            if (mission == null) return;
            panel ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            marker ??= new GUIStyle(GUI.skin.box) { alignment = TextAnchor.MiddleCenter, wordWrap = true };
            marker.fontSize = objectiveLabelFontSize;
            float width = Mathf.Min(600, Screen.width - 24);
            string instruction = mission.Instruction + "\n" + mission.CivilianSummary;
            float panelHeight = Mathf.Max(92, panel.CalcHeight(new GUIContent(instruction), width));
            GUI.Box(new Rect((Screen.width - width) / 2, 12, width, panelHeight), instruction, panel);
            markerTop = 12 + panelHeight + 32;
            string alert = string.Join("\n", new[] { mission.RescueNotice, mission.RescueAlert }).Trim();
            if (!string.IsNullOrEmpty(alert))
            {
                Color previous = GUI.color;
                GUI.color = new Color(1, 0.45f, 0.25f);
                float alertHeight = panel.CalcHeight(new GUIContent(alert), width) + 12;
                GUI.Box(new Rect((Screen.width - width) / 2, 18 + panelHeight, width, alertHeight), alert, panel);
                markerTop = 18 + panelHeight + alertHeight + 28;
                GUI.color = previous;
            }
            var camera = Camera.main;
            if (camera == null) return;
            for (int i = 0; i < mission.Objectives.Count && mission.State == FireRescueMission.Phase.Objective; i++)
            {
                var site = mission.Objectives[i];
                var step = mission.GetCurrentStep(site);
                if (step == null) continue;
                DrawMarker(camera, site.transform.position, $"OBJECTIVE {i + 1}\n{site.CiviliansWaiting} trapped / {site.CiviliansLost} lost", site.CiviliansInDanger ? new Color(1, 0.45f, 0.25f) : Color.yellow,
                    i < objectiveArrows.Count ? objectiveArrows[i] : null);
                if (step is IObjectiveMarkerSource source)
                {
                    targetMarkers.Clear();
                    source.CollectMarkers(targetMarkers);
                    foreach (var target in targetMarkers)
                        DrawMarker(camera, target.Position, target.Label, target.Color);
                }
            }
            for (int i = 0; i < civilians.Count; i++)
                if (civilians[i] != null && !civilians[i].IsResolved)
                    DrawMarker(camera, civilians[i].transform.position,
                        $"CIVILIAN {Mathf.CeilToInt(civilians[i].CurrentHealth)}/{civilians[i].MaxHealth:0}",
                        new Color(0.7f, 0.9f, 1), civilianArrows[i]);
            if (mission.Spawn != null && mission.ShowEvacuationGuidance)
                DrawMarker(camera, mission.Spawn.transform.position,
                    mission.State == FireRescueMission.Phase.Evacuate ? $"EVAC {mission.EvacuationProgress:P0}" : "EVAC",
                    Color.cyan, evacuationArrow);
        }
        private void DrawMarker(Camera camera, Vector3 world, string text, Color color, PlayerRescueArrow arrow = null)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool outside = screen.z <= 0 || !camera.pixelRect.Contains(new Vector2(screen.x, screen.y));
            if (outside && (arrow == null || !arrow.gameObject.activeSelf)) return;
            float labelWidth = Mathf.Min(220, Mathf.Max(1, Screen.width - 16));
            float labelHeight = Mathf.Max(56, marker.CalcHeight(new GUIContent(text), labelWidth));
            Vector2 position = new Vector2(screen.x, Screen.height - screen.y);
            if (outside)
            {
                // Keep the label inward from the shared arrow rather than covering its tip.
                Vector2 arrowPosition = arrow.rectTransform.anchoredPosition;
                Vector2 inward = (camera.pixelRect.center - arrowPosition).normalized;
                float labelExtent = Mathf.Abs(inward.x) * labelWidth / 2 + Mathf.Abs(inward.y) * labelHeight / 2;
                Vector2 label = arrowPosition + inward * (arrow.rectTransform.rect.width / 2 + labelExtent + 12);
                position = new Vector2(label.x, Screen.height - label.y);
            }
            position.x = Mathf.Clamp(position.x, labelWidth / 2 + 8, Mathf.Max(labelWidth / 2 + 8, Screen.width - labelWidth / 2 - 8));
            position.y = Mathf.Clamp(position.y, Mathf.Min(markerTop, Screen.height - labelHeight / 2 - 8), Mathf.Max(labelHeight / 2 + 8, Screen.height - labelHeight / 2 - 8));
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(position.x - labelWidth / 2, position.y - labelHeight / 2, labelWidth, labelHeight), text, marker);
            GUI.color = previous;
        }
    }
}