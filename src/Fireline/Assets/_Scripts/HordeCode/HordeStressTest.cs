using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Pool;

namespace Game.Runtime
{
    /// <summary>
    /// Enemy spawner cheat tool for testing and demos.
    /// Pick an enemy type in the panel, then spawn a ring of them with the buttons,
    /// or right-click anywhere in the Game view to drop them at the cursor.
    /// </summary>
    public class HordeStressTest : MonoBehaviour
    {
        [Header("Spawning")]
        [SerializeField] private HordeEnemy enemyPrefab;
        [Tooltip("Enemies created up front at scene load, so the first big spawn doesn't hitch.")]
        [SerializeField, Min(0)] private int prewarmCount = 300;
        [SerializeField, Min(1)] private int maxPoolSize = 2000;
        [SerializeField, Min(0f)] private float spawnRadiusMin = 8f;
        [SerializeField, Min(0f)] private float spawnRadiusMax = 12f;

        [Header("Enemy Types")]
        [Tooltip("Configs you can pick from in the panel. Leave empty to spawn whatever config is on the prefab.")]
        [SerializeField] private EnemyDefinition[] enemyTypes = new EnemyDefinition[0];

        [Header("Right-Click Spawning")]
        [SerializeField] private bool rightClickToSpawn = true;
        [Tooltip("How many enemies each right-click spawns.")]
        [SerializeField, Min(1)] private int enemiesPerClick = 1;
        [Tooltip("Random spread around the cursor, so several enemies don't stack on one spot.")]
        [SerializeField, Min(0f)] private float clickScatter = 0.4f;

        [Header("Debug UI")]
        [SerializeField] private bool showPanel = true;

        private const float PanelWidth = 280f;
        private Rect _panelRect;
        private int _selectedType;

        private ObjectPool<HordeEnemy> _pool;
        private readonly HashSet<HordeEnemy> _active = new HashSet<HordeEnemy>();
        private readonly List<HordeEnemy> _releaseBuffer = new List<HordeEnemy>();
        private float _smoothedFrameMs;
        private float _smoothedHordeMs;

        private void Awake()
        {
            if (enemyPrefab == null)
            {
                Debug.LogError("HordeStressTest needs an enemy prefab.", this);
                enabled = false;
                return;
            }

            _pool = new ObjectPool<HordeEnemy>(
                createFunc: CreateEnemy,
                actionOnGet: e => { e.gameObject.SetActive(true); _active.Add(e); },
                actionOnRelease: e => { e.gameObject.SetActive(false); _active.Remove(e); },
                actionOnDestroy: e => { if (e != null) Destroy(e.gameObject); },
                collectionCheck: false,
                defaultCapacity: prewarmCount,
                maxSize: maxPoolSize);
        }

        private void Start()
        {
            if (_pool == null) return;
            
            for (int i = 0; i < prewarmCount; i++)
                _releaseBuffer.Add(_pool.Get());
            for (int i = 0; i < _releaseBuffer.Count; i++)
                _pool.Release(_releaseBuffer[i]);
            _releaseBuffer.Clear();
        }

        private HordeEnemy CreateEnemy()
        {
            HordeEnemy e = Instantiate(enemyPrefab, transform.position, Quaternion.identity);
            e.gameObject.SetActive(false);
            e.Pool = _pool;
            return e;
        }

        /// <summary>The config new enemies get, or null to keep the prefab's own.</summary>
        public EnemyDefinition SelectedType =>
            enemyTypes != null && enemyTypes.Length > 0 ? enemyTypes[Mathf.Clamp(_selectedType, 0, enemyTypes.Length - 1)] : null;

        /// <summary>Spawns enemies of the selected type in a ring around this object.</summary>
        public void Spawn(int amount) => Spawn(amount, SelectedType);

        /// <summary>Spawns enemies with a specific config in a ring around this object.</summary>
        public void Spawn(int amount, EnemyDefinition type)
        {
            Vector3 center = transform.position;
            for (int i = 0; i < amount; i++)
            {
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                float r = Random.Range(spawnRadiusMin, spawnRadiusMax);
                SpawnOne(type, center + new Vector3(dir.x, dir.y, 0f) * r);
            }
        }

        /// <summary>Spawns enemies with a specific config scattered around a world position.</summary>
        public void SpawnAt(Vector2 position, int amount, EnemyDefinition type)
        {
            for (int i = 0; i < amount; i++)
            {
                Vector2 p = position + Random.insideUnitCircle * clickScatter;
                SpawnOne(type, new Vector3(p.x, p.y, transform.position.z));
            }
        }

        private void SpawnOne(EnemyDefinition type, Vector3 position)
        {
            if (_pool == null) return;
            HordeEnemy e = _pool.Get();
            // Pooled enemies remember their last config, so always set it.
            if (type != null) e.SetDefinition(type);
            e.CachedTransform.position = position;
        }

        public void DespawnAll()
        {
            if (_pool == null) return;

            _releaseBuffer.AddRange(_active);
            for (int i = 0; i < _releaseBuffer.Count; i++)
                _pool.Release(_releaseBuffer[i]);
            _releaseBuffer.Clear();
        }

        private void Update()
        {
            // Light smoothing so the numbers are readable instead of flickering.
            _smoothedFrameMs = Mathf.Lerp(_smoothedFrameMs, Time.unscaledDeltaTime * 1000f, 0.05f);

            if (HordeManager.Instance != null)
                _smoothedHordeMs = Mathf.Lerp(_smoothedHordeMs, HordeManager.Instance.LastUpdateMs, 0.05f);

            HandleRightClick();
        }

        private void HandleRightClick()
        {
            Mouse mouse = Mouse.current;
            Camera cam = Camera.main;
            if (!rightClickToSpawn || mouse == null || cam == null || !mouse.rightButton.wasPressedThisFrame)
                return;

            Vector2 screen = mouse.position.ReadValue();
            // IMGUI measures y from the top of the screen; the mouse measures from the bottom.
            if (showPanel && _panelRect.Contains(new Vector2(screen.x, Screen.height - screen.y)))
                return;

            Ray ray = cam.ScreenPointToRay(screen);
            if (!new Plane(Vector3.forward, transform.position).Raycast(ray, out float distance)) return;
            Vector3 world = ray.GetPoint(distance);
            
            SpawnAt(world, enemiesPerClick, SelectedType);
        }

        private void OnGUI()
        {
            if (!showPanel) return;

            int typeCount = enemyTypes != null ? enemyTypes.Length : 0;
            float height = 200f + (typeCount > 0 ? 30f + 24f * Mathf.Ceil(typeCount / 2f) : 0f) + (rightClickToSpawn ? 22f : 0f);
            _panelRect = new Rect(10, 10, PanelWidth, height);
            GUILayout.BeginArea(_panelRect, GUI.skin.box);

            HordeManager manager = HordeManager.Instance;
            int enemyCount = manager != null ? manager.EnemyCount : 0;
            int playerCount = manager != null ? manager.PlayerCount : 0;
            float fps = _smoothedFrameMs > 0.0001f ? 1000f / _smoothedFrameMs : 0f;

            GUILayout.Label($"Enemies: {enemyCount}");
            GUILayout.Label($"Player targets: {playerCount}");
            GUILayout.Label($"Frame: {_smoothedFrameMs:F2} ms  ({fps:F0} fps)");
            GUILayout.Label($"Horde AI: {_smoothedHordeMs:F2} ms");
            if (manager == null)
                GUILayout.Label("No HordeManager in the scene!");
            else if (playerCount == 0)
                GUILayout.Label("No Player-tagged objects found!");

            if (typeCount > 0)
            {
                GUILayout.Space(4);
                GUILayout.Label("Enemy type:");
                string[] names = new string[typeCount];
                for (int i = 0; i < typeCount; i++)
                    names[i] = enemyTypes[i] != null ? enemyTypes[i].DisplayName : "(empty)";
                _selectedType = GUILayout.SelectionGrid(Mathf.Clamp(_selectedType, 0, typeCount - 1), names, 2);
            }
            if (rightClickToSpawn)
                GUILayout.Label($"Right-click to place {enemiesPerClick}");

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+1")) Spawn(1);
            if (GUILayout.Button("+10")) Spawn(10);
            if (GUILayout.Button("+50")) Spawn(50);
            if (GUILayout.Button("+100")) Spawn(100);
            GUILayout.EndHorizontal();

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("+500")) Spawn(500);
            if (GUILayout.Button("Clear")) DespawnAll();
            GUILayout.EndHorizontal();

            GUILayout.EndArea();
        }

        private void OnDestroy()
        {
            _pool?.Dispose();
        }
    }
}
