using System;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>Shared, editable graphs for the three nozzles and their contact effects.</summary>
[CreateAssetMenu(menuName = "Fireline/Hose VFX Library")]
public sealed class HoseVfxLibrary : ScriptableObject
{
    public const string ResourcePath = "HoseVfx/HoseVfxLibrary";

    [SerializeField] private VisualEffectAsset stream;
    [SerializeField] private VisualEffectAsset mist;
    [SerializeField] private VisualEffectAsset burst;
    [SerializeField] private VisualEffectAsset splash;
    [SerializeField] private VisualEffectAsset steam;

    [Serializable]
    public sealed class StreamSettings
    {
        [Tooltip("Diameter of the continuous water core in world units. Independent of the damage hitbox.")]
        [Min(0.01f)] public float beamWidth = 0.22f;
        public Color beamColor = new Color(0.7f, 0.92f, 1f, 0.95f);
        [Tooltip("Speed of the advancing water front, in world units per second.")]
        [Min(0.1f)] public float extensionSpeed = 20f;
        [Tooltip("Seconds for the remaining beam to dissolve after releasing the trigger.")]
        [Range(0.02f, 0.5f)] public float releaseFade = 0.12f;
        [Tooltip("Visual downward acceleration of the continuous stream. Separate from detached-droplet gravity.")]
        [Min(0f)] public float beamGravity = 18f;
        [Tooltip("Maximum downward bend in world units. Gameplay also limits it to the muzzle's height above ground.")]
        [Min(0f)] public float maximumDrop = 0.75f;
        [Tooltip("Droplets shed along the length of the beam each second.")]
        [Range(0f, 600f)] public float dropletRate = 55f;
        [Min(0.005f)] public float dropletSize = 0.065f;
        [Range(0f, 0.9f)] public float sizeVariation = 0.35f;
        [Range(0.05f, 3f)] public float dropletLifetime = 0.6f;
        [Tooltip("Forward speed after a droplet separates from the beam.")]
        [Min(0f)] public float dropletSpeed = 2f;
        [Tooltip("Sideways speed of shed droplets.")]
        [Min(0f)] public float dropletSpread = 0.7f;
        [Tooltip("Initial downward speed along the tilted view's vertical axis.")]
        [Min(0f)] public float fallSpeed = 0.35f;
        [Tooltip("Downward acceleration of detached droplets, in world units per second squared.")]
        [Min(0f)] public float gravity = 4f;
        public Color dropletColor = new Color(0.52f, 0.82f, 1f, 0.8f);
    }

    [Serializable]
    public sealed class JetSettings
    {
        [Tooltip("Length of the single pointed shot in world units. Clamped to the available range.")]
        [Min(0.05f)] public float length = 1.8f;
        [Min(0.01f)] public float width = 0.45f;
        [Tooltip("Seconds for the spear tip to reach maximum range. Independent of the firing cooldown.")]
        [Range(0.02f, 1f)] public float travelTime = 0.12f;
        public Color color = new Color(0.68f, 0.91f, 1f, 1f);
        [Range(0.1f, 0.99f)] public float fadeStart = 0.85f;
    }

    [Serializable]
    public sealed class MistSettings
    {
        [Tooltip("Multipliers for the existing mist look. One preserves its current appearance.")]
        [Range(0f, 4f)] public float density = 1f;
        [Range(0.1f, 4f)] public float size = 1f;
        [Range(0.1f, 4f)] public float lifetime = 1f;
        [Tooltip("Visual cone width multiplier. Does not change the damage hitbox.")]
        [Range(0.1f, 4f)] public float spread = 1f;
    }

    [Serializable]
    public sealed class SplashSettings
    {
        [Range(0, 100)] public int streamCount = 10;
        [Range(0, 200)] public int jetCount = 28;
        [Min(0.02f)] public float streamInterval = 0.065f;
        [Min(0.005f)] public float size = 0.14f;
        [Range(0.05f, 3f)] public float lifetime = 0.45f;
        [Min(0f)] public float speed = 1f;
        [Min(0f)] public float spread = 4f;
        public Color color = new Color(0.52f, 0.82f, 1f, 0.85f);
    }

    [Serializable]
    public sealed class SteamSettings
    {
        [Min(0.005f)] public float size = 0.45f;
        [Range(0.05f, 5f)] public float lifetime = 1.3f;
        [Min(0f)] public float riseSpeed = 1f;
        [Min(0f)] public float spread = 0.5f;
        [Min(0.02f)] public float puffInterval = 0.1f;
        [Min(0f)] public float particlesPerWaterUnit = 2f;
        [Range(0, 50)] public int minimumCount = 3;
        [Range(0, 100)] public int maximumCount = 24;
        public Color color = new Color(0.86f, 0.91f, 0.95f, 0.32f);
    }

    [Header("Appearance — edit these settings while previewing")]
    [SerializeField] private StreamSettings streamAppearance = new StreamSettings();
    [SerializeField] private JetSettings jetAppearance = new JetSettings();
    [SerializeField] private MistSettings mistAppearance = new MistSettings();
    [SerializeField] private SplashSettings splashAppearance = new SplashSettings();
    [SerializeField] private SteamSettings steamAppearance = new SteamSettings();
    [SerializeField, HideInInspector] private int graphVersion;

    public VisualEffectAsset Stream => stream;
    public VisualEffectAsset Mist => mist;
    public VisualEffectAsset Burst => burst;
    public VisualEffectAsset Splash => splash;
    public VisualEffectAsset Steam => steam;
    public StreamSettings StreamAppearance => streamAppearance;
    public JetSettings JetAppearance => jetAppearance;
    public MistSettings MistAppearance => mistAppearance;
    public SplashSettings SplashAppearance => splashAppearance;
    public SteamSettings SteamAppearance => steamAppearance;
    public int GraphVersion => graphVersion;
    public bool IsComplete => stream != null && mist != null && burst != null && splash != null && steam != null;
}
