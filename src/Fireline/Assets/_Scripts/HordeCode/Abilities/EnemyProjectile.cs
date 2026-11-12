using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// A shot fired by an enemy. Flies straight along the ground, stops at walls, and hurts the
    /// first player it touches (rolling players dodge it).
    ///
    /// It's an AxeParryable: swing the axe into it and it flies back the way you aimed, faster,
    /// and now hurts enemies instead of players.
    ///
    /// RangedAttackAbility builds one automatically from a sprite, or you can make a prefab with
    /// this component (plus a trigger Collider2D) and assign it there for custom art/effects.
    /// </summary>
    [RequireComponent(typeof(CircleCollider2D))]
    public class EnemyProjectile : AxeParryable
    {
        /// <summary>Everything a shot needs, filled in by whoever fires it.</summary>
        public struct Settings
        {
            public float Speed;
            public float Damage;
            public float Lifetime;
            public float Radius;
            public float ReflectSpeedMultiplier;
            public float ReflectDamage;
        }

        private static readonly List<Collider2D> Overlaps = new List<Collider2D>(16);
        private static Sprite _defaultSprite;

        private Settings _settings;
        private Vector2 _velocity;
        private float _dieAt;
        private bool _reflected;
        private CircleCollider2D _collider;
        private int _wallMask;

        /// <summary>True once parried: it now hurts enemies.</summary>
        public bool IsReflected => _reflected;
        public Vector2 Velocity => _velocity;

        // ------------------------------------------------------------------ creation

        /// <summary>
        /// Builds a projectile from scratch: a trigger circle the axe can hit plus a raised,
        /// upright sprite. Null sprite = a plain glowing dot, fine for prototyping.
        /// </summary>
        public static EnemyProjectile Create(Sprite sprite, Color color, float size, float height)
        {
            var root = new GameObject("Enemy Projectile");
            var body = root.AddComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.interpolation = RigidbodyInterpolation2D.None;
            var projectile = root.AddComponent<EnemyProjectile>();

            var art = new GameObject("Sprite");
            art.transform.SetParent(root.transform, false);
            art.transform.localPosition = TiltedView.Up * height;
            art.transform.rotation = TiltedView.StandingRotation;
            art.transform.localScale = Vector3.one * size;
            var renderer = art.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite != null ? sprite : DefaultSprite();
            renderer.color = color;
            renderer.sortingOrder = 20;
            return projectile;
        }

        /// <summary>Sends it off. Call right after Create/Instantiate.</summary>
        public void Launch(Vector2 position, Vector2 direction, Settings settings)
        {
            _settings = settings;
            transform.position = new Vector3(position.x, position.y, 0f);
            _velocity = direction.normalized * settings.Speed;
            _dieAt = Time.time + settings.Lifetime;
            _reflected = false;
            IsParryable = true;
            if (_collider == null) _collider = GetComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _collider.radius = settings.Radius;
        }

        // ------------------------------------------------------------------ flight

        private void Awake()
        {
            _collider = GetComponent<CircleCollider2D>();
            _collider.isTrigger = true;
            _wallMask = LayerMask.GetMask(Structure.ObstacleLayer);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (Time.time >= _dieAt)
            {
                Die();
                return;
            }

            Vector2 from = transform.position;
            Vector2 step = _velocity * dt;
            float distance = step.magnitude;

            // Walls stop it.
            if (distance > 0f && _wallMask != 0
                && Physics2D.CircleCast(from, _settings.Radius * 0.5f, step / distance, distance, _wallMask).collider != null)
            {
                Die();
                return;
            }

            Vector2 to = from + step;
            transform.position = new Vector3(to.x, to.y, transform.position.z);
            CheckHits(to);
        }

        private void CheckHits(Vector2 at)
        {
            var filter = new ContactFilter2D();
            filter.NoFilter(); // every layer, triggers included (enemies are triggers)
            Physics2D.OverlapCircle(at, _settings.Radius, filter, Overlaps);

            for (int i = 0; i < Overlaps.Count; i++)
            {
                Collider2D other = Overlaps[i];
                if (other == null || other == _collider) continue;

                if (!_reflected)
                {
                    PlayerHealth player = other.GetComponentInParent<PlayerHealth>();
                    // Rolling (or freshly hit) players: fly straight through them.
                    if (player == null || player.IsDead || player.IsInvulnerable) continue;
                    player.TakeEnemyDamage(_settings.Damage);
                    Die();
                    return;
                }

                HordeEnemy enemy = other.GetComponentInParent<HordeEnemy>();
                if (enemy == null || !enemy.isActiveAndEnabled || enemy.CurrentHealth <= 0f) continue;
                enemy.ApplyKnockback(_velocity.normalized * 4f);
                enemy.TakeDamage(_settings.ReflectDamage); // last, since it can despawn the enemy
                Die();
                return;
            }
        }

        // ------------------------------------------------------------------ parry

        protected override void HandleParry(in AxeHit hit)
        {
            // Back the way the player aimed (falls back to straight away from them).
            Vector2 direction = hit.Facing.sqrMagnitude > 0.001f ? hit.Facing : hit.Direction;
            if (direction.sqrMagnitude < 0.001f) direction = -_velocity;
            _velocity = direction.normalized * (_settings.Speed * _settings.ReflectSpeedMultiplier);
            _dieAt = Time.time + _settings.Lifetime;
            _reflected = true;
            IsParryable = false; // can't be bounced back and forth by the same swing
        }

        private void Die() => Destroy(gameObject);

        // A soft white dot, made once, so projectiles work before there's art.
        private static Sprite DefaultSprite()
        {
            if (_defaultSprite != null) return _defaultSprite;
            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { filterMode = FilterMode.Bilinear };
            var pixels = new Color32[size * size];
            float r = size * 0.5f;
            for (int y = 0; y < size; y++)
            for (int x = 0; x < size; x++)
            {
                float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(r, r)) / r;
                float alpha = Mathf.Clamp01((1f - d) * 2f); // solid middle, soft edge
                pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
            }
            texture.SetPixels32(pixels);
            texture.Apply();
            _defaultSprite = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
            return _defaultSprite;
        }
    }
}
