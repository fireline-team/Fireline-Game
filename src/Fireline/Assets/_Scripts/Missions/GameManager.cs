using System.Collections;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;

namespace Fireline.Missions
{
    /// <summary>Owns the session; the arena's FireRescueMission owns objectives and extraction.</summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameManager : MonoBehaviour
    {
        public enum SessionState { MainMenu, MissionBriefing, Loading, Playing, Paused, Results }
        public enum Outcome { Extracted, TeamLost, MissionUnavailable }
        public const string MenuScene = "Assets/_Scenes/MainMenu.unity";
        public const string ArenaScene = "Assets/_Scenes/JakeProtoLevel.unity";
        public static GameManager Instance { get; private set; }
        public static bool BlocksGameplay => Instance != null && Instance.State != SessionState.Playing;
        public SessionState State { get; private set; } = SessionState.MainMenu;
        public Outcome Result { get; private set; }
        public float MissionSeconds { get; private set; }
        public int CompletedObjectives { get; private set; }
        public int TotalObjectives { get; private set; }
        public int CiviliansSaved { get; private set; }
        public int CiviliansLost { get; private set; }
        private FireRescueMission mission;
        private GUIStyle heading, body, button;
        private bool ownsSession;
        private float resultsAt;

        private void Awake()
        {
            if (Instance != null && Instance != this) { Destroy(gameObject); return; }
            Instance = this;
            ownsSession = true;
            DontDestroyOnLoad(gameObject);
            RestoreTime();
        }

        public void OpenMission()
        {
            if (State == SessionState.MainMenu) State = SessionState.MissionBriefing;
        }

        public void CloseMission()
        {
            if (State == SessionState.MissionBriefing) State = SessionState.MainMenu;
        }

        public void StartMission()
        {
            if (State != SessionState.MissionBriefing) return;
            MissionSeconds = 0;
            CompletedObjectives = TotalObjectives = CiviliansSaved = CiviliansLost = 0;
            StartCoroutine(LoadArena());
        }

        private IEnumerator LoadArena()
        {
            State = SessionState.Loading;
            RestoreTime();
            yield return LoadScene(ArenaScene);
            mission = FindAnyObjectByType<FireRescueMission>();
            // Let the scene-local mission and newly spawned players finish Start.
            yield return null;
            if (mission == null || mission.State == FireRescueMission.Phase.InvalidSetup)
            {
                Finish(Outcome.MissionUnavailable);
                yield break;
            }
            State = SessionState.Playing;
        }

        private static AsyncOperation LoadScene(string path)
        {
#if UNITY_EDITOR
            if (SceneUtility.GetBuildIndexByScenePath(path) < 0)
                return UnityEditor.SceneManagement.EditorSceneManager.LoadSceneAsyncInPlayMode(
                    path, new LoadSceneParameters(LoadSceneMode.Single));
#endif
            return SceneManager.LoadSceneAsync(path, LoadSceneMode.Single);
        }

        private void Update()
        {
            bool pausePressed = Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame;
            foreach (var pad in Gamepad.all) pausePressed |= pad.startButton.wasPressedThisFrame;
            if (pausePressed) TogglePause();
            bool confirm = Keyboard.current != null && Keyboard.current.enterKey.wasPressedThisFrame;
            bool back = false;
            foreach (var pad in Gamepad.all)
            {
                confirm |= pad.buttonSouth.wasPressedThisFrame;
                back |= pad.buttonEast.wasPressedThisFrame;
            }
            if (confirm)
            {
                switch (State)
                {
                    case SessionState.MainMenu: OpenMission(); break;
                    case SessionState.MissionBriefing: StartMission(); break;
                    case SessionState.Results: if (Time.unscaledTime - resultsAt > 0.3f) ReturnToMainMenu(); break;
                }
            }
            if (back && State == SessionState.MissionBriefing) CloseMission();
            if (State != SessionState.Playing) return;
            MissionSeconds += Time.deltaTime;
            bool any = false, allDead = true;
            foreach (var player in PlayerHealth.ActivePlayers)
            {
                if (player == null || mission == null || player.gameObject.scene != mission.gameObject.scene) continue;
                any = true;
                if (!player.IsDead) allDead = false;
            }
            // No players yet is a join lobby, not a team wipe. A single downed teammate can be revived.
            if (any && allDead) Finish(Outcome.TeamLost);
            else if (mission != null && mission.State == FireRescueMission.Phase.Complete) Finish(Outcome.Extracted);
        }

        public void TogglePause()
        {
            if (State == SessionState.Playing)
            {
                State = SessionState.Paused;
                Freeze();
            }
            else if (State == SessionState.Paused)
            {
                RestoreTime();
                State = SessionState.Playing;
            }
        }

        private void Finish(Outcome outcome)
        {
            if (State == SessionState.Results) return;
            resultsAt = Time.unscaledTime;
            Result = outcome;
            CompletedObjectives = mission != null ? mission.CompletedObjectives : 0;
            TotalObjectives = mission != null ? mission.Objectives.Count : 0;
            CiviliansSaved = mission != null ? mission.CiviliansSaved : 0;
            CiviliansLost = mission != null ? mission.CiviliansLost : 0;
            State = SessionState.Results;
            Freeze();
        }

        private static void Freeze()
        {
            // Cancel first so an expiring hit-stop cannot unpause the session later.
            HitStop.Cancel();
            Time.timeScale = 0;
        }

        private static void RestoreTime()
        {
            HitStop.Cancel();
            Time.timeScale = 1;
        }

        public void ReturnToMainMenu()
        {
            if (State != SessionState.Results && State != SessionState.Paused) return;
            StartCoroutine(LoadMenu());
        }

        private IEnumerator LoadMenu()
        {
            State = SessionState.Loading;
            RestoreTime();
            mission = null;
            yield return LoadScene(MenuScene);
            State = SessionState.MainMenu;
        }

        private void OnDestroy()
        {
            if (!ownsSession) return;
            Instance = null;
            RestoreTime();
        }

        private void OnGUI()
        {
            if (!ownsSession) return;
            GUI.depth = -1000;
            heading ??= new GUIStyle(GUI.skin.label) { fontSize = 30, fontStyle = FontStyle.Bold, wordWrap = true };
            body ??= new GUIStyle(GUI.skin.label) { fontSize = 19, wordWrap = true };
            button ??= new GUIStyle(GUI.skin.button) { fontSize = 20, fixedHeight = 48 };
            if (State == SessionState.Playing)
            {
                if (GUI.Button(new Rect(Screen.width - 180, 16, 164, 44), "Pause (Esc)", button)) TogglePause();
                return;
            }
            Color color = GUI.color;
            GUI.color = new Color(0.035f, 0.055f, 0.075f, 0.98f);
            GUI.DrawTexture(new Rect(0, 0, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
            float width = Mathf.Min(560, Screen.width - 32);
            GUILayout.BeginArea(new Rect((Screen.width - width) / 2, Mathf.Max(16, (Screen.height - 430) / 2), width, 430));
            switch (State)
            {
                case SessionState.MainMenu:
                    GUILayout.Label("FIRELINE", heading);
                    GUILayout.Label("Hello World!", body);
                    GUILayout.Space(28);
                    if (GUILayout.Button("Open mission (Enter / A)", button)) OpenMission();
                    break;
                case SessionState.MissionBriefing:
                    GUILayout.Label("Fire rescue", heading);
                    GUILayout.Label("Deploy to the arena. Extinguish the marked fires, rescue civilians, then return together to EVAC. Hold the extraction zone to finish.\n\nA full team wipe ends the mission. Downed teammates can be revived with water.", body);
                    GUILayout.Space(20);
                    if (GUILayout.Button("Start mission (Enter / A)", button)) StartMission();
                    if (GUILayout.Button("Back", button)) CloseMission();
                    break;
                case SessionState.Loading:
                    GUILayout.Label("Loading…", heading);
                    break;
                case SessionState.Paused:
                    GUILayout.Label("Paused", heading);
                    GUILayout.Label("Esc or controller Start to resume.", body);
                    GUILayout.Space(20);
                    if (GUILayout.Button("Resume", button)) TogglePause();
                    if (GUILayout.Button("Exit to main menu", button)) ReturnToMainMenu();
                    break;
                case SessionState.Results:
                    GUILayout.Label(Result == Outcome.Extracted ? "Team extracted" : Result == Outcome.TeamLost ? "Mission failed" : "Mission unavailable", heading);
                    GUILayout.Label(Result == Outcome.TeamLost ? "All firefighters are down." : Result == Outcome.Extracted ? "Your team made it back to EVAC." : "The arena could not prepare a mission.", body);
                    GUILayout.Space(20);
                    GUILayout.Label($"Objectives: {CompletedObjectives}/{TotalObjectives}\nCivilians saved: {CiviliansSaved}  •  Lost: {CiviliansLost}\nMission time: {Mathf.FloorToInt(MissionSeconds / 60):00}:{Mathf.FloorToInt(MissionSeconds % 60):00}", body);
                    GUILayout.Space(20);
                    if (GUILayout.Button("Exit to main menu", button)) ReturnToMainMenu();
                    break;
            }
            GUILayout.EndArea();
        }
    }
}
