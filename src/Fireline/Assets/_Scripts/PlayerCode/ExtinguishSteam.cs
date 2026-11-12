using UnityEngine;
using UnityEngine.VFX;

/// <summary>One completion puff using the same graph and appearance as hose-contact steam.</summary>
public sealed class ExtinguishSteam : MonoBehaviour
{
    private HoseVfxLibrary library;
    private VisualEffect effect;
    private float finishesAt;
    public bool IsPlaying => effect != null && Time.time < finishesAt;

    public static ExtinguishSteam Create(Transform parent, Vector3 position, Renderer sortingReference)
    {
        var go = new GameObject("Extinguish steam");
        go.transform.SetParent(parent, false);
        go.transform.position = position;
        var steam = go.AddComponent<ExtinguishSteam>();
        steam.library = Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath);
        if (steam.library == null || steam.library.Steam == null) return steam;
        steam.effect = go.AddComponent<VisualEffect>();
        steam.effect.initialEventName = "HoseIdle";
        steam.effect.visualEffectAsset = steam.library.Steam;
        if (sortingReference != null)
        {
            var renderer = go.GetComponent<VFXRenderer>();
            renderer.sortingLayerID = sortingReference.sortingLayerID;
            renderer.sortingOrder = sortingReference.sortingOrder + 1;
        }
        return steam;
    }

    public void Play()
    {
        if (effect == null) return;
        Clear();
        var settings = library.SteamAppearance;
        effect.SetVector3("Origin", transform.position);
        effect.SetVector3("Direction", TiltedView.Up);
        effect.SetVector3("Up", TiltedView.Up);
        effect.SetFloat("ParticleSize", Mathf.Max(0.005f, settings.size));
        effect.SetFloat("Lifetime", Mathf.Max(0.05f, settings.lifetime));
        effect.SetVector4("EffectColor", settings.color);
        effect.SetFloat("Speed", Mathf.Max(0f, settings.riseSpeed));
        effect.SetFloat("Spread", Mathf.Max(0f, settings.spread));
        effect.SetFloat("Count", Mathf.Max(0, settings.maximumCount));
        VfxPixelation.Apply(effect, settings.pixelation);
        // The shared graph varies particle lifetime from 80% to 120%.
        finishesAt = Time.time + Mathf.Max(0.05f, settings.lifetime) * 1.2f;
        effect.Play(); // The graph has a single-burst spawner, not sustained emission.
    }

    public void Clear()
    {
        finishesAt = 0f;
        if (effect == null) return;
        effect.Reinit(); // HoseIdle prevents a new burst when resetting existing particles.
        effect.Stop();
    }

    private void Update()
    {
        if (effect != null) VfxPixelation.Apply(effect, library.SteamAppearance.pixelation);
    }

    private void OnDisable() => Clear();
}
