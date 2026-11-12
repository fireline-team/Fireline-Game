using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Shoots at the nearest player when they're in range and in sight. Shots can be parried
    /// back with the axe. Pair it with a Keep Distance on the EnemyDefinition so the enemy
    /// hangs back instead of walking into melee.
    /// </summary>
    [CreateAssetMenu(fileName = "RangedAttack", menuName = "Fireline/Enemy Abilities/Ranged Attack")]
    public class RangedAttackAbility : EnemyAbility
    {
        [Header("Firing")]
        [Tooltip("Only shoots at players closer than this.")]
        [SerializeField, Min(0.5f)] private float range = 8f;
        [Tooltip("Seconds between shots.")]
        [SerializeField, Min(0.1f)] private float fireInterval = 2.5f;
        [Tooltip("The enemy stops and aims for this long before each shot. Gives players a tell to dodge or parry.")]
        [SerializeField, Min(0f)] private float aimTime = 0.4f;
        [Tooltip("Don't shoot through walls.")]
        [SerializeField] private bool needsLineOfSight = true;

        [Header("Projectile")]
        [SerializeField, Min(0.1f)] private float projectileSpeed = 7f;
        [SerializeField, Min(0f)] private float damage = 10f;
        [Tooltip("Seconds before a miss fizzles out.")]
        [SerializeField, Min(0.1f)] private float lifetime = 3f;
        [Tooltip("Hit radius on the ground. The axe has to reach this to parry it.")]
        [SerializeField, Min(0.05f)] private float radius = 0.25f;

        [Header("Parry")]
        [Tooltip("A parried shot flies back this many times faster.")]
        [SerializeField, Min(0.1f)] private float reflectSpeedMultiplier = 1.5f;
        [Tooltip("Damage a parried shot deals to the enemy it hits.")]
        [SerializeField, Min(0f)] private float reflectDamage = 25f;

        [Header("Looks")]
        [Tooltip("Optional. A prefab with an EnemyProjectile on it, for custom art/effects. Leave empty to build one from the sprite below.")]
        [SerializeField] private EnemyProjectile projectilePrefab;
        [Tooltip("Used when there's no prefab. Empty = a plain glowing dot.")]
        [SerializeField] private Sprite sprite;
        [SerializeField] private Color color = new Color(1f, 0.55f, 0.15f);
        [SerializeField, Min(0.05f)] private float size = 0.5f;
        [Tooltip("How high off the ground the shot is drawn.")]
        [SerializeField, Min(0f)] private float height = 0.6f;

        public override EnemyAbilityRuntime CreateRuntime(HordeEnemy owner) => new Runtime(owner, this);

        private EnemyProjectile.Settings Settings => new EnemyProjectile.Settings
        {
            Speed = projectileSpeed,
            Damage = damage,
            Lifetime = lifetime,
            Radius = radius,
            ReflectSpeedMultiplier = reflectSpeedMultiplier,
            ReflectDamage = reflectDamage,
        };

        private EnemyProjectile Spawn()
        {
            if (projectilePrefab != null) return Instantiate(projectilePrefab);
            return EnemyProjectile.Create(sprite, color, size, height);
        }

        private sealed class Runtime : EnemyAbilityRuntime
        {
            private readonly RangedAttackAbility _config;
            private float _cooldown;
            private float _aiming = -1f;
            private Vector2 _aimAt;

            public Runtime(HordeEnemy owner, RangedAttackAbility config) : base(owner) => _config = config;

            // Stagger the first shot so a group of shooters doesn't fire in one volley.
            public override void OnSpawned()
            {
                _cooldown = Random.Range(0.5f, 1f) * _config.fireInterval;
                _aiming = -1f;
            }

            public override void Tick(float deltaTime, IHordeTargets targets)
            {
                if (_aiming >= 0f)
                {
                    // Track the player while aiming, then fire.
                    if (targets.TryGetNearestPlayer(OwnerPosition, out Vector2 latest)) _aimAt = latest;
                    _aiming -= deltaTime;
                    if (_aiming < 0f) Fire();
                    return;
                }

                _cooldown -= deltaTime;
                if (_cooldown > 0f) return;
                if (!targets.TryGetNearestPlayer(OwnerPosition, out Vector2 player)) return;
                if ((player - OwnerPosition).sqrMagnitude > _config.range * _config.range) return;
                if (_config.needsLineOfSight && Blocked(OwnerPosition, player)) return;

                _aimAt = player;
                _aiming = _config.aimTime;
                Owner.Hold(_config.aimTime + 0.15f);
                if (_aiming <= 0f) Fire();
            }

            private void Fire()
            {
                _aiming = -1f;
                _cooldown = _config.fireInterval;
                Vector2 from = OwnerPosition;
                Vector2 direction = _aimAt - from;
                if (direction.sqrMagnitude < 0.0001f) return;
                _config.Spawn().Launch(from, direction, _config.Settings);
            }

            private static bool Blocked(Vector2 from, Vector2 to)
            {
                int walls = LayerMask.GetMask(Structure.ObstacleLayer);
                return walls != 0 && Physics2D.Linecast(from, to, walls).collider != null;
            }
        }
    }
}
