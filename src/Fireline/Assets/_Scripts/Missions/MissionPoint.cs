using UnityEngine;

namespace Fireline.Missions
{
    public sealed class MissionPoint : MonoBehaviour
    {
        [SerializeField, Min(0)] private float exclusionRadius = 10f;
        public float ExclusionRadius => Mathf.Max(0, exclusionRadius);
        public static bool CanPair(MissionPoint spawn, MissionPoint objective)
        {
            if (spawn == null || objective == null || spawn == objective) return false;
            float distance = spawn.ExclusionRadius + objective.ExclusionRadius;
            return ((Vector2)(spawn.transform.position - objective.transform.position)).sqrMagnitude >= distance * distance;
        }
        private void OnDrawGizmosSelected()
        {
            Gizmos.color = Color.yellow;
            Vector3 previous = transform.position + Vector3.right * ExclusionRadius;
            for (int i = 1; i <= 64; i++)
            {
                float angle = i * Mathf.PI * 2 / 64;
                Vector3 next = transform.position + new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0) * ExclusionRadius;
                Gizmos.DrawLine(previous, next);
                previous = next;
            }
        }
    }
}
