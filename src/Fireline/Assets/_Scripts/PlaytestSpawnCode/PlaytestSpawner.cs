using System;
using System.Collections.Generic;
using Fireline.Missions;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;
using UnityEngine.SceneManagement;

namespace Game.Runtime
{
    /// <summary>
    /// PLAYTEST spawner for a full level, loosely modeled on how Helldivers spawns.
    /// Enemies always come from EnemySpawnPoints placed in the level, and only from points
    /// that are far enough from players and off camera, so nothing pops in on screen.
    ///
    /// Three sources of enemies:
    ///   AMBIENT    - small groups trickle in from points in a ring around the team. Grows
    ///                with intensity, which ramps over Ramp Duration.
    ///   OBJECTIVE  - while players are at an active objective, bigger waves come from the
    ///                points around that objective. Standing still to fight fires gets loud.
    ///   EVAC       - once the mission says "return to evac", everything speeds up.
    ///
    /// Enemies that end up far from every player (lost, left behind) are quietly recycled,
    /// so the alive cap gets spent where the players are.
    ///
    ///   START -> opens player joining and starts the director.
    ///   RESET -> reloads the scene (same as PlayerHealth.ResetScene).
    ///
    /// Needs a HordeManager in the scene. Uses the FireRescueMission and HordeNavGrid if present.
    /// </summary>
    public class PlaytestSpawner : MonoBehaviour
    {
        [Serializable]
        private class EnemyEntry
        {
            [Tooltip("The enemy config to spawn (an asset from Assets/_Configs).")]
            public EnemyDefinition type;
            [Tooltip("Share of spawns this type gets. Treated as ratios, so 7 / 2 / 1 works the same as 70 / 20 / 10.")]
            [Min(0f)] public float spawnPercent = 100f;
            [Tooltip("Seconds after Start before this type can show up. While locked, its share goes to the unlocked types.")]
            [Min(0f)] public float unlockTime;

            [NonSerialized] public int spawned;
        }

        [Header("Enemies")]
        [Tooltip("The shared enemy prefab (Assets/_Prefabs/HordeEnemy). Each spawn gets one of the types below applied.")]
        [SerializeField] private HordeEnemy enemyPrefab;
        [SerializeField] private EnemyEntry[] enemyTypes = new EnemyEntry[0];
        [Tooltip("Hard cap on living enemies across the whole level.")]
        [SerializeField, Min(1)] private int maxAlive = 300;

        [Header("Intensity Ramp")]
        [Tooltip("Seconds to go from start values to end values.")]
        [SerializeField, Min(1f)] private float rampDuration = 300f;
        [Tooltip("Shape of the ramp. X = progress (0-1), Y = intensity (0-1).")]
        [SerializeField] private AnimationCurve rampCurve = AnimationCurve.Linear(0f, 0f, 1f, 1f);

        [Header("Ambient Groups")]
        [Tooltip("Seconds between ambient groups at the start / at full intensity.")]
        [SerializeField, Min(0.1f)] private float ambientIntervalStart = 6f;
        [SerializeField, Min(0.1f)] private float ambientIntervalEnd = 2f;
        [Tooltip("Enemies per ambient group at the start / at full intensity.")]
        [SerializeField, Min(1)] private int ambientGroupStart = 3;
        [SerializeField, Min(1)] private int ambientGroupEnd = 10;
        [Tooltip("Stop ambient spawning while this many enemies are already near the team (within Spawn Distance Max). Keeps fights from snowballing.")]
        [SerializeField, Min(1)] private int ambientNearbyLimit = 60;

        [Header("Objective Waves")]
        [Tooltip("A player within this distance of an active objective counts as 'working on it'.")]
        [SerializeField, Min(0f)] private float objectiveEngageRadius = 10f;
        [Tooltip("Only spawn points within this distance of the objective are used for its waves.")]
        [SerializeField, Min(0f)] private float objectiveSpawnRadius = 30f;
        [Tooltip("Seconds between objective waves at the start / at full intensity.")]
        [SerializeField, Min(0.1f)] private float objectiveIntervalStart = 8f;
        [SerializeField, Min(0.1f)] private float objectiveIntervalEnd = 4f;
        [Tooltip("Enemies per objective wave at the start / at full intensity.")]
        [SerializeField, Min(1)] private int objectiveWaveStart = 6;
        [SerializeField, Min(1)] private int objectiveWaveEnd = 16;
        [Tooltip("How many spawn points a single objective wave is split across, so it comes from more than one direction.")]
        [SerializeField, Min(1)] private int objectiveWaveDirections = 2;

        [Header("Evac")]
        [Tooltip("During the evac phase, ambient groups come this many times as often.")]
        [SerializeField, Min(1f)] private float evacRateMultiplier = 2f;
        [Tooltip("During the evac phase, ambient groups are this many times bigger.")]
        [SerializeField, Min(1f)] private float evacSizeMultiplier = 1.5f;

        [Header("Spawn Point Rules")]
        [Tooltip("Points closer than this to ANY player are never used.")]
        [SerializeField, Min(0f)] private float spawnDistanceMin = 14f;
        [Tooltip("Ambient groups only use points within this distance of some player.")]
        [SerializeField, Min(0f)] private float spawnDistanceMax = 30f;
        [Tooltip("Points must be outside the camera view, plus this margin (fraction of the screen).")]
        [SerializeField, Range(0f, 0.5f)] private float offscreenMargin = 0.1f;

        [Header("Cleanup")]
        [Tooltip("Enemies farther than this from every player (and off camera) start a recycle timer.")]
        [SerializeField, Min(0f)] private float recycleDistance = 45f;
        [Tooltip("Seconds an enemy must stay far away before it's recycled.")]
        [SerializeField, Min(0f)] private float recycleDelay = 6f;

        [Header("Players")]
        [Tooltip("Block player joining until Start is pressed.")]
        [SerializeField] private bool lockJoiningUntilStart = true;

        [Header("Debug UI")]
        [SerializeField] private bool showPanel = true;
        [Tooltip("1 = normal size on a 1080p screen. Scales with the window automatically.")]
        [SerializeField, Min(0.5f)] private float uiScale = 1.5f;

        private ObjectPool<HordeEnemy> _pool;
        private readonly List<HordeEnemy> _active = new List<HordeEnemy>();
        private readonly Dictionary<HordeEnemy, float> _farSince = new Dictionary<HordeEnemy, float>();
        private readonly List<Vector2> _players = new List<Vector2>(4);
        private readonly List<EnemySpawnPoint> _candidates = new List<EnemySpawnPoint>();
        private readonly List<HordeEnemy> _recycleBuffer = new List<HordeEnemy>();

        private FireRescueMission _mission;
        private Camera _camera;
        private bool _running;
        private bool _resetting;
        private float _elapsed;
        private float _ambientTimer;
        private float _objectiveTimer;
        private float _cleanupTimer;
        private int _totalSpawned;
        private int _recycled;
        private int _nearbyCount;
        private int _validAmbientPoints;
        private string _objectiveStatus = "-";

        public bool IsRunning => _running;
        public int AliveCount => _active.Count;
        public float Intensity => Mathf.Clamp01(rampCurve.Evaluate(Mathf.Clamp01(_elapsed / rampDuration)));
        private bool IsEvac => _mission != null && _mission.State == FireRescueMission.Phase.Evacuate;

        // ------------------------------------------------------------------ setup

        private void Awake()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("PlaytestSpawner needs an enemy prefab.", this);
                enabled = false;
                return;
            }

            _pool = new ObjectPool<HordeEnemy>(
                createFunc: () =>
                {
                    HordeEnemy e = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
                    e.Pool = _pool; // so HordeEnemy.Despawn() (death) returns it here
                    return e;
                },
                actionOnGet: e => { e.gameObject.SetActive(true); _active.Add(e); },
                actionOnRelease: e => { e.gameObject.SetActive(false); _active.Remove(e); _farSince.Remove(e); },
                actionOnDestroy: e => { if (e != null) Destroy(e.gameObject); },
                collectionCheck: false,
                defaultCapacity: Mathf.Min(maxAlive, 128),
                maxSize: maxAlive);
        }

        private void Start()
        {
            _mission = FindAnyObjectByType<FireRescueMission>();
            if (lockJoiningUntilStart && PlayerInputManager.instance != null)
                PlayerInputManager.instance.DisableJoining();
            if (EnemySpawnPoint.All.Count == 0)
                Debug.LogWarning("PlaytestSpawner: no EnemySpawnPoints in the scene, so nothing will spawn.", this);
        }

        private void OnDestroy() => _pool?.Dispose();

        // ------------------------------------------------------------------ buttons

        public void Begin()
        {
            if (_running || _pool == null) return;
            _running = true;
            _elapsed = 0f;
            _ambientTimer = 2f; // short breather before the first group
            _objectiveTimer = 0f;
            if (PlayerInputManager.instance != null)
                PlayerInputManager.instance.EnableJoining();
        }

        public void ResetScene()
        {
            if (_resetting) return;
            _resetting = true;
            Time.timeScale = 1f;
            Scene scene = gameObject.scene;
#if UNITY_EDITOR
            if (scene.buildIndex < 0)
            {
                UnityEditor.SceneManagement.EditorSceneManager.LoadSceneInPlayMode(
                    scene.path, new LoadSceneParameters(LoadSceneMode.Single));
                return;
            }
#endif
            SceneManager.LoadScene(scene.path);
        }

        // ------------------------------------------------------------------ director loop

        private void Update()
        {
            if (!_running) return;

            _camera = Camera.main;
            GatherPlayers();
            if (_players.Count == 0) return; // nobody to fight yet; hold the timer too

            if (_mission != null && _mission.State == FireRescueMission.Phase.Complete) return;

            float dt = Time.deltaTime;
            _elapsed += dt;
            CountNearby();
            RefreshPointDebug();

            // Ambient trickle.
            float rate = IsEvac ? evacRateMultiplier : 1f;
            _ambientTimer -= dt * rate;
            if (_ambientTimer <= 0f)
            {
                _ambientTimer = Mathf.Lerp(ambientIntervalStart, ambientIntervalEnd, Intensity);
                if (_nearbyCount < ambientNearbyLimit)
                {
                    float size = Mathf.Lerp(ambientGroupStart, ambientGroupEnd, Intensity) * (IsEvac ? evacSizeMultiplier : 1f);
                    SpawnAmbientGroup(Mathf.RoundToInt(size));
                }
            }

            // Objective pressure.
            UpdateObjectiveWaves(dt);

            // Recycle stragglers about once a second.
            _cleanupTimer -= dt;
            if (_cleanupTimer <= 0f)
            {
                _cleanupTimer = 1f;
                RecycleFarEnemies();
            }
        }

        private void GatherPlayers()
        {
            _players.Clear();
            foreach (PlayerHealth p in PlayerHealth.ActivePlayers)
                if (p != null && !p.IsDowned && p.gameObject.scene == gameObject.scene)
                    _players.Add(p.transform.position);
        }

        private void CountNearby()
        {
            _nearbyCount = 0;
            for (int i = 0; i < _active.Count; i++)
                if (NearestPlayerDistance(_active[i].transform.position) <= spawnDistanceMax)
                    _nearbyCount++;
        }

        // ------------------------------------------------------------------ ambient

        private void SpawnAmbientGroup(int amount)
        {
            _candidates.Clear();
            foreach (EnemySpawnPoint point in EnemySpawnPoint.All)
            {
                if (!point.UseForAmbient || !IsUsable(point)) continue;
                if (NearestPlayerDistance(point.Position) <= spawnDistanceMax)
                    _candidates.Add(point);
            }
            if (_candidates.Count == 0) return;

            SpawnGroup(_candidates[UnityEngine.Random.Range(0, _candidates.Count)], amount);
        }

        // ------------------------------------------------------------------ objectives

        private void UpdateObjectiveWaves(float dt)
        {
            MissionObjectiveSite site = EngagedObjective();
            if (site == null)
            {
                _objectiveStatus = _mission == null ? "no mission in scene" : "nobody at an objective";
                // Let the first wave come quickly once the team arrives.
                _objectiveTimer = Mathf.Min(_objectiveTimer, 3f);
                return;
            }

            _objectiveStatus = $"team at {site.name}";
            _objectiveTimer -= dt;
            if (_objectiveTimer > 0f) return;
            _objectiveTimer = Mathf.Lerp(objectiveIntervalStart, objectiveIntervalEnd, Intensity);

            // Points around the objective, closest first, so waves converge on it.
            _candidates.Clear();
            Vector2 target = site.transform.position;
            foreach (EnemySpawnPoint point in EnemySpawnPoint.All)
                if (IsUsable(point) && Vector2.Distance(point.Position, target) <= objectiveSpawnRadius)
                    _candidates.Add(point);
            if (_candidates.Count == 0)
            {
                _objectiveStatus += " (no usable points nearby!)";
                return;
            }
            _candidates.Sort((a, b) =>
                Vector2.Distance(a.Position, target).CompareTo(Vector2.Distance(b.Position, target)));

            int total = Mathf.RoundToInt(Mathf.Lerp(objectiveWaveStart, objectiveWaveEnd, Intensity));
            int directions = Mathf.Min(objectiveWaveDirections, _candidates.Count);
            // Pick from the nearest handful at random so it isn't always the same alley.
            int pool = Mathf.Min(_candidates.Count, directions * 2);
            for (int d = 0; d < directions; d++)
            {
                int pick = UnityEngine.Random.Range(d, pool);
                (_candidates[d], _candidates[pick]) = (_candidates[pick], _candidates[d]);
                int share = total / directions + (d < total % directions ? 1 : 0);
                SpawnGroup(_candidates[d], share);
            }
        }

        /// <summary>An objective whose step is running and that a player is standing near, or null.</summary>
        private MissionObjectiveSite EngagedObjective()
        {
            if (_mission == null || _mission.State != FireRescueMission.Phase.Objective) return null;
            foreach (MissionObjectiveSite site in _mission.Objectives)
            {
                if (site == null || _mission.GetCurrentStep(site) == null) continue;
                if (NearestPlayerDistance(site.transform.position) <= objectiveEngageRadius)
                    return site;
            }
            return null;
        }

        // ------------------------------------------------------------------ spawning

        private bool IsUsable(EnemySpawnPoint point) =>
            point != null && point.IsReady
            && NearestPlayerDistance(point.Position) >= spawnDistanceMin
            && IsOffscreen(point.Position);

        private void SpawnGroup(EnemySpawnPoint point, int amount)
        {
            point.MarkUsed();
            FlowFieldBlockedCheck blocked = new FlowFieldBlockedCheck(HordeNavGrid.Instance);

            for (int i = 0; i < amount && _active.Count < maxAlive; i++)
            {
                EnemyEntry entry = PickEntry();
                if (entry == null) return;

                // Try a few scatter spots so nobody spawns inside a wall.
                Vector2 pos = point.Position;
                for (int attempt = 0; attempt < 4; attempt++)
                {
                    Vector2 candidate = point.Position + UnityEngine.Random.insideUnitCircle * point.ScatterRadius;
                    if (!blocked.IsBlocked(candidate)) { pos = candidate; break; }
                }

                HordeEnemy e = _pool.Get();
                e.SetDefinition(entry.type); // pooled enemies remember their last type
                e.transform.position = new Vector3(pos.x, pos.y, point.transform.position.z);

                entry.spawned++;
                _totalSpawned++;
            }
        }

        private EnemyEntry PickEntry()
        {
            float total = UnlockedTotal();
            if (total <= 0f) return null;

            float roll = UnityEngine.Random.value * total;
            EnemyEntry last = null;
            foreach (EnemyEntry entry in enemyTypes)
            {
                if (!IsAvailable(entry)) continue;
                last = entry;
                roll -= entry.spawnPercent;
                if (roll <= 0f) return entry;
            }
            return last;
        }

        private float UnlockedTotal()
        {
            float total = 0f;
            foreach (EnemyEntry entry in enemyTypes)
                if (IsAvailable(entry)) total += entry.spawnPercent;
            return total;
        }

        private bool IsAvailable(EnemyEntry entry) =>
            entry != null && entry.type != null && entry.spawnPercent > 0f && _elapsed >= entry.unlockTime;

        // ------------------------------------------------------------------ cleanup

        private void RecycleFarEnemies()
        {
            float now = Time.time;
            _recycleBuffer.Clear();
            for (int i = 0; i < _active.Count; i++)
            {
                HordeEnemy e = _active[i];
                Vector2 pos = e.transform.position;
                if (NearestPlayerDistance(pos) > recycleDistance && IsOffscreen(pos))
                {
                    if (!_farSince.TryGetValue(e, out float since)) _farSince[e] = now;
                    else if (now - since >= recycleDelay) _recycleBuffer.Add(e);
                }
                else _farSince.Remove(e);
            }
            // Release after the loop: releasing edits _active.
            foreach (HordeEnemy e in _recycleBuffer) { e.Despawn(); _recycled++; }
        }

        // ------------------------------------------------------------------ helpers

        private float NearestPlayerDistance(Vector2 position)
        {
            float best = float.MaxValue;
            foreach (Vector2 p in _players)
                best = Mathf.Min(best, Vector2.Distance(position, p));
            return best;
        }

        private bool IsOffscreen(Vector2 position)
        {
            if (_camera == null) return true;
            Vector3 v = _camera.WorldToViewportPoint(new Vector3(position.x, position.y, 0f));
            float m = offscreenMargin;
            return v.z <= 0f || v.x < -m || v.x > 1f + m || v.y < -m || v.y > 1f + m;
        }

        private void RefreshPointDebug()
        {
            _validAmbientPoints = 0;
            foreach (EnemySpawnPoint point in EnemySpawnPoint.All)
            {
                bool valid = IsUsable(point) && NearestPlayerDistance(point.Position) <= spawnDistanceMax;
                point.DebugValid = valid;
                if (valid && point.UseForAmbient) _validAmbientPoints++;
            }
        }

        /// <summary>Skips spawn spots inside walls when a HordeNavGrid is in the scene.</summary>
        private readonly struct FlowFieldBlockedCheck
        {
            private readonly HordeNavGrid _grid;
            public FlowFieldBlockedCheck(HordeNavGrid grid) => _grid = grid;
            public bool IsBlocked(Vector2 p) =>
                _grid != null && _grid.Field != null && _grid.Field.IsBlockedAt(new System.Numerics.Vector2(p.x, p.y));
        }

        // ------------------------------------------------------------------ debug panel

        private void OnGUI()
        {
            if (!showPanel) return;

            float scale = uiScale * Screen.height / 1080f;
            GUI.matrix = Matrix4x4.Scale(new Vector3(scale, scale, 1f));
            float screenHeight = Screen.height / scale;

            const float width = 340f;
            float height = 250f + 20f * enemyTypes.Length;
            GUILayout.BeginArea(new Rect(10f, screenHeight - height - 10f, width, height), GUI.skin.box);
            GUILayout.Label("Playtest");

            if (!_running)
            {
                if (GUILayout.Button("START", GUILayout.Height(40f))) Begin();
            }
            else if (_players.Count == 0)
            {
                GUILayout.Label("Waiting for a living player...");
            }
            else
            {
                int minutes = (int)(_elapsed / 60f);
                int seconds = (int)(_elapsed % 60f);
                GUILayout.Label($"Time {minutes}:{seconds:00}   Intensity {Intensity * 100f:0}%{(IsEvac ? "   EVAC SURGE" : "")}");
                GUILayout.Label($"Alive {_active.Count}/{maxAlive}   Near team {_nearbyCount}/{ambientNearbyLimit}");
                GUILayout.Label($"Usable ambient points: {_validAmbientPoints}/{EnemySpawnPoint.All.Count}");
                GUILayout.Label($"Objective: {_objectiveStatus}");
                GUILayout.Label($"Spawned {_totalSpawned}   Recycled {_recycled}");
            }

            float total = UnlockedTotal();
            foreach (EnemyEntry entry in enemyTypes)
            {
                if (entry == null || entry.type == null) continue;
                float target = IsAvailable(entry) && total > 0f ? entry.spawnPercent / total * 100f : 0f;
                float actual = _totalSpawned > 0 ? entry.spawned * 100f / _totalSpawned : 0f;
                string locked = _elapsed < entry.unlockTime ? $" (unlocks {entry.unlockTime:0}s)" : "";
                GUILayout.Label($"{entry.type.DisplayName}: {target:0}%  spawned {entry.spawned} ({actual:0}%){locked}");
            }

            if (GUILayout.Button("RESET", GUILayout.Height(30f))) ResetScene();
            GUILayout.EndArea();
        }

        private void OnDrawGizmosSelected()
        {
            if (!Application.isPlaying) return;
            // Spawn band around each player: inner = never spawn closer, outer = ambient reach.
            foreach (Vector2 p in _players)
            {
                Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.8f);
                Gizmos.DrawWireSphere(p, spawnDistanceMin);
                Gizmos.color = new Color(1f, 0.6f, 0f, 0.8f);
                Gizmos.DrawWireSphere(p, spawnDistanceMax);
            }
        }
    }
}