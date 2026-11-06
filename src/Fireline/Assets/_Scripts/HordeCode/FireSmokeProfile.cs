using UnityEngine;
using UnityEngine.VFX;

namespace Game.Runtime
{
    [CreateAssetMenu(menuName = "Fireline/Fire Smoke Profile")]
    public sealed class FireSmokeProfile : ScriptableObject
    {
        public const string ResourcePath = "FireVfx/FireSmokeProfile";
        [SerializeField] private VisualEffectAsset graph;
        [Tooltip("Smoke puffs per second at full fire strength.")]
        [Range(0f, 80f)] public float rate = 14f;
        [Min(0.05f)] public float lifetime = 2.8f;
        [Min(0.01f)] public float size = 0.8f;
        [Range(0f, 0.9f)] public float sizeVariation = 0.35f;
        [Min(0f)] public float riseSpeed = 0.75f;
        [Min(0f)] public float drift = 0.25f;
        [Tooltip("Emission radius relative to the fire's collider footprint. Standalone emitters use this as a world-space radius.")]
        [Min(0f)] public float radius = 0.65f;
        [Min(0f)] public float spawnHeight = 0.55f;
        [Range(1f, 5f)] public float growth = 2.4f;
        public Color color = new Color(0.27f, 0.29f, 0.32f, 0.58f);
        public HoseVfxLibrary.PixelationSettings pixelation = new HoseVfxLibrary.PixelationSettings
        {
            pixelSize = 6, colorSteps = 5, alphaCutoff = 0.02f
        };
        public VisualEffectAsset Graph => graph;
    }
}
