using UnityEngine;
using UnityEngine.VFX;

namespace Game.Runtime
{
    /// <summary>Reusable ambient smoke: follows a parent fire's state, or emits standalone.</summary>
    [RequireComponent(typeof(VisualEffect))]
    public sealed class FireSmokeVisual : MonoBehaviour
    {
        [SerializeField] private FireSmokeProfile profile;
        private FireZone source;
        private BoxCollider2D footprint;
        private Renderer fireRenderer;
        private VisualEffect effect;
        private bool initialized;
        public bool IsEmitting { get; private set; }

        private static readonly int Origin = Shader.PropertyToID("Origin");
        private static readonly int Up = Shader.PropertyToID("Up");
        private static readonly int Rate = Shader.PropertyToID("Rate");
        private static readonly int Lifetime = Shader.PropertyToID("Lifetime");
        private static readonly int Size = Shader.PropertyToID("ParticleSize");
        private static readonly int Radius = Shader.PropertyToID("Radius");
        private static readonly int Speed = Shader.PropertyToID("Speed");
        private static readonly int Spread = Shader.PropertyToID("Spread");
        private static readonly int Variation = Shader.PropertyToID("SizeVariation");
        private static readonly int Growth = Shader.PropertyToID("Growth");
        private static readonly int ColorId = Shader.PropertyToID("EffectColor");

        public void Configure(FireZone fire, FireSmokeProfile appearance, Renderer sortingSource)
        {
            source = fire;
            profile = appearance;
            fireRenderer = sortingSource;
            footprint = fire != null ? fire.GetComponent<BoxCollider2D>() : null;
        }

        private void Start()
        {
            if (source == null) source = GetComponentInParent<FireZone>();
            if (footprint == null && source != null) footprint = source.GetComponent<BoxCollider2D>();
            if (profile == null) profile = Resources.Load<FireSmokeProfile>(FireSmokeProfile.ResourcePath);
            effect = GetComponent<VisualEffect>();
            if (profile == null || profile.Graph == null || !SystemInfo.supportsComputeShaders)
            {
                enabled = false;
                return;
            }
            effect.initialEventName = "SmokeIdle";
            effect.visualEffectAsset = profile.Graph;
            var renderer = GetComponent<VFXRenderer>();
            if (fireRenderer != null)
            {
                renderer.sortingLayerID = fireRenderer.sortingLayerID;
                renderer.sortingOrder = fireRenderer.sortingOrder + 1;
            }
            initialized = true;
            Refresh();
        }

        private void Update()
        {
            if (initialized) Refresh();
        }

        private void Refresh()
        {
            bool burning = source == null || source.isActiveAndEnabled && source.IsBurning;
            float intensity = source != null ? source.Intensity : 1f;
            effect.SetVector3(Origin, transform.position + TiltedView.Up * profile.spawnHeight);
            effect.SetVector3(Up, TiltedView.Up);
            float radius = footprint != null ? Mathf.Max(footprint.bounds.extents.x, footprint.bounds.extents.y) * profile.radius : profile.radius;
            effect.SetFloat(Radius, Mathf.Max(0f, radius));
            effect.SetFloat(Lifetime, Mathf.Max(0.05f, profile.lifetime));
            effect.SetFloat(Size, Mathf.Max(0.01f, profile.size));
            effect.SetFloat(Variation, Mathf.Clamp(profile.sizeVariation, 0f, 0.9f));
            effect.SetFloat(Speed, Mathf.Max(0f, profile.riseSpeed));
            effect.SetFloat(Spread, Mathf.Max(0f, profile.drift));
            effect.SetFloat(Growth, Mathf.Clamp(profile.growth, 1f, 5f));
            effect.SetVector4(ColorId, profile.color);
            VfxPixelation.Apply(effect, profile.pixelation);
            float rate = burning ? Mathf.Clamp(profile.rate, 0f, 80f) * Mathf.Lerp(0.25f, 1f, intensity) : 0f;
            effect.SetFloat(Rate, rate);
            if (IsEmitting == (rate > 0f)) return;
            IsEmitting = rate > 0f;
            if (IsEmitting) effect.Play();
            else effect.Stop(); // Existing world-space puffs finish their own lifetimes.
        }

        private void OnDisable()
        {
            IsEmitting = false;
            if (effect == null || !initialized) return;
            effect.SetFloat(Rate, 0f);
            effect.Stop();
        }
    }
}
