using UnityEngine;

namespace Game.Runtime
{
    // recently added knockback
    [CreateAssetMenu(fileName = "NewEnemy", menuName = "Fireline/Enemy Definition")]
    public class EnemyDefinition : ScriptableObject
    {
        [SerializeField] private string displayName = "Fire Swarmer";

        [Header("Stats")]
        [SerializeField, Min(0f)] private float moveSpeed = 2f;
        [SerializeField, Min(1f)] private float maxHealth = 10f;
        [SerializeField, Min(0f)] private float contactDamage = 10f;
        [Tooltip("0 = full knockback from hoses, 1 = can't be pushed at all (tanks, bosses).")]
        [SerializeField, Range(0f, 1f)] private float knockbackResistance = 0f;

        [Header("Looks")]
        [Tooltip("Each spawn picks one at random, so a single enemy type doesn't look copy-pasted. One entry is fine. Leave empty to keep the prefab's sprite.")]
        [SerializeField] private Sprite[] spriteVariants = new Sprite[0];
        [Tooltip("Multiplied with the sprite's colors. White = unchanged. Cheap way to make variants (e.g. a blue-hot elite).")]
        [SerializeField] private Color tint = Color.white;
        [Tooltip("Each spawn picks a random size in this range. Set both equal for a fixed size.")]
        [SerializeField, Min(0.01f)] private float minScale = 1f;
        [SerializeField, Min(0.01f)] private float maxScale = 1f;
        [Tooltip("Tick this if the artist drew this enemy facing LEFT, so sprite flipping still points it the right way.")]
        [SerializeField] private bool artFacesLeft;

        [Header("Animation (optional)")]
        [Tooltip("Swapped onto the prefab's Animator when spawned. Leave empty for static sprites.")]
        [SerializeField] private RuntimeAnimatorController animatorController;

        public string DisplayName => displayName;
        public float MoveSpeed => moveSpeed;
        public float MaxHealth => maxHealth;
        public float ContactDamage => contactDamage;
        public float KnockbackResistance => knockbackResistance;
        public Color Tint => tint;
        public bool ArtFacesLeft => artFacesLeft;
        public RuntimeAnimatorController AnimatorController => animatorController;
        
        public Sprite PickSprite()
        {
            if (spriteVariants == null || spriteVariants.Length == 0) return null;
            return spriteVariants[Random.Range(0, spriteVariants.Length)];
        }
        
        public float PickScale() => Random.Range(minScale, maxScale);

        private void OnValidate()
        {
            if (maxScale < minScale) maxScale = minScale;
        }
    }
}