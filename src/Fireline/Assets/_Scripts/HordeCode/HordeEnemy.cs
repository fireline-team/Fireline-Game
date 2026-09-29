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
        
        internal int ManagerIndex = -1;
        
        public IObjectPool<HordeEnemy> Pool { get; set; }

        private void Awake()
        {
            CachedTransform = transform;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
            if (animator == null)
                animator = GetComponentInChildren<Animator>();
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
            if (health != null && health.TakeDamage(amount))
                Despawn();
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