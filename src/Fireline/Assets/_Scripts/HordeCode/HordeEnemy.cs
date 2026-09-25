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
        public float CurrentHealth { get; private set; }
        
        public Vector2 MoveDirection { get; internal set; }
        
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
            if (amount <= 0f || CurrentHealth <= 0f) return;

            CurrentHealth -= amount;
            if (CurrentHealth <= 0f)
                Despawn();
        }
        
        public void Despawn()
        {
            if (Pool != null) Pool.Release(this);
            else Destroy(gameObject);
        }
        
        private void ResetState()
        {
            MoveDirection = Vector2.zero;
            CurrentHealth = definition != null ? definition.MaxHealth : 1f;

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