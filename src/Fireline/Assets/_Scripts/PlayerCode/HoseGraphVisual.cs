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
    private static readonly int ActiveId = Shader.PropertyToID("Active");
    private static readonly int BeamWidthId = Shader.PropertyToID("BeamWidth");
    private static readonly int BeamColorId = Shader.PropertyToID("BeamColor");
    private static readonly int EffectColorId = Shader.PropertyToID("EffectColor");
    private static readonly int VariationId = Shader.PropertyToID("SizeVariation");
    private static readonly int SpeedId = Shader.PropertyToID("Speed");
    private static readonly int SpreadId = Shader.PropertyToID("Spread");
    private static readonly int FallSpeedId = Shader.PropertyToID("FallSpeed");
    private static readonly int GravityId = Shader.PropertyToID("Gravity");
    private static readonly int LengthId = Shader.PropertyToID("Length");
    private static readonly int SpearWidthId = Shader.PropertyToID("SpearWidth");
    private static readonly int FadeStartId = Shader.PropertyToID("FadeStart");
    private static readonly int BeamMeshId = Shader.PropertyToID("BeamMesh");
    private static readonly int WaterSpeedId = Shader.PropertyToID("WaterSpeed");
    private static readonly int BeamGravityId = Shader.PropertyToID("BeamGravity");
    private static readonly int MaxDropId = Shader.PropertyToID("MaxDrop");

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
    private HoseStreamRibbon _streamRibbon;

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
        if (mode == NozzleType.Standard && _water.HasMesh(BeamMeshId))
        {
            _streamRibbon = new HoseStreamRibbon();
            _water.SetMesh(BeamMeshId, _streamRibbon.Mesh);
        }
    }

    public void SetSpraying(bool spraying, Vector3 origin, Vector2 direction,
        float range, float width, float lifetime, float rate, float size, float heightAboveGround = -1f)
    {
        if (_streamRibbon != null)
        {
            if (!spraying) { Stop(); return; }
            _streamRibbon.Aim(origin, direction, range, heightAboveGround);
            ConfigureWater(origin, direction, range, width, lifetime, size);
            RefreshRibbon(0f);
            bool starting = !IsEmitting;
            IsEmitting = true;
            if (starting) _water.Play();
            return;
        }
        if (spraying)
        {
            ConfigureWater(origin, direction, range, width, lifetime, size);
            _water.SetFloat(RateId, _mode == NozzleType.Standard && _water.HasFloat(ActiveId)
                ? Mathf.Max(0f, _library.StreamAppearance.dropletRate)
                : rate * (_mode == NozzleType.Mist ? 9f * _library.MistAppearance.density : 6f));
        }
        if (_water.HasFloat(ActiveId)) _water.SetFloat(ActiveId, spraying ? 1f : 0f);
        if (IsEmitting == spraying) return;
        IsEmitting = spraying;
        if (spraying) _water.Play();
        else _water.Stop();
    }

    public void Pulse(Vector3 origin, Vector2 direction, float range, float width,
        float lifetime, float size, int count, bool hit, Vector3 hitPoint, Vector3 hitNormal)
    {
        bool spear = _water.HasFloat(LengthId);
        float travelTime = spear ? Mathf.Max(0.02f, _library.JetAppearance.travelTime) : Mathf.Min(lifetime, 0.09f);
        ConfigureWater(origin, direction, range, width, travelTime, size);
        _water.SetFloat(CountId, spear ? 1f : Mathf.Max(1, count) * 4f);
        _water.Play();
        if (!hit) return;
        // Keep a fired shot's impact even if the trigger is released or the nozzle is switched.
        if (_pendingImpacts.Count == 8) EmitSplash(_pendingImpacts.Dequeue(), _library.SplashAppearance.jetCount);
        float impactDelay = travelTime;
        if (spear)
        {
            float length = Mathf.Min(_library.JetAppearance.length, range);
            float hitDistance = Vector3.Dot(hitPoint - origin, direction);
            impactDelay *= Mathf.Clamp01((hitDistance - length) / Mathf.Max(0.001f, range - length));
        }
        _pendingImpacts.Enqueue(new Impact { Position = hitPoint, Normal = hitNormal, Time = Time.time + impactDelay });
    }

    public void ShowContinuousImpact(bool hit, Vector3 point, Vector3 normal)
    {
        if (_mode != NozzleType.Standard || !IsEmitting || !hit || Time.time < _nextSplashTime) return;
        if (_streamRibbon != null)
        {
            float distance = Vector3.Dot(point - _streamRibbon.Origin, _streamRibbon.Direction);
            if (_streamRibbon.Length + 0.01f < distance) return;
            point -= TiltedView.Up * _streamRibbon.Drop(distance, _library.StreamAppearance);
        }
        _nextSplashTime = Time.time + Mathf.Max(0.02f, _library.SplashAppearance.streamInterval);
        EmitSplash(new Impact { Position = point, Normal = normal }, _library.SplashAppearance.streamCount);
    }

    public void WaterFire(Object fire, Vector3 contactPoint, float amount)
    {
        if (amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        if (_streamRibbon != null && (!IsEmitting || Vector3.Dot(contactPoint - _streamRibbon.Origin,
            _streamRibbon.Direction) > _streamRibbon.Length + 0.05f)) return;
        SteamContact contact = null;
        int available = -1;
        for (int i = 0; i < _steamContacts.Length; i++)
        {
            SteamContact candidate = _steamContacts[i];
            if (candidate != null && candidate.Fire == fire) { contact = candidate; break; }
            if (available < 0 && (candidate == null || Time.time - candidate.LastWaterTime
                > Mathf.Max(1.5f, _library.SteamAppearance.lifetime * 1.2f))) available = i;
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
        var settings = _library.SteamAppearance;
        contact.NextPuffTime = Time.time + Mathf.Max(0.02f, settings.puffInterval);
        ConfigureContact(contact.Effect, contactPoint, TiltedView.Up);
        ConfigureAppearance(contact.Effect, settings.size, settings.lifetime, settings.color, settings.riseSpeed, settings.spread);
        contact.Effect.SetFloat(CountId, Mathf.Clamp(amount * settings.particlesPerWaterUnit,
            settings.minimumCount, Mathf.Max(settings.minimumCount, settings.maximumCount)));
        contact.Effect.Play();
    }

    public void Stop()
    {
        if (_water == null || !IsEmitting) return;
        IsEmitting = false;
        if (_streamRibbon != null) _streamRibbon.Stop();
        else if (_water.HasFloat(ActiveId)) _water.SetFloat(ActiveId, 0f);
        _water.Stop();
    }

    private void Update()
    {
        if (_streamRibbon != null && _streamRibbon.IsVisible) RefreshRibbon(Time.deltaTime);
        while (_pendingImpacts.Count > 0 && _pendingImpacts.Peek().Time <= Time.time)
            EmitSplash(_pendingImpacts.Dequeue(), _library.SplashAppearance.jetCount);
    }

    private void OnDisable()
    {
        IsEmitting = false;
        _streamRibbon?.Clear();
        _pendingImpacts.Clear();
        if (_water != null)
        {
            if (_water.HasFloat(ActiveId)) _water.SetFloat(ActiveId, 0f);
            _water.Stop();
        }
        if (_splash != null) _splash.Stop();
        foreach (SteamContact contact in _steamContacts)
            if (contact != null) contact.Effect.Stop();
    }

    private void OnDestroy() => _streamRibbon?.Dispose();

    private void RefreshRibbon(float deltaTime)
    {
        var settings = _library.StreamAppearance;
        _streamRibbon.Tick(deltaTime, settings);
        _water.SetMesh(BeamMeshId, _streamRibbon.Mesh);
        _water.SetFloat(RangeId, Mathf.Max(0.001f, _streamRibbon.Length));
        _water.SetFloat(ActiveId, _streamRibbon.IsVisible ? 1f : 0f);
        Color color = settings.beamColor;
        color.a *= _streamRibbon.Opacity;
        _water.SetVector4(BeamColorId, color);
        _water.SetFloat(RateId, IsEmitting && _streamRibbon.Length > 0.01f ? Mathf.Max(0f, settings.dropletRate) : 0f);
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
        if (_mode == NozzleType.Standard && _water.HasFloat(ActiveId))
        {
            var settings = _library.StreamAppearance;
            _water.SetFloat(BeamWidthId, Mathf.Max(0.01f, settings.beamWidth));
            _water.SetVector4(BeamColorId, settings.beamColor);
            _water.SetFloat(LifetimeId, Mathf.Max(0.05f, settings.dropletLifetime));
            _water.SetFloat(SizeId, Mathf.Max(0.005f, settings.dropletSize));
            _water.SetFloat(VariationId, Mathf.Clamp01(settings.sizeVariation));
            _water.SetFloat(SpeedId, Mathf.Max(0f, settings.dropletSpeed));
            _water.SetFloat(SpreadId, Mathf.Max(0f, settings.dropletSpread));
            _water.SetFloat(FallSpeedId, Mathf.Max(0f, settings.fallSpeed));
            _water.SetVector3(GravityId, -TiltedView.Up * Mathf.Max(0f, settings.gravity));
            _water.SetVector4(EffectColorId, settings.dropletColor);
            if (_streamRibbon != null)
            {
                _water.SetFloat(WaterSpeedId, Mathf.Max(0.1f, settings.extensionSpeed));
                _water.SetFloat(BeamGravityId, Mathf.Max(0f, settings.beamGravity));
                _water.SetFloat(MaxDropId, Mathf.Max(0f, _streamRibbon.MaximumDrop(settings)));
            }
        }
        else if (_mode == NozzleType.Mist)
        {
            var settings = _library.MistAppearance;
            _water.SetFloat(WidthId, Mathf.Max(0.001f, width * settings.spread));
            _water.SetFloat(LifetimeId, Mathf.Max(0.02f, lifetime * settings.lifetime));
            _water.SetFloat(SizeId, Mathf.Max(0.01f, size * 0.24f * settings.size));
        }
        else if (_mode == NozzleType.Jet && _water.HasFloat(LengthId))
        {
            var settings = _library.JetAppearance;
            _water.SetFloat(LengthId, Mathf.Max(0.05f, settings.length));
            _water.SetFloat(SpearWidthId, Mathf.Max(0.01f, settings.width));
            _water.SetFloat(FadeStartId, Mathf.Clamp(settings.fadeStart, 0.1f, 0.99f));
            _water.SetVector4(EffectColorId, settings.color);
        }
    }

    private void EmitSplash(Impact impact, float count)
    {
        if (count <= 0f) return;
        ConfigureContact(_splash, impact.Position + impact.Normal * 0.025f, impact.Normal);
        var settings = _library.SplashAppearance;
        ConfigureAppearance(_splash, settings.size, settings.lifetime, settings.color, settings.speed, settings.spread);
        _splash.SetFloat(CountId, count);
        _splash.Play();
    }

    private static void ConfigureAppearance(VisualEffect effect, float size, float lifetime, Color color, float speed, float spread)
    {
        effect.SetFloat(SizeId, Mathf.Max(0.005f, size));
        effect.SetFloat(LifetimeId, Mathf.Max(0.05f, lifetime));
        if (!effect.HasVector4(EffectColorId)) return; // Older/custom library graphs retain their original look.
        effect.SetVector4(EffectColorId, color);
        effect.SetFloat(SpeedId, Mathf.Max(0f, speed));
        effect.SetFloat(SpreadId, Mathf.Max(0f, spread));
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
