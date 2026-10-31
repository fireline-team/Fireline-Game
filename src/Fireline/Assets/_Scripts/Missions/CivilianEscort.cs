using System.Collections.Generic;
using Fireline.Shared.Horde;
using Fireline.Shared.Infrastructure;
using Game.Runtime;
using UnityEngine;
using PlaneVector = System.Numerics.Vector2;

namespace Fireline.Missions
{
    /// <summary>A released survivor. Only entering evac counts as saved.</summary>
    [RequireComponent(typeof(Rigidbody2D), typeof(CircleCollider2D))]
    public sealed class CivilianEscort : MonoBehaviour
    {
        [SerializeField, Min(1)] private float maxHealth = 50f;
        [SerializeField, Range(0.1f, 0.95f)] private float playerSpeedFraction = 0.8f;
        [SerializeField, Min(0.1f)] private float followingDistance = 0.8f;
        [SerializeField, Min(0.05f)] private float contactDamageCooldown = 0.75f;
        private const float Radius = 0.22f;
        private MissionObjectiveSite site;
        private FireRescueMission mission;
        private Rigidbody2D body;
        private HealthPool health;
        private SpriteRenderer visual;
        private Sprite fallbackSprite;
        private float nextDamageTime;
        private float nextPathTime;
        private FlowField route;
        private readonly List<PlaneVector> targetPositions = new List<PlaneVector>(1);
        private readonly List<Collider2D> contacts = new List<Collider2D>();
        private readonly List<RaycastHit2D> obstacleHits = new List<RaycastHit2D>();
        public PlayerHealth FollowTarget { get; private set; }
        public bool IsResolved { get; private set; }
        public bool IsSaved { get; private set; }
        public float CurrentHealth => health != null ? health.Current : 0;
        public float MaxHealth => maxHealth;
        public float MoveSpeed => (FollowTarget != null && FollowTarget.TryGetComponent<PlayerController>(out var player)
            ? player.MoveSpeed : 5f) * playerSpeedFraction;

        private void Awake()
        {
            body = GetComponent<Rigidbody2D>();
            body.bodyType = RigidbodyType2D.Kinematic;
            body.gravityScale = 0;
            body.constraints = RigidbodyConstraints2D.FreezeRotation;
            body.interpolation = RigidbodyInterpolation2D.Interpolate;
            var collider = GetComponent<CircleCollider2D>();
            collider.radius = Radius;
            collider.isTrigger = true;
            health = new HealthPool(maxHealth);
        }

        public void Initialize(MissionObjectiveSite owner, FireRescueMission rescueMission, Sprite sprite)
        {
            site = owner;
            mission = rescueMission;
            var art = new GameObject("Civilian body");
            art.transform.SetParent(transform, false);
            // Player body is 1.25; this is 70% of that visual size. Physics stays on XY.
            art.transform.localScale = Vector3.one * 0.875f;
            visual = art.AddComponent<SpriteRenderer>();
            if (sprite == null)
            {
                foreach (var player in PlayerHealth.ActivePlayers)
                {
                    var playerArt = player.transform.Find("PlayerSprite");
                    if (playerArt != null && playerArt.TryGetComponent<SpriteRenderer>(out var renderer))
                    { sprite = renderer.sprite; break; }
                }
            }
            if (sprite == null)
            {
                fallbackSprite = Sprite.Create(Texture2D.whiteTexture, new Rect(0, 0, 2, 2), new Vector2(0.5f, 0), 2);
                sprite = fallbackSprite;
            }
            visual.sprite = sprite;
            visual.color = new Color(0.7f, 0.9f, 1f);
            art.AddComponent<StandUpright>();
        }

        private void FixedUpdate()
        {
            body.linearVelocity = Vector2.zero;
            if (IsResolved || mission == null || !mission.isActiveAndEnabled
                || mission.State == FireRescueMission.Phase.Complete) return;
            ApplyContactDamage();
            if (IsResolved) return;
            if (mission.ShowEvacuationGuidance && mission.Spawn != null
                && Vector2.Distance(body.position, mission.Spawn.transform.position) <= mission.EvacuationRadius)
            {
                Resolve(true);
                return;
            }
            PlayerHealth closest = null;
            float distance = float.PositiveInfinity;
            foreach (var player in PlayerHealth.ActivePlayers)
            {
                if (player == null || player.IsDowned || player.gameObject.scene != gameObject.scene) continue;
                float candidate = Vector2.Distance(body.position, player.transform.position);
                if (candidate < distance) { closest = player; distance = candidate; }
            }
            if (closest != FollowTarget) nextPathTime = 0;
            FollowTarget = closest;
            if (closest == null || distance <= followingDistance) return;
            Vector2 target = closest.transform.position;
            Vector2 direction = DirectionTo(target);
            float travel = Mathf.Min(MoveSpeed * Time.fixedDeltaTime, distance - followingDistance);
            if (direction.sqrMagnitude > 0 && travel > 0)
            {
                // Never walk through a wall, even without a nav grid or when its bake is stale.
                float clearance = ObstacleDistance(body.position, direction, travel + 0.02f);
                travel = Mathf.Min(travel, Mathf.Max(0, clearance - 0.02f));
                body.MovePosition(body.position + direction * travel);
                if (visual != null && Mathf.Abs(direction.x) > 0.05f) visual.flipX = direction.x < 0;
            }
        }

        private float ObstacleDistance(Vector2 from, Vector2 direction, float distance)
        {
            var filter = new ContactFilter2D { useTriggers = false };
            filter.SetLayerMask(LayerMask.GetMask(Structure.ObstacleLayer));
            Physics2D.CircleCast(from, Radius, direction, filter, obstacleHits, distance);
            float nearest = distance;
            foreach (var hit in obstacleHits) nearest = Mathf.Min(nearest, hit.distance);
            return nearest;
        }

        private Vector2 DirectionTo(Vector2 target)
        {
            Vector2 delta = target - body.position;
            if (ObstacleDistance(body.position, delta.normalized, delta.magnitude) >= delta.magnitude)
                return delta.normalized;
            // Bake a private route area: the existing horde grid may not cover outer rescue sites.
            // Include both endpoints with room to detour around buildings, and reuse until we leave it.
            var from = new PlaneVector(body.position.x, body.position.y);
            var to = new PlaneVector(target.x, target.y);
            if (route == null || !route.TryWorldToCell(from, out _, out _) || !route.TryWorldToCell(to, out _, out _))
            {
                const float cellSize = 0.5f;
                Vector2 min = Vector2.Min(body.position, target) - Vector2.one * 12;
                Vector2 max = Vector2.Max(body.position, target) + Vector2.one * 12;
                route = new FlowField(Mathf.CeilToInt((max.x - min.x) / cellSize),
                    Mathf.CeilToInt((max.y - min.y) / cellSize), cellSize, new PlaneVector(min.x, min.y));
                var filter = new ContactFilter2D { useTriggers = false };
                filter.SetLayerMask(LayerMask.GetMask(Structure.ObstacleLayer));
                for (int y = 0; y < route.Height; y++)
                    for (int x = 0; x < route.Width; x++)
                    {
                        var center = route.CellCenter(x, y);
                        route.SetBlocked(x, y, Physics2D.OverlapBox(new Vector2(center.X, center.Y),
                            Vector2.one * (cellSize + Radius * 2), 0, filter, contacts) > 0);
                    }
                nextPathTime = 0;
            }
            if (Time.time >= nextPathTime)
            {
                targetPositions.Clear();
                targetPositions.Add(new PlaneVector(target.x, target.y));
                route.Build(targetPositions);
                nextPathTime = Time.time + 0.5f;
            }
            var sample = route.Sample(new PlaneVector(body.position.x, body.position.y), out var heading, out _);
            return sample == FlowSample.Direct ? delta.normalized
                : sample == FlowSample.NoPath ? Vector2.zero : new Vector2(heading.X, heading.Y).normalized;
        }

        private void ApplyContactDamage()
        {
            if (Time.time < nextDamageTime) return;
            Physics2D.OverlapCircle(body.position, Radius, new ContactFilter2D { useTriggers = true }, contacts);
            float damage = 0;
            foreach (var contact in contacts)
            {
                var fire = contact.GetComponentInParent<FireZone>();
                if (fire != null && fire.isActiveAndEnabled) damage = Mathf.Max(damage, fire.ContactDamage);
                var enemy = contact.GetComponentInParent<HordeEnemy>();
                if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0)
                    damage = Mathf.Max(damage, enemy.ContactDamage);
            }
            if (damage <= 0) return;
            nextDamageTime = Time.time + contactDamageCooldown;
            TakeDamage(damage);
        }

        public void TakeDamage(float amount)
        {
            if (IsResolved || health == null) return;
            if (health.TakeDamage(amount)) Resolve(false);
        }

        private void Resolve(bool saved)
        {
            if (IsResolved) return;
            IsResolved = true;
            IsSaved = saved;
            FollowTarget = null;
            body.simulated = false;
            site.ResolveCivilian(saved);
            if (saved) gameObject.SetActive(false);
            else if (visual != null)
            {
                visual.color = new Color(0.6f, 0.18f, 0.18f);
                visual.transform.Rotate(0, 0, 90, Space.Self);
            }
        }

        private void LateUpdate()
        {
            if (IsResolved || visual == null) return;
            float alpha = Time.time < nextDamageTime && Mathf.FloorToInt(Time.time * 12) % 2 == 0 ? 0.35f : 1;
            visual.color = new Color(0.7f, 0.9f, 1f, alpha);
        }

        private void OnDestroy() { if (fallbackSprite != null) Destroy(fallbackSprite); }
    }
}
