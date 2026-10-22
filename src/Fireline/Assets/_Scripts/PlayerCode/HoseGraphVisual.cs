using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

/// <summary>Presentation only. The hose's existing Physics2D queries own all gameplay hits.</summary>
public sealed class HoseGraphVisual : MonoBehaviour
{
    private static readonly int OriginId = Shader.PropertyToID("Origin");
    private static readonly int DirectionId = Shader.PropertyToID("Direction");
    private static readonly int UpId = Shader.PropertyToID("Up");
    private static readonly int RangeId = Shader.PropertyToID("Range");
    private static readonly int WidthId = Shader.PropertyToID("Width");
    private static readonly int LifetimeId = Shader.PropertyToID("Lifetime");
    private static readonly int SizeId = Shader.PropertyToID("ParticleSize");
    private static readonly int RateId = Shader.PropertyToID("Rate");
    private static readonly int CountId = Shader.PropertyToID("Count");

    private struct Impact
    {
        public Vector3 Position;
        public Vector3 Normal;
        public float Time;
    }

    private sealed class SteamContact
    {
        public Object Fire;
        public VisualEffect Effect;
        public float LastWaterTime;
        public float NextPuffTime;
    }

    private readonly SteamContact[] _steamContacts = new SteamContact[8];
    private readonly Queue<Impact> _pendingImpacts = new Queue<Impact>(8);
    private HoseVfxLibrary _library;
    private VisualEffect _water;
    private VisualEffect _splash;
    private NozzleType _mode;
    private string _sortingLayer;
    private int _sortingOrder;
    private float _nextSplashTime;

    public bool IsEmitting { get; private set; }

    public void Initialize(HoseVfxLibrary library, NozzleType mode, string sortingLayer, int sortingOrder)
    {
        _library = library;
        _mode = mode;
        _sortingLayer = sortingLayer;
        _sortingOrder = sortingOrder;
        VisualEffectAsset asset = mode == NozzleType.Mist ? library.Mist
            : mode == NozzleType.Jet ? library.Burst : library.Stream;
        _water = CreateEffect(asset, "Water");
        _splash = CreateEffect(library.Splash, "Impact splash");
    }

    public void SetSpraying(bool spraying, Vector3 origin, Vector2 direction,
        float range, float width, float lifetime, float rate, float size)
    {
        if (spraying)
        {
            ConfigureWater(origin, direction, range, width, lifetime, size);
            _water.SetFloat(RateId, rate * (_mode == NozzleType.Mist ? 9f : 6f));
        }
        if (IsEmitting == spraying) return;
        IsEmitting = spraying;
        if (spraying) _water.Play();
        else _water.Stop();
    }

    public void Pulse(Vector3 origin, Vector2 direction, float range, float width,
        float lifetime, float size, int count, bool hit, Vector3 hitPoint, Vector3 hitNormal)
    {
        float travelTime = Mathf.Min(lifetime, 0.09f);
        ConfigureWater(origin, direction, range, width, travelTime, size);
        _water.SetFloat(CountId, Mathf.Max(1, count) * 4f);
        _water.Play();
        if (!hit) return;
        // Keep a fired shot's impact even if the trigger is released or the nozzle is switched.
        if (_pendingImpacts.Count == 8) EmitSplash(_pendingImpacts.Dequeue(), 28f);
        _pendingImpacts.Enqueue(new Impact { Position = hitPoint, Normal = hitNormal, Time = Time.time + travelTime });
    }

    public void ShowContinuousImpact(bool hit, Vector3 point, Vector3 normal)
    {
        if (_mode != NozzleType.Standard || !IsEmitting || !hit || Time.time < _nextSplashTime) return;
        _nextSplashTime = Time.time + 0.065f;
        EmitSplash(new Impact { Position = point, Normal = normal }, 10f);
    }

    public void WaterFire(Object fire, Vector3 contactPoint, float amount)
    {
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        SteamContact contact = null;
        int available = -1;
        for (int i = 0; i < _steamContacts.Length; i++)
        {
            SteamContact candidate = _steamContacts[i];
            if (candidate != null && candidate.Fire == fire) { contact = candidate; break; }
            if (available < 0 && (candidate == null || Time.time - candidate.LastWaterTime > 1.5f)) available = i;
        }
        if (contact == null)
        {
            // Bound the cost of spraying many overlapping fire zones at once.
            if (available < 0) return;
            contact = _steamContacts[available];
            if (contact == null)
            {
                contact = new SteamContact { Effect = CreateEffect(_library.Steam, "Fire contact steam") };
                _steamContacts[available] = contact;
            }
            contact.Fire = fire;
            contact.NextPuffTime = 0f;
        }
        contact.LastWaterTime = Time.time;
        if (Time.time < contact.NextPuffTime) return;
        contact.NextPuffTime = Time.time + 0.1f;
        ConfigureContact(contact.Effect, contactPoint, TiltedView.Up);
        contact.Effect.SetFloat(CountId, Mathf.Clamp(amount * 2f, 3f, 24f));
        contact.Effect.Play();
    }

    public void Stop()
    {
        if (_water == null || !IsEmitting) return;
        IsEmitting = false;
        _water.Stop();
    }

    private void Update()
    {
        while (_pendingImpacts.Count > 0 && _pendingImpacts.Peek().Time <= Time.time)
            EmitSplash(_pendingImpacts.Dequeue(), 28f);
    }

    private void OnDisable()
    {
        IsEmitting = false;
        _pendingImpacts.Clear();
        if (_water != null) _water.Stop();
        if (_splash != null) _splash.Stop();
        foreach (SteamContact contact in _steamContacts)
            if (contact != null) contact.Effect.Stop();
    }

    private void ConfigureWater(Vector3 origin, Vector2 direction, float range, float width, float lifetime, float size)
    {
        _water.transform.position = origin;
        _water.SetVector3(OriginId, origin);
        _water.SetVector3(DirectionId, direction);
        _water.SetVector3(UpId, TiltedView.Up);
        _water.SetFloat(RangeId, Mathf.Max(0.001f, range));
        _water.SetFloat(WidthId, Mathf.Max(0.001f, width));
        _water.SetFloat(LifetimeId, Mathf.Max(0.02f, lifetime));
        _water.SetFloat(SizeId, Mathf.Max(0.01f, size * 0.24f));
    }

    private void EmitSplash(Impact impact, float count)
    {
        ConfigureContact(_splash, impact.Position + impact.Normal * 0.025f, impact.Normal);
        _splash.SetFloat(CountId, count);
        _splash.Play();
    }

    private static void ConfigureContact(VisualEffect effect, Vector3 point, Vector3 direction)
    {
        effect.transform.position = point;
        effect.SetVector3(OriginId, point);
        effect.SetVector3(DirectionId, direction);
        effect.SetVector3(UpId, TiltedView.Up);
    }

    private VisualEffect CreateEffect(VisualEffectAsset asset, string effectName)
    {
        var go = new GameObject(effectName);
        go.transform.SetParent(transform, false);
        var effect = go.AddComponent<VisualEffect>();
        effect.initialEventName = "HoseIdle";
        effect.visualEffectAsset = asset;
        var renderer = go.GetComponent<VFXRenderer>();
        renderer.sortingLayerName = _sortingLayer;
        renderer.sortingOrder = _sortingOrder;
        return effect;
    }
}
