using Fireline.Shared;
using UnityEngine;

namespace Fireline.Missions
{
    /// <summary>One appearance per survivor, with five directional poses from the same civilian sheet.</summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public sealed class CivilianAppearance : MonoBehaviour
    {
        [System.Serializable]
        public sealed class Variant
        {
            public string name;
            [Tooltip("Front, front-side, side, back-side, back. Include hands in each pose.")]
            public Sprite[] poses = new Sprite[5];
        }

        [SerializeField] private Variant[] variants = new Variant[0];
        [Tooltip("Choose independently when instantiated. Repeats are allowed.")]
        [SerializeField] private bool randomizeOnSpawn = true;
        [Tooltip("Preview/fixed appearance index. Disable Randomize On Spawn to keep this choice.")]
        [SerializeField, Min(0)] private int selectedVariant;
        private SpriteRenderer spriteRenderer;
        private Vector3 lastPosition;
        private Facing8 facing = Facing8.S;
        public int SelectedVariant => selectedVariant;
        public int VariantCount => variants.Length;
        public SpriteRenderer Renderer => spriteRenderer != null ? spriteRenderer : GetComponent<SpriteRenderer>();

        private void Awake()
        {
            spriteRenderer = GetComponent<SpriteRenderer>();
            lastPosition = transform.parent != null ? transform.parent.position : transform.position;
            SetVariant(randomizeOnSpawn && variants.Length > 0 ? Random.Range(0, variants.Length) : selectedVariant);
        }

        public void SetVariant(int index)
        {
            if (index < 0 || index >= variants.Length) return;
            selectedVariant = index;
            Apply();
        }

        private void LateUpdate()
        {
            Vector3 position = transform.parent != null ? transform.parent.position : transform.position;
            Vector3 delta = position - lastPosition;
            lastPosition = position;
            if (delta.sqrMagnitude > 0.000001f) facing = FacingDirection.FromVector(delta.x, delta.y);
            Apply();
        }

        private void Apply()
        {
            if (variants.Length == 0) return;
            selectedVariant = Mathf.Clamp(selectedVariant, 0, variants.Length - 1);
            var poses = variants[selectedVariant]?.poses;
            var (pose, flip) = FacingDirection.ToPose(facing, true);
            int index = (int)pose;
            if (poses == null || poses.Length <= index || poses[index] == null) return;
            Renderer.sprite = poses[index];
            Renderer.flipX = flip;
        }

#if UNITY_EDITOR
        private void OnValidate() => Apply();
#endif
    }
}
