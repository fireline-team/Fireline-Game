using System.Collections.Generic;
using Fireline.Shared.Infrastructure;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Runtime
{
    /// <summary>
    /// The shared enemy body. What kind of enemy it is comes entirely from its EnemyDefinition:
    /// stats, looks, and abilities (shooting, summoning...). HordeManager moves it and ticks it.
    /// </summary>
    public class HordeEnemy : MonoBehaviour, IAxeHittable
    {
        [SerializeField] private EnemyDefinition definition;
        [Tooltip("Optional. If set, the manager flips it to face left/right. Leave empty to auto-find one on this object or its children.")]
        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Optional. Only needed if enemy definitions use animator controllers. Auto-found if left empty.")]
        [SerializeField] private Animator animator;

        [Header("Hit Flash")]
        [Tooltip("Material that draws the sprite as a solid color (HitFlash, using the Fireline/SpriteFlash shader). Leave empty to turn flashing off.")]
        [SerializeField] private Material hitFlashMaterial;
        [Tooltip("How long each flash lasts, in seconds.")]
        [SerializeField, Min(0.01f)] private float flashDuration = 0.06f;
        [Tooltip("Shortest time between flashes. Keeps enemies in a steady stream blinking instead of staying solid white.")]
        [SerializeField, Min(0.01f)] private float flashInterval = 0.12f;

        public EnemyDefinition Definition => definition;
        public Transform CachedTransform { get; private set; }
        public SpriteRenderer Sprite => spriteRenderer;

        public float MoveSpeed => definition != null ? definition.MoveSpeed : 0f;
        public bool ArtFacesLeft => definition != null && definition.ArtFacesLeft;
        private HealthPool health;
        public float CurrentHealth => health != null ? health.Current : 0f;
        /// <summary>Damage on touch. None while still climbing out of the ground.</summary>
        public float ContactDamage => IsRising ? 0f : definition != null ? definition.ContactDamage : 10f;
        /// <summary>How close it walks to players before stopping (0 = all the way, for melee).</summary>
        public float KeepDistance => definition != null ? definition.KeepDistance : 0f;
        /// <summary>Still climbing out of the ground after spawning.</summary>
        public bool IsRising => _rise != null && _rise.IsRising;
        /// <summary>Told to stand still for a moment (aiming, summoning).</summary>
        public bool IsHeld => Time.time < _holdUntil;
        /// <summary>Goes up by one every spawn, so abilities can tell a pooled enemy's new life from its old one.</summary>
        public int Generation { get; private set; }
        
        public Vector2 MoveDirection { get; internal set; }

        /// <summary>Extra velocity from hose hits. HordeManager adds it to movement and makes it wear off.</summary>
        public Vector2 KnockbackVelocity { get; internal set; }

        /// <summary>Multiplier on walking speed: 1 normally, lower while slowed.</summary>
        public float SpeedMultiplier => Time.time < _slowUntil ? _slowMultiplier : 1f;

        private float _slowMultiplier = 1f;
        private float _slowUntil;
        private float _holdUntil;

        private readonly List<EnemyAbilityRuntime> _abilities = new List<EnemyAbilityRuntime>();
        private EnemyDefinition _abilitiesBuiltFor;
        private SpawnRise _rise;

        private Material _normalMaterial;
        private float _flashEndTime;
        private float _nextFlashTime;

        /// <summary>True while the hit flash is showing. HordeManager ends it on time.</summary>
        public bool IsFlashing { get; private set; }
        
        internal int ManagerIndex = -1;
        
        public IObjectPool<HordeEnemy> Pool { get; set; }

        private void Awake()
        {
            CachedTransform = transform;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
            if (spriteRenderer != null)
                _normalMaterial = spriteRenderer.sharedMaterial;
            // Only animate a child sprite: the root's scale belongs to the definition's size roll.
            _rise = new SpawnRise(spriteRenderer != null && spriteRenderer.transform != transform
                ? spriteRenderer.transform : null);
            if (definition == null)
                Debug.LogWarning($"{name} has no EnemyDefinition assigned, so it won't move.", this);
        }

        private void OnEnable()
        {
            ResetState();
            if (HordeManager.Instance != null)
                HordeManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (HordeManager.Instance != null)
                HordeManager.Instance.Unregister(this);
            for (int i = 0; i < _abilities.Count; i++) _abilities[i].OnDespawned();
            _rise?.Finish(); // pooled: don't come back half-grown
        }

        /// <summary>Stand still for this long (abilities use it to aim or channel).</summary>
        public void Hold(float seconds) => _holdUntil = Mathf.Max(_holdUntil, Time.time + seconds);

        /// <summary>Called by HordeManager every frame: finishes rising, then runs abilities.</summary>
        internal void TickBehaviors(float deltaTime, IHordeTargets targets)
        {
            if (IsRising)
            {
                _rise.Tick(deltaTime);
                return;
            }
            for (int i = 0; i < _abilities.Count; i++)
            {
                _abilities[i].Tick(deltaTime, targets);
                if (!isActiveAndEnabled) return; // an ability can end this enemy's life
            }
        }
        
        public void SetDefinition(EnemyDefinition newDefinition)
        {
            definition = newDefinition;
            ResetState();
        }
        
        public void TakeDamage(float amount)
        {
            if (health == null) return;

            float before = health.Current;
            if (health.TakeDamage(amount))
            {
                Despawn();
                return;
            }
            if (health.Current < before)
                StartFlash();
        }

        // Swaps to the flash material. Swapping (instead of a per-renderer property) keeps
        // sprite batching intact for every enemy that isn't flashing.
        private void StartFlash()
        {
            if (hitFlashMaterial == null || spriteRenderer == null) return;

            float now = Time.time;
            if (now < _nextFlashTime) return;
            _nextFlashTime = now + flashInterval;
            _flashEndTime = now + flashDuration;

            if (!IsFlashing)
            {
                spriteRenderer.sharedMaterial = hitFlashMaterial;
                IsFlashing = true;
            }
        }

        /// <summary>Called by HordeManager each frame for flashing enemies.</summary>
        internal void UpdateFlash(float now)
        {
            if (IsFlashing && now >= _flashEndTime)
                EndFlash();
        }

        private void EndFlash()
        {
            IsFlashing = false;
            if (spriteRenderer != null)
                spriteRenderer.sharedMaterial = _normalMaterial;
        }
        
        public void ApplyKnockback(Vector2 velocity)
        {
            if (CurrentHealth <= 0f) return;
            float resistance = definition != null ? definition.KnockbackResistance : 0f;
            KnockbackVelocity += velocity * (1f - resistance);
        }
        
        /// <summary>Axe hit: shoved away from the player, then damaged.</summary>
        public AxeHitResult OnAxeHit(in AxeHit hit)
        {
            if (!isActiveAndEnabled || CurrentHealth <= 0f) return AxeHitResult.Ignored;
            ApplyKnockback(hit.Direction * hit.Knockback);
            TakeDamage(hit.Damage); // last, since it can despawn the enemy
            return AxeHitResult.Hit;
        }

        public void ApplySlow(float multiplier, float duration)
        {
            if (CurrentHealth <= 0f) return;
            multiplier = Mathf.Clamp01(multiplier);
            float now = Time.time;

            _slowMultiplier = now < _slowUntil ? Mathf.Min(_slowMultiplier, multiplier) : multiplier;
            _slowUntil = Mathf.Max(_slowUntil, now + duration);
        }

        public void Despawn()
        {
            if (!gameObject.activeSelf) return;
            if (Pool != null) Pool.Release(this);
            else
            {
                gameObject.SetActive(false);
                Destroy(gameObject);
            }
        }
        
        private void ResetState()
        {
            Generation++;
            _holdUntil = 0f;
            MoveDirection = Vector2.zero;
            KnockbackVelocity = Vector2.zero;
            _slowMultiplier = 1f;
            _slowUntil = 0f;
            if (IsFlashing) EndFlash();
            _nextFlashTime = 0f;
            float maximum = definition != null ? definition.MaxHealth : 1f;
            if (health == null) health = new HealthPool(maximum);
            else health.Reset(maximum);

            if (definition == null)
            {
                _abilities.Clear();
                _abilitiesBuiltFor = null;
                _rise?.Begin(null, 0f);
                return;
            }
            
            if (spriteRenderer != null)
            {
                Sprite variant = definition.PickSprite();
                if (variant != null) spriteRenderer.sprite = variant;
                spriteRenderer.color = definition.Tint;
                spriteRenderer.flipX = false;
            }

            float scale = definition.PickScale();
            transform.localScale = new Vector3(scale, scale, 1f);
            
            if (animator != null && definition.AnimatorController != null
                && animator.runtimeAnimatorController != definition.AnimatorController)
            {
                animator.runtimeAnimatorController = definition.AnimatorController;
            }

            _rise?.Begin(spriteRenderer, definition.RiseDuration);
            BuildAbilities();
            for (int i = 0; i < _abilities.Count; i++) _abilities[i].OnSpawned();
        }

        // Runtimes are made once per definition and reused while the pool recycles this enemy
        // as the same type, so respawning doesn't allocate.
        private void BuildAbilities()
        {
            if (_abilitiesBuiltFor == definition) return;
            _abilities.Clear();
            _abilitiesBuiltFor = definition;
            var list = definition.Abilities;
            if (list == null) return;
            for (int i = 0; i < list.Count; i++)
            {
                if (list[i] == null) continue;
                EnemyAbilityRuntime runtime = list[i].CreateRuntime(this);
                if (runtime != null) _abilities.Add(runtime);
            }
        }
    }
}