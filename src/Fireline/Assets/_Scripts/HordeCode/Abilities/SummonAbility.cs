using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Every so often, calls in more enemies around itself while a player is nearby. Summons
    /// climb out of the ground (give their EnemyDefinition a Rise Duration) and come from the
    /// same pool as the summoner, so they're recycled like any other enemy.
    /// </summary>
    [CreateAssetMenu(fileName = "Summon", menuName = "Fireline/Enemy Abilities/Summon")]
    public class SummonAbility : EnemyAbility
    {
        [Tooltip("What it summons. Avoid a type that can summon itself, or they'll multiply.")]
        [SerializeField] private EnemyDefinition summonType;
        [Tooltip("Enemies per summon.")]
        [SerializeField, Min(1)] private int countPerSummon = 3;
        [Tooltip("Seconds between summons.")]
        [SerializeField, Min(0.5f)] private float interval = 7f;
        [Tooltip("Most of its summons alive at once. It waits while it's at the limit.")]
        [SerializeField, Min(1)] private int maxAlive = 6;
        [Tooltip("Summons appear within this distance of the summoner.")]
        [SerializeField, Min(0.3f)] private float radius = 1.8f;
        [Tooltip("Only summons while a player is within this distance, so it doesn't fill the map offscreen.")]
        [SerializeField, Min(1f)] private float activationRange = 14f;
        [Tooltip("The summoner stands still for this long while summoning.")]
        [SerializeField, Min(0f)] private float channelTime = 0.6f;

        public override EnemyAbilityRuntime CreateRuntime(HordeEnemy owner) => new Runtime(owner, this);

        private sealed class Runtime : EnemyAbilityRuntime
        {
            private static readonly List<Collider2D> WallProbe = new List<Collider2D>(1);

            private readonly SummonAbility _config;
            // Pooled enemies get reused, so remember which life each summon was on.
            private readonly List<(HordeEnemy enemy, int generation)> _summons = new List<(HordeEnemy, int)>();
            private float _cooldown;

            public Runtime(HordeEnemy owner, SummonAbility config) : base(owner) => _config = config;

            public override void OnSpawned()
            {
                _summons.Clear();
                _cooldown = _config.interval * 0.5f; // first call comes a bit sooner
            }

            public override void Tick(float deltaTime, IHordeTargets targets)
            {
                if (_config.summonType == null) return;
                _cooldown -= deltaTime;
                if (_cooldown > 0f) return;

                if (!targets.TryGetNearestPlayer(OwnerPosition, out Vector2 player)
                    || (player - OwnerPosition).sqrMagnitude > _config.activationRange * _config.activationRange) return;

                int room = _config.maxAlive - CountAlive();
                if (room <= 0) return;

                _cooldown = _config.interval;
                Owner.Hold(_config.channelTime);
                int count = Mathf.Min(room, _config.countPerSummon);
                for (int i = 0; i < count; i++) SummonOne(i, count);
            }

            private void SummonOne(int index, int count)
            {
                // Spread around the summoner, nudged randomly, avoiding walls.
                float angle = (index + Random.value * 0.5f) / count * Mathf.PI * 2f;
                Vector2 spot = OwnerPosition + new Vector2(Mathf.Cos(angle), Mathf.Sin(angle)) * _config.radius;
                if (InsideWall(spot)) spot = OwnerPosition;

                HordeEnemy summon = Owner.Pool != null ? Owner.Pool.Get() : Object.Instantiate(Owner);
                summon.transform.position = new Vector3(spot.x, spot.y, 0f);
                summon.SetDefinition(_config.summonType); // also starts the rise from the ground
                _summons.Add((summon, summon.Generation));
            }

            private int CountAlive()
            {
                for (int i = _summons.Count - 1; i >= 0; i--)
                {
                    var (enemy, generation) = _summons[i];
                    if (enemy == null || !enemy.isActiveAndEnabled || enemy.Generation != generation)
                        _summons.RemoveAt(i);
                }
                return _summons.Count;
            }

            private static bool InsideWall(Vector2 point)
            {
                int walls = LayerMask.GetMask(Structure.ObstacleLayer);
                if (walls == 0) return false;
                var filter = new ContactFilter2D();
                filter.SetLayerMask(walls);
                return Physics2D.OverlapCircle(point, 0.3f, filter, WallProbe) > 0;
            }
        }
    }
}
