using UnityEngine;

namespace Fireline.Missions
{
    [RequireComponent(typeof(FireRescueMission))]
    public sealed class MissionGuidance : MonoBehaviour
    {
        private FireRescueMission mission;
        private GUIStyle panel, marker;
        private LineRenderer evacuationRing;
        private Material ringMaterial;
        private void LateUpdate()
        {
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
        private void Awake() => mission = GetComponent<FireRescueMission>();
        private void OnGUI()
        {
            if (mission == null) return;
            panel ??= new GUIStyle(GUI.skin.box) { fontSize = 18, alignment = TextAnchor.MiddleCenter, wordWrap = true };
            marker ??= new GUIStyle(GUI.skin.box) { fontSize = 15, alignment = TextAnchor.MiddleCenter };
            float width = Mathf.Min(600, Screen.width - 24);
            GUI.Box(new Rect((Screen.width - width) / 2, 12, width, 64), mission.Instruction, panel);
            var camera = Camera.main;
            if (camera == null) return;
            if (mission.State == FireRescueMission.Phase.Objective && mission.Objective != null)
            {
                DrawMarker(camera, mission.Objective.transform.position, "OBJECTIVE", Color.yellow, true);
                if (mission.CurrentStep is FireObjectiveStep fireStep)
                {
                    int number = 0;
                    foreach (var fire in fireStep.Fires)
                    {
                        number++;
                        if (fire != null && fire.IsBurning)
                            DrawMarker(camera, fire.transform.position, $"FIRE {number}", new Color(1, 0.65f, 0.15f), false);
                    }
                }
            }
            if (mission.Spawn != null && mission.State != FireRescueMission.Phase.InvalidSetup)
                DrawMarker(camera, mission.Spawn.transform.position,
                    mission.State == FireRescueMission.Phase.Evacuate ? $"EVAC {mission.EvacuationProgress:P0}" : "EVAC",
                    Color.cyan, true);
        }
        private void DrawMarker(Camera camera, Vector3 world, string text, Color color, bool offscreen)
        {
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool outside = screen.z <= 0 || screen.x < 70 || screen.x > Screen.width - 70 || screen.y < 30 || screen.y > Screen.height - 100;
            if (outside && !offscreen) return;
            Vector2 position = new Vector2(screen.x, Screen.height - screen.y);
            if (screen.z <= 0) position = new Vector2(Screen.width, Screen.height) - position;
            if (outside)
            {
                Vector2 direction = position - new Vector2(Screen.width, Screen.height) * 0.5f;
                text += Mathf.Abs(direction.x) > Mathf.Abs(direction.y) ? (direction.x > 0 ? " >" : " <") : (direction.y > 0 ? " v" : " ^");
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
