using System.Collections.Generic;
using Fireline.Shared.Horde;
using UnityEngine;
using PlaneVector = System.Numerics.Vector2;

namespace Game.Runtime
{
    [DefaultExecutionOrder(-110)]
    public class HordeNavGrid : MonoBehaviour
    {
        public static HordeNavGrid Instance { get; private set; }

        [Header("Area")]
        [Tooltip("Center of the walkable area (world XY). Right-click the component > Fit To Tilemaps to size it to the level.")]
        [SerializeField] private Vector2 center;
        [Tooltip("Size of the walkable area (world units). Enemies outside it walk straight at players.")]
        [SerializeField] private Vector2 size = new Vector2(40f, 40f);
        [Tooltip("Grid resolution in world units. Smaller = tighter paths through narrow gaps, but slower rebuilds. 0.5 is a good start.")]
        [SerializeField, Min(0.1f)] private float cellSize = 0.5f;

        [Header("Obstacles")]
        [Tooltip("Layers that block enemies. Structures put themselves on \"Obstacles\".")]
        [SerializeField] private LayerMask obstacleLayers;
        [Tooltip("About an enemy's radius. Obstacles are padded by this, so a gap narrower than twice this is treated as closed.")]
        [SerializeField, Min(0f)] private float enemyRadius = 0.3f;

        [Header("Updating")]
        [Tooltip("Seconds between path rebuilds. Lower follows players more tightly; higher is cheaper.")]
        [SerializeField, Min(0.02f)] private float rebuildInterval = 0.2f;
        [Tooltip("Enemies within this path distance of a player check whether they can see them, and walk straight at them if so.")]
        [SerializeField, Min(0f)] private float sightRange = 12f;

        [Header("Debug")]
        [Tooltip("When selected, draw blocked cells.")]
        [SerializeField] private bool showBlocked = true;
        [Tooltip("When selected in Play mode, draw each cell's direction (can be slow on big grids).")]
        [SerializeField] private bool showFlow;

        private FlowField _field;
        private float _rebuildTimer;
        private int _lastTargetCount = -1;
        private readonly List<Collider2D> _overlapBuffer = new List<Collider2D>(4);

        public FlowField Field => _field;

        public float LastBuildMs { get; private set; }

        private void Reset()
        {
            obstacleLayers = LayerMask.GetMask(Structure.ObstacleLayer);
            FitToTilemaps();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Debug.LogWarning("Two HordeNavGrids in the scene; disabling the extra one.", this);
                enabled = false;
                return;
            }
            Instance = this;
            if (obstacleLayers.value == 0)
                Debug.LogWarning($"{name}: no Obstacle Layers set, so enemies won't path around anything.", this);
            Bake();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Called by HordeManager every frame with the living players. Rebuilds the field on its
        /// interval, or right away if players joined, died or respawned (so target indices stay valid).
        /// Returns the field to sample, or null if disabled.
        /// </summary>
        public FlowField Tick(List<PlaneVector> playerPositions, float deltaTime)
        {
            if (!isActiveAndEnabled || _field == null) return null;

            _rebuildTimer -= deltaTime;
            if (_rebuildTimer <= 0f || playerPositions.Count != _lastTargetCount)
            {
                var watch = System.Diagnostics.Stopwatch.StartNew();
                _field.SightRange = sightRange;
                _field.Build(playerPositions);
                LastBuildMs = (float)watch.Elapsed.TotalMilliseconds;

                _rebuildTimer = rebuildInterval;
                _lastTargetCount = playerPositions.Count;
            }
            return _field;
        }

        /// <summary>Re-reads every obstacle. Call after big level changes.</summary>
        [ContextMenu("Bake")]
        public void Bake()
        {
            int width = Mathf.Max(1, Mathf.CeilToInt(size.x / cellSize));
            int height = Mathf.Max(1, Mathf.CeilToInt(size.y / cellSize));
            Vector2 origin = center - new Vector2(width, height) * (cellSize * 0.5f);

            _field = new FlowField(width, height, cellSize, new PlaneVector(origin.x, origin.y));
            BakeCells(0, 0, width - 1, height - 1);
        }

        /// <summary>Re-reads obstacles inside <paramref name="area"/> only, e.g. where a structure was built or destroyed.</summary>
        public void RebakeArea(Bounds area)
        {
            if (_field == null) { Bake(); return; }

            float pad = enemyRadius + cellSize;
            _field.TryWorldToCell(new PlaneVector(area.min.x - pad, area.min.y - pad), out int x0, out int y0);
            _field.TryWorldToCell(new PlaneVector(area.max.x + pad, area.max.y + pad), out int x1, out int y1);
            BakeCells(Mathf.Max(0, x0), Mathf.Max(0, y0), Mathf.Min(_field.Width - 1, x1), Mathf.Min(_field.Height - 1, y1));
        }

        [ContextMenu("Rebake")]
        public void Rebake() => Bake();

        private void BakeCells(int x0, int y0, int x1, int y1)
        {
            Physics2D.SyncTransforms();
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(obstacleLayers);
            var boxSize = Vector2.one * (cellSize + 2f * enemyRadius);

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    PlaneVector c = _field.CellCenter(x, y);
                    int hits = Physics2D.OverlapBox(new Vector2(c.X, c.Y), boxSize, 0f, filter, _overlapBuffer);
                    _field.SetBlocked(x, y, hits > 0);
                }
            }
            _lastTargetCount = -1; // force a rebuild next tick
        }

        /// <summary>Sizes the area to cover every tilemap in the scene, plus a small margin.</summary>
        [ContextMenu("Fit To Tilemaps")]
        public void FitToTilemaps()
        {
            var renderers = FindObjectsByType<UnityEngine.Tilemaps.TilemapRenderer>(FindObjectsSortMode.None);
            if (renderers.Length == 0)
            {
                Debug.LogWarning($"{name}: no tilemaps found to fit to; set Center and Size by hand.", this);
                return;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) bounds.Encapsulate(renderers[i].bounds);

#if UNITY_EDITOR
            UnityEditor.Undo.RecordObject(this, "Fit Nav Grid To Tilemaps");
#endif
            center = bounds.center;
            size = new Vector2(bounds.size.x, bounds.size.y) + Vector2.one * 2f;
        }

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.5f);
            Gizmos.DrawWireCube(center, size);
        }

        private void OnDrawGizmosSelected()
        {
            if (_field == null || !showBlocked && !showFlow) return;

            float s = _field.CellSize;
            for (int y = 0; y < _field.Height; y++)
            {
                for (int x = 0; x < _field.Width; x++)
                {
                    PlaneVector c = _field.CellCenter(x, y);
                    var p = new Vector3(c.X, c.Y, 0f);

                    if (showBlocked && _field.IsBlocked(x, y))
                    {
                        Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.35f);
                        Gizmos.DrawCube(p, new Vector3(s, s, 0f));
                    }
                    else if (showFlow && Application.isPlaying)
                    {
                        PlaneVector d = _field.GetCellDirection(x, y);
                        if (d == PlaneVector.Zero) continue;
                        Gizmos.color = _field.HasLineOfSight(x, y) ? new Color(0.4f, 1f, 0.4f, 0.6f) : new Color(1f, 0.9f, 0.3f, 0.8f);
                        Gizmos.DrawLine(p, p + new Vector3(d.X, d.Y, 0f) * (s * 0.45f));
                    }
                }
            }
        }
#endif
    }
}
