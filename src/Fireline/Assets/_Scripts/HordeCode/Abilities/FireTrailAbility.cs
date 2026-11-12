using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Leaves small fires behind as it walks. They hurt players who step in them, can be put
    /// out with the hose, and burn out on their own after a while.
    /// </summary>
    [CreateAssetMenu(fileName = "FireTrail", menuName = "Fireline/Enemy Abilities/Fire Trail")]
    public class FireTrailAbility : EnemyAbility
    {
        [Tooltip("The fire to drop, e.g. Assets/_Prefabs/FireZone/FireZone (a smaller variant prefab works best).")]
        [SerializeField] private FireZone firePrefab;
        [Tooltip("Size multiplier on the prefab, for small fires.")]
        [SerializeField, Min(0.05f)] private float fireScale = 0.5f;
        [Tooltip("Seconds between fires (each gap is randomized by +/- 25%).")]
        [SerializeField, Min(0.2f)] private float interval = 3f;
        [Tooltip("Seconds a fire burns before going out on its own.")]
        [SerializeField, Min(0.5f)] private float fireLifetime = 8f;
        [Tooltip("Won't drop a new fire within this distance of its last one (no stacking while it stands still).")]
        [SerializeField, Min(0f)] private float minSpacing = 1.5f;
        [Tooltip("Most fires from one enemy burning at once.")]
        [SerializeField, Min(1)] private int maxPerEnemy = 3;
        [Tooltip("Most enemy-made fires in the whole level at once.")]
        [SerializeField, Min(1)] private int maxInLevel = 40;

        public override EnemyAbilityRuntime CreateRuntime(HordeEnemy owner)
        {
            if (firePrefab == null)
            {
                Debug.LogWarning($"{name}: Fire Trail has no Fire Prefab, so it won't start fires.", this);
                return null;
            }
            return new Runtime(owner, this);
        }

        private sealed class Runtime : EnemyAbilityRuntime
        {
            private readonly FireTrailAbility _config;
            private readonly List<FireZone> _fires = new List<FireZone>();
            private float _cooldown;
            private Vector2 _lastDrop;
            private bool _hasDropped;

            public Runtime(HordeEnemy owner, FireTrailAbility config) : base(owner) => _config = config;

            public override void OnSpawned()
            {
                _fires.Clear(); // old fires keep burning on their own timers
                _hasDropped = false;
                _cooldown = NextGap();
            }

            public override void Tick(float deltaTime, IHordeTargets targets)
            {
                _cooldown -= deltaTime;
                if (_cooldown > 0f) return;
                _cooldown = NextGap();

                Vector2 here = OwnerPosition;
                if (_hasDropped && (here - _lastDrop).sqrMagnitude < _config.minSpacing * _config.minSpacing) return;
                _fires.RemoveAll(f => f == null);
                if (_fires.Count >= _config.maxPerEnemy || TemporaryFire.ActiveCount >= _config.maxInLevel) return;

                FireZone fire = Object.Instantiate(_config.firePrefab, new Vector3(here.x, here.y, 0f), Quaternion.identity);
                fire.transform.localScale = _config.firePrefab.transform.localScale * _config.fireScale;
                fire.gameObject.AddComponent<TemporaryFire>().Begin(_config.fireLifetime);
                _fires.Add(fire);
                _lastDrop = here;
                _hasDropped = true;
            }

            private float NextGap() => _config.interval * Random.Range(0.75f, 1.25f);
        }
    }
}
