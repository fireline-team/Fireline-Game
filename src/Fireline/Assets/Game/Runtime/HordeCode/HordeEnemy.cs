using UnityEngine;
using UnityEngine.Pool;

namespace Game.Runtime
{
    public class HordeEnemy : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        
        [SerializeField] private SpriteRenderer spriteRenderer;

        public float MoveSpeed => moveSpeed;
        public Transform CachedTransform { get; private set; }
        public SpriteRenderer Sprite => spriteRenderer;
        public Vector2 MoveDirection { get; internal set; }
        
        internal int ManagerIndex = -1;
        
        public IObjectPool<HordeEnemy> Pool { get; set; }

        private void Awake()
        {
            CachedTransform = transform;
            if (spriteRenderer == null)
                spriteRenderer = GetComponentInChildren<SpriteRenderer>();
        }

        private void OnEnable()
        {
            MoveDirection = Vector2.zero;
            if (HordeManager.Instance != null)
                HordeManager.Instance.Register(this);
        }

        private void OnDisable()
        {
            if (HordeManager.Instance != null)
                HordeManager.Instance.Unregister(this);
        }
        
        public void Despawn()
        {
            if (Pool != null) Pool.Release(this);
            else Destroy(gameObject);
        }
    }
}
