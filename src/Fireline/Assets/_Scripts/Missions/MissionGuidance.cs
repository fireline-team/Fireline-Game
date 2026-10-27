using System.Collections.Generic;
using UnityEngine;

namespace Fireline.Missions
{
    [DefaultExecutionOrder(11000)]
    [RequireComponent(typeof(FireRescueMission))]
    public sealed class MissionGuidance : MonoBehaviour
    {
        private FireRescueMission mission;
        private GUIStyle panel, marker;
        private LineRenderer evacuationRing;
        private Material ringMaterial;
        private Canvas arrowCanvas;
        private readonly List<PlayerRescueArrow> objectiveArrows = new List<PlayerRescueArrow>();
        private PlayerRescueArrow evacuationArrow;
        private void LateUpdate()
        {
            var camera = Camera.main;
            while (objectiveArrows.Count < mission.Objectives.Count)
                objectiveArrows.Add(CreateArrow($"Objective {objectiveArrows.Count + 1} direction", Color.yellow));
            for (int i = 0; i < objectiveArrows.Count; i++)
            {
                var site = i < mission.Objectives.Count ? mission.Objectives[i] : null;
                objectiveArrows[i].PointTo(camera, site != null ? site.transform.position : Vector3.zero,
                    mission.State == FireRescueMission.Phase.Objective && site != null && mission.GetCurrentStep(site) != null);
            }
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
            marker ??= new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            float width = Mathf.Min(600, Screen.width - 24);
            GUI.Box(new Rect((Screen.width - width) / 2, 12, width, 64), mission.Instruction, panel);
            var camera = Camera.main;
            if (camera == null) return;
            for (int i = 0; i < mission.Objectives.Count && mission.State == FireRescueMission.Phase.Objective; i++)
            {
                var site = mission.Objectives[i];
                var step = mission.GetCurrentStep(site);
                if (step == null) continue;
                DrawMarker(camera, site.transform.position, $"OBJECTIVE {i + 1}", Color.yellow,
                    i < objectiveArrows.Count ? objectiveArrows[i] : null);
                if (step is FireObjectiveStep fireStep)
                {
                    int number = 0;
                    foreach (var fire in fireStep.Fires)
                    {
                        number++;
                        if (fire != null && fire.IsBurning)
                            DrawMarker(camera, fire.transform.position, $"FIRE {number}", new Color(1, 0.65f, 0.15f));
                    }
                }
            }
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
            Vector2 position = new Vector2(screen.x, Screen.height - screen.y);
            if (outside)
            {
                // Keep the label inward from the shared arrow rather than covering its tip.
                Vector2 arrowPosition = arrow.rectTransform.anchoredPosition;
                Vector2 inward = (camera.pixelRect.center - arrowPosition).normalized;
                Vector2 label = arrowPosition + inward * 100;
                position = new Vector2(label.x, Screen.height - label.y);
            }
            position.x = Mathf.Clamp(position.x, 70, Mathf.Max(70, Screen.width - 70));
            position.y = Mathf.Clamp(position.y, 105, Mathf.Max(105, Screen.height - 25));
            Color previous = GUI.color;
            GUI.color = color;
            GUI.Box(new Rect(position.x - 65, position.y - 15, 130, 30), text, marker);
            GUI.color = previous;
        }
    }
}
