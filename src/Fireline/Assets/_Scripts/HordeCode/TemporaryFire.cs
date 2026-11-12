using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Makes a FireZone short-lived: it burns out on its own after a while, and disappears
    /// shortly after being put out. Used for fires that enemies leave behind, so they don't
    /// pile up across the map. Added at runtime by FireTrailAbility; no setup needed.
    /// </summary>
    [RequireComponent(typeof(FireZone))]
    public class TemporaryFire : MonoBehaviour
    {
        private static readonly List<TemporaryFire> Active = new List<TemporaryFire>();

        /// <summary>How many enemy-made fires are burning in total. Used to cap them.</summary>
        public static int ActiveCount => Active.Count;

        private FireZone _fire;
        private float _burnOutAt;
        private float _shrinkTime;
        private Vector3 _fullScale;
        private bool _ending;

        /// <param name="lifetime">Seconds before it burns out by itself.</param>
        /// <param name="shrinkTime">Seconds spent shrinking away at the end.</param>
        public void Begin(float lifetime, float shrinkTime = 0.6f)
        {
            _fire = GetComponent<FireZone>();
            _fullScale = transform.localScale;
            _shrinkTime = Mathf.Max(0.05f, shrinkTime);
            _burnOutAt = Time.time + Mathf.Max(0f, lifetime);
            _fire.OnExtinguished.AddListener(OnPutOut);
        }

        private void OnEnable() => Active.Add(this);

        private void OnDisable() => Active.Remove(this);

        private void OnDestroy()
        {
            if (_fire != null) _fire.OnExtinguished.RemoveListener(OnPutOut);
        }

        // Put out by a hose: leave a moment for the steam puff, then go.
        private void OnPutOut()
        {
            if (_ending) return;
            _ending = true;
            Destroy(gameObject, 1f);
        }

        private void Update()
        {
            if (_ending || _fire == null) return;
            float left = _burnOutAt - Time.time;
            if (left > _shrinkTime) return;

            if (left <= 0f)
            {
                _ending = true;
                Destroy(gameObject);
                return;
            }
            transform.localScale = _fullScale * (left / _shrinkTime);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ClearRegistry() => Active.Clear();
    }
}
