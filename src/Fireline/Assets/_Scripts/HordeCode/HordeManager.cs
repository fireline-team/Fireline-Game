using System.Collections.Generic;
using Fireline.Shared.Horde;
using UnityEngine;
using PlaneVector = System.Numerics.Vector2;

namespace Game.Runtime
{
    [DefaultExecutionOrder(-100)]
    public class HordeManager : MonoBehaviour
    {
        public static HordeManager Instance { get; private set; }

        [Header("Targeting")]
        [SerializeField] private string playerTag = "Player";
        [Tooltip("How often (seconds) to re-scan for Player-tagged objects. Scanning allocates, so don't do it every frame.")]
        [SerializeField, Min(0.05f)] private float playerRefreshInterval = 0.5f;
        [Tooltip("Enemies stop pushing forward once this close to their target.")]
        [SerializeField, Min(0f)] private float stopDistance = 0.6f;

        [Header("Separation (crowd spacing)")]
        [Tooltip("Enemies closer than this push each other apart. Roughly one enemy width.")]
        [SerializeField, Min(0.01f)] private float separationRadius = 0.8f;
        [SerializeField, Min(0f)] private float separationStrength = 3f;
        [Tooltip("Cap on neighbors each enemy reacts to. Keeps dense clumps from spiking the frame time.")]
        [SerializeField, Min(1)] private int maxNeighbors = 8;

        [Header("Facing")]
        [Tooltip("Flip each enemy's SpriteRenderer so it faces the way it walks. Art is assumed to face right unless its EnemyDefinition says otherwise.")]
        [SerializeField] private bool flipSpriteToFaceMovement = true;
        [Tooltip("Ignore tiny sideways movement so sprites don't flicker when walking almost straight up/down.")]
        [SerializeField, Min(0f)] private float flipDeadZone = 0.1f;
        
        private readonly List<HordeEnemy> _enemies = new List<HordeEnemy>(512);
        private readonly List<Transform> _playerTransforms = new List<Transform>(4);
        private readonly List<PlaneVector> _playerPositions = new List<PlaneVector>(4);
        private readonly List<int> _neighborBuffer = new List<int>(64);
        private readonly System.Diagnostics.Stopwatch _stopwatch = new System.Diagnostics.Stopwatch();

        private PlaneVector[] _positions = new PlaneVector[512];
        private SpatialHash _hash;
        private float _refreshTimer;

        public int EnemyCount => _enemies.Count;
        public int PlayerCount => _playerPositions.Count;
        public float LastUpdateMs { get; private set; }

        private static PlaneVector ToPlane(Vector3 v) => new PlaneVector(v.x, v.y);

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Two HordeManagers in the scene; destroying the extra one.", this);
                Destroy(this);
                return;
            }
            Instance = this;
            // Cell size = separation radius, so a 3x3 query always covers the full radius.
            _hash = new SpatialHash(separationRadius);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void OnValidate()
        {
            // Keep the hash in sync if you tweak the radius in the Inspector during play.
            if (Application.isPlaying && _hash != null && !Mathf.Approximately(_hash.CellSize, separationRadius))
                _hash = new SpatialHash(separationRadius);
        }

        public void Register(HordeEnemy enemy)
        {
            if (enemy.ManagerIndex >= 0) return;
            enemy.ManagerIndex = _enemies.Count;
            _enemies.Add(enemy);
        }

        public void Unregister(HordeEnemy enemy)
        {
            int i = enemy.ManagerIndex;
            if (i < 0 || i >= _enemies.Count || _enemies[i] != enemy) return;
            
            int last = _enemies.Count - 1;
            HordeEnemy moved = _enemies[last];
            _enemies[i] = moved;
            moved.ManagerIndex = i;
            _enemies.RemoveAt(last);
            enemy.ManagerIndex = -1;
        }

        private void RefreshPlayerList()
        {
            _playerTransforms.Clear();
            GameObject[] found = GameObject.FindGameObjectsWithTag(playerTag);
            for (int i = 0; i < found.Length; i++)
                _playerTransforms.Add(found[i].transform);
        }

        private void SnapshotPlayerPositions()
        {
            _playerPositions.Clear();
            for (int i = 0; i < _playerTransforms.Count; i++)
            {
                Transform t = _playerTransforms[i];
                if (t == null || !t.gameObject.activeInHierarchy) continue;
                _playerPositions.Add(ToPlane(t.position));
            }
        }

        private void Update()
        {
            _stopwatch.Restart();

            _refreshTimer -= Time.deltaTime;
            if (_refreshTimer <= 0f)
            {
                RefreshPlayerList();
                _refreshTimer = playerRefreshInterval;
            }
            SnapshotPlayerPositions();

            int count = _enemies.Count;
            if (count == 0)
            {
                LastUpdateMs = 0f;
                return;
            }

            if (_positions.Length < count)
                _positions = new PlaneVector[Mathf.NextPowerOfTwo(count)];
            
            _hash.Clear();
            for (int i = 0; i < count; i++)
            {
                PlaneVector p = ToPlane(_enemies[i].CachedTransform.position);
                _positions[i] = p;
                _hash.Insert(i, p);
            }

            float dt = Time.deltaTime;

            for (int i = 0; i < count; i++)
            {
                HordeEnemy enemy = _enemies[i];
                PlaneVector pos = _positions[i];

                PlaneVector seek = PlaneVector.Zero;
                int target = HordeSteering.FindNearest(pos, _playerPositions);
                if (target >= 0)
                    seek = HordeSteering.Seek(pos, _playerPositions[target], stopDistance);

                _hash.QueryNeighbors(pos, _neighborBuffer);
                PlaneVector push = HordeSteering.Separation(i, _positions, _neighborBuffer, separationRadius, maxNeighbors);

                PlaneVector velocity = seek * enemy.MoveSpeed + push * separationStrength;
                PlaneVector newPos = pos + velocity * dt;

                Transform tr = enemy.CachedTransform;
                tr.position = new Vector3(newPos.X, newPos.Y, tr.position.z);
                
                enemy.MoveDirection = new Vector2(seek.X, seek.Y);

                if (flipSpriteToFaceMovement && enemy.Sprite != null && Mathf.Abs(seek.X) > flipDeadZone)
                    enemy.Sprite.flipX = (seek.X < 0f) != enemy.ArtFacesLeft;
            }

            _stopwatch.Stop();
            LastUpdateMs = (float)_stopwatch.Elapsed.TotalMilliseconds;
        }

#if UNITY_EDITOR
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.35f);
            for (int i = 0; i < _enemies.Count; i++)
                Gizmos.DrawWireSphere(_enemies[i].CachedTransform.position, separationRadius * 0.5f);
        }
#endif
    }
}