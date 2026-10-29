using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fireline.Missions
{
    /// <summary>
    /// Restart controls for the mission scene.
    ///   - Mid-play: hold T for <see cref="holdSeconds"/> to reload. A countdown shows while held.
    ///   - After evacuating: any player presses Interact (E / A) to reload instantly.
    ///   (A full team wipe is already handled by PlayerController's Interact reset.)
    /// </summary>
    [DefaultExecutionOrder(11001)]
    [RequireComponent(typeof(FireRescueMission))]
    public sealed class MissionResetControl : MonoBehaviour
    {
        [SerializeField] private Key holdResetKey = Key.T;
        [SerializeField, Min(0.1f)] private float holdSeconds = 3f;
        [Tooltip("Ignore Interact presses for this long after evac, so a press already in progress doesn't skip the end screen.")]
        [SerializeField, Min(0f)] private float completeInputDelay = 0.3f;

        private FireRescueMission mission;
        private float heldFor;
        private float completedAt = -1f;
        private bool resetting;
        private GUIStyle countdownStyle, promptStyle;

        public float HoldProgress => Mathf.Clamp01(heldFor / holdSeconds);
        public bool IsHolding => heldFor > 0f;
        public float SecondsRemaining => Mathf.Max(0f, holdSeconds - heldFor);
        private bool MissionComplete => mission != null && mission.State == FireRescueMission.Phase.Complete;

        private void Awake() => mission = GetComponent<FireRescueMission>();
        private void OnDisable() => heldFor = 0f;

        private void Update()
        {
            if (resetting) return;

            if (MissionComplete)
            {
                heldFor = 0f;
                if (completedAt < 0f) completedAt = Time.unscaledTime;
                if (Time.unscaledTime - completedAt > completeInputDelay && AnyPlayerPressedInteract())
                    ResetScene();
                return;
            }
            completedAt = -1f;

            var keyboard = Keyboard.current;
            if (keyboard == null || !keyboard[holdResetKey].isPressed) { heldFor = 0f; return; }
            // Unscaled so hit-stop freezes don't stall the countdown.
            heldFor += Time.unscaledDeltaTime;
            if (heldFor >= holdSeconds) ResetScene();
        }

        private bool AnyPlayerPressedInteract()
        {
            foreach (var player in PlayerHealth.ActivePlayers)
            {
                if (player == null || player.gameObject.scene != gameObject.scene) continue;
                var input = player.GetComponent<PlayerInput>();
                var interact = input != null ? input.actions?.FindAction("Interact") : null;
                if (interact != null && interact.WasPressedThisFrame()) return true;
            }
            return false;
        }

        public void ResetScene()
        {
            if (resetting) return;
            resetting = true;
            Scene scene = gameObject.scene;
#if UNITY_EDITOR
            // Prototype scenes may be opened directly, outside the build list.
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.path);
        }

        private void OnGUI()
        {
            countdownStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 28, alignment = TextAnchor.MiddleCenter, fontStyle = FontStyle.Bold };
            promptStyle ??= new GUIStyle(GUI.skin.box) { fontSize = 20, alignment = TextAnchor.MiddleCenter };

            if (MissionComplete)
            {
                float width = Mathf.Min(460, Screen.width - 24);
                GUI.Box(new Rect((Screen.width - width) / 2, Screen.height * 0.5f - 25, width, 50),
                    "Mission complete! Press E / A to restart", promptStyle);
                return;
            }

            if (!IsHolding) return;
            float w = Mathf.Min(360, Screen.width - 24);
            var rect = new Rect((Screen.width - w) / 2, 88, w, 56);
            GUI.Box(rect, $"Resetting in {Mathf.CeilToInt(SecondsRemaining)}...", countdownStyle);
            // Thin progress bar under the text so the tick-down reads at a glance.
            Color previous = GUI.color;
            GUI.color = new Color(1f, 0.55f, 0.15f);
            GUI.DrawTexture(new Rect(rect.x, rect.yMax + 2, rect.width * HoldProgress, 6), Texture2D.whiteTexture);
            GUI.color = previous;
        }
    }
}
