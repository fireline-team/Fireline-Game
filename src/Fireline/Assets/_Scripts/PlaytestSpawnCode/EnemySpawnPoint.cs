using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// A place enemies can come from. Scatter these around the level: alleys, building
    /// doors, map edges, behind walls. The PlaytestSpawner decides which ones are usable
    /// each moment (far enough from players, not on camera), so place plenty and let it choose.
    ///
    /// Points register themselves while enabled, so there's no list to keep in sync.
    /// </summary>
    public class EnemySpawnPoint : MonoBehaviour
    {
        private static readonly List<EnemySpawnPoint> Active = new List<EnemySpawnPoint>();
        public static IReadOnlyList<EnemySpawnPoint> All => Active;

        [Tooltip("Enemies in a group are scattered within this radius, so they don't stack on one spot.")]
        [SerializeField, Min(0f)] private float scatterRadius = 1.5f;
        [Tooltip("Seconds before this point can be used again, so one alley doesn't spew forever.")]
        [SerializeField, Min(0f)] private float cooldown = 4f;
        [Tooltip("Off: only used for objective/evac waves, never for the ambient trickle. Good for points right next to an objective.")]
        [SerializeField] private bool useForAmbient = true;

        public float ScatterRadius => scatterRadius;
        public bool UseForAmbient => useForAmbient;
        public bool IsReady => Time.time >= _readyTime;
        public Vector2 Position => transform.position;

        /// <summary>Last state the spawner saw, for gizmos only.</summary>
        internal bool DebugValid;

        private float _readyTime;

        private void OnEnable() => Active.Add(this);
        private void OnDisable() => Active.Remove(this);

        public void MarkUsed() => _readyTime = Time.time + cooldown;

        private void OnDrawGizmos()
        {
            // Green = usable right now, red = rejected (too close, on screen, cooling down). Grey outside Play mode.
            Gizmos.color = !Application.isPlaying ? new Color(1f, 0.5f, 0f, 0.9f)
                : DebugValid ? Color.green : new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireSphere(transform.position, Mathf.Max(0.3f, scatterRadius));
            Gizmos.DrawLine(transform.position + Vector3.left * 0.3f, transform.position + Vector3.right * 0.3f);
            Gizmos.DrawLine(transform.position + Vector3.down * 0.3f, transform.position + Vector3.up * 0.3f);
        }
    }
}