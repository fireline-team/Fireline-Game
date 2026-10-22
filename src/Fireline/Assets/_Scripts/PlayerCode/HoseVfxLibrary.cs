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

    public VisualEffectAsset Stream => stream;
    public VisualEffectAsset Mist => mist;
    public VisualEffectAsset Burst => burst;
    public VisualEffectAsset Splash => splash;
    public VisualEffectAsset Steam => steam;
    public bool IsComplete => stream != null && mist != null && burst != null && splash != null && steam != null;
}
