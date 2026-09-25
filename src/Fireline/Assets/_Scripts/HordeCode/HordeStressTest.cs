using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Runtime
{
    /// <summary>
    /// Enemy spawner cheat tool.
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

        [Header("Debug UI")]
        [SerializeField] private bool showPanel = true;

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

        public void Spawn(int amount)
        {
            if (_pool == null) return;

            Vector3 center = transform.position;
            for (int i = 0; i < amount; i++)
            {
                HordeEnemy e = _pool.Get();
                Vector2 dir = Random.insideUnitCircle.normalized;
                if (dir == Vector2.zero) dir = Vector2.up;
                float r = Random.Range(spawnRadiusMin, spawnRadiusMax);
                e.CachedTransform.position = center + new Vector3(dir.x, dir.y, 0f) * r;
            }
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
        }

        private void OnGUI()
        {
            if (!showPanel) return;

            GUILayout.BeginArea(new Rect(10, 10, 260, 200), GUI.skin.box);

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

            GUILayout.Space(6);
            GUILayout.BeginHorizontal();
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
