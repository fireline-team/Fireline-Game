using UnityEngine;
using UnityEngine.VFX;

/// <summary>Shader bindings for independently configured water and fire-smoke filters.</summary>
public static class VfxPixelation
{
    private static readonly int Size = Shader.PropertyToID("PixelSize");
    private static readonly int Strength = Shader.PropertyToID("PixelStrength");
    private static readonly int Aspect = Shader.PropertyToID("PixelAspect");
    private static readonly int Offset = Shader.PropertyToID("PixelOffset");
    private static readonly int Colors = Shader.PropertyToID("ColorSteps");
    private static readonly int Cutoff = Shader.PropertyToID("AlphaCutoff");

    public static void Apply(VisualEffect effect, HoseVfxLibrary.PixelationSettings settings)
    {
        if (effect == null || !effect.HasFloat(Size)) return;
        effect.SetFloat(Size, settings.enabled ? Mathf.Clamp(settings.pixelSize, 1, 32) : 1f);
        if (!effect.HasFloat(Strength)) return; // Version-four/custom graphs retain basic pixelation.
        effect.SetFloat(Strength, settings.enabled ? Mathf.Clamp01(settings.strength) : 0f);
        effect.SetVector2(Aspect, new Vector2(Mathf.Clamp(settings.pixelAspect.x, 0.25f, 4f), Mathf.Clamp(settings.pixelAspect.y, 0.25f, 4f)));
        effect.SetVector2(Offset, settings.gridOffset);
        effect.SetFloat(Colors, settings.enabled && settings.colorSteps >= 2 ? Mathf.Clamp(settings.colorSteps, 2, 32) : 0f);
        effect.SetFloat(Cutoff, settings.enabled ? Mathf.Clamp(settings.alphaCutoff, 0f, 0.95f) : 0f);
    }
}
