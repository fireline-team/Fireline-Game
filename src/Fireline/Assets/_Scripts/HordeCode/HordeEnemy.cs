using Fireline.Shared.Infrastructure;
using UnityEngine;
using UnityEngine.Pool;

namespace Game.Runtime
{
    public class HordeEnemy : MonoBehaviour
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
        public float ContactDamage => definition != null ? definition.ContactDamage : 10f;
        
        public Vector2 MoveDirection { get; internal set; }

        /// <summary>Extra velocity from hose hits. HordeManager adds it to movement and makes it wear off.</summary>
        public Vector2 KnockbackVelocity { get; internal set; }

        /// <summary>Multiplier on walking speed: 1 normally, lower while slowed.</summary>
        public float SpeedMultiplier => Time.time < _slowUntil ? _slowMultiplier : 1f;

        private float _slowMultiplier = 1f;
        private float _slowUntil;

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
            MoveDirection = Vector2.zero;
            KnockbackVelocity = Vector2.zero;
            _slowMultiplier = 1f;
            _slowUntil = 0f;
            if (IsFlashing) EndFlash();
            _nextFlashTime = 0f;
            float maximum = definition != null ? definition.MaxHealth : 1f;
            if (health == null) health = new HealthPool(maximum);
            else health.Reset(maximum);

            if (definition == null) return;
            
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
        }
    }
}