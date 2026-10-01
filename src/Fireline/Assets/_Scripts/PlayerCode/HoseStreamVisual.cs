using UnityEngine;

/// <summary>
/// The animated water for a hose. Purely visual: HoseWeapon's hitbox decides what gets hit.
///
/// By default it builds a particle system in code that fires sprite-sheet water blobs out
/// of the nozzle. Particles live in world space, so sweeping the aim or running while
/// spraying makes the stream bend and trail like a real hose. A VFX artist can replace it
/// entirely by giving HoseWeapon a particle prefab instead.
/// </summary>
public sealed class HoseStreamVisual
{
    /// <summary>Look settings for the built-in stream. Ignored when a custom prefab is used.</summary>
    public struct Look
    {
        public Material StreamMaterial;
        public int StreamFrames;
        public Color StreamColor;
        public Material SplashMaterial;
        public int SplashFrames;
        public float SplashChance;
        public string SortingLayerName;
        public int SortingOrder;
    }

    private readonly Transform _holder;     // rotated so its +X points along the aim
    private readonly ParticleSystem _particles;
    private readonly bool _isCustom;
    private readonly bool _flipRotation;

    public bool IsEmitting { get; private set; }

    /// <param name="customPrefab">Optional VFX prefab. Author it pointing RIGHT (+X); the hose rotates it to aim.</param>
    public HoseStreamVisual(Transform parent, ParticleSystem customPrefab, Look look, bool flipRotation)
    {
        _flipRotation = flipRotation;

        GameObject holder = new GameObject("Hose stream");
        holder.transform.SetParent(parent, false);
        _holder = holder.transform;

        if (customPrefab != null)
        {
            _particles = Object.Instantiate(customPrefab, _holder);
            _isCustom = true;
        }
        else
        {
            _particles = BuildStream(_holder, look);
        }

        ParticleSystem.EmissionModule emission = _particles.emission;
        emission.enabled = false;
        _particles.Play(true);
    }

    /// <summary>Points the stream and turns continuous spraying on or off.</summary>
    public void SetSpraying(bool spraying, Vector2 origin, Vector2 direction, float z,
        float range, float width, float lifetime, float particlesPerSecond, float particleSize)
    {
        if (spraying)
        {
            Aim(origin, direction, z);
            Shape(direction, range, width, lifetime, particleSize, 0.9f, 1.1f);
            if (!_isCustom)
            {
                ParticleSystem.EmissionModule emission = _particles.emission;
                emission.rateOverTime = particlesPerSecond;
            }
        }

        if (spraying == IsEmitting) return;
        IsEmitting = spraying;
        ParticleSystem.EmissionModule em = _particles.emission;
        em.enabled = spraying;
    }

    /// <summary>One burst of water, for pulsing nozzles like the jet.</summary>
    public void Pulse(Vector2 origin, Vector2 direction, float z,
        float range, float width, float lifetime, float particleSize, int count)
    {
        Aim(origin, direction, z);
        // A wide speed spread fills the whole line at once instead of one clump.
        Shape(direction, range, width, lifetime, particleSize, 0.35f, 1.15f);
        _particles.Emit(count);
    }

    public void Stop()
    {
        if (!IsEmitting) return;
        IsEmitting = false;
        ParticleSystem.EmissionModule em = _particles.emission;
        em.enabled = false;
    }

    // ---------------------------------------------------------------- per-frame setup

    private void Aim(Vector2 origin, Vector2 direction, float z)
    {
        float angle = Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg;
        _holder.SetPositionAndRotation(new Vector3(origin.x, origin.y, z), Quaternion.Euler(0f, 0f, angle));
    }

    private void Shape(Vector2 direction, float range, float width, float lifetime,
        float particleSize, float minSpeed, float maxSpeed)
    {
        if (_isCustom) return; // a VFX prefab controls its own look

        float speed = range / lifetime; // so blobs reach the end of the hitbox as they fade
        float spread = width * 0.5f / lifetime; // so the stream fans out to the hitbox's width

        ParticleSystem.MainModule main = _particles.main;
        main.startLifetime = lifetime;
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * minSpeed, speed * maxSpeed);
        main.startSize = new ParticleSystem.MinMaxCurve(particleSize * 0.8f, particleSize * 1.2f);

        // Turn each blob to face along the stream. Particle rotation runs clockwise,
        // the opposite of transform rotation, hence the minus sign by default.
        float aimRadians = Mathf.Atan2(direction.y, direction.x);
        main.startRotation = _flipRotation ? aimRadians : -aimRadians;

        // Sideways drift in the emitter's local Y, which is "across the stream".
        ParticleSystem.VelocityOverLifetimeModule velocity = _particles.velocityOverLifetime;
        velocity.x = new ParticleSystem.MinMaxCurve(0f, 0f);
        velocity.y = new ParticleSystem.MinMaxCurve(-spread, spread);
        velocity.z = new ParticleSystem.MinMaxCurve(0f, 0f);
    }

    // ---------------------------------------------------------------- building the default stream

    private static ParticleSystem BuildStream(Transform holder, Look look)
    {
        GameObject go = new GameObject("Water");
        go.transform.SetParent(holder, false);
        // Particle cones fire along local +Z; turn that to point along the holder's +X (the aim).
        // Rotating about Y leaves local Y as "across the stream".
        go.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = ps.main;
        main.loop = true;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World; // lets the stream bend as you sweep
        main.gravityModifier = 0f;
        main.maxParticles = 500;
        main.startColor = look.StreamColor;
        main.startLifetime = 0.3f;

        ParticleSystem.ShapeModule shape = ps.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Cone;
        shape.angle = 0f;
        shape.radius = 0.05f;

        ParticleSystem.VelocityOverLifetimeModule velocity = ps.velocityOverLifetime;
        velocity.enabled = true;
        velocity.space = ParticleSystemSimulationSpace.Local;

        // A little turbulence so the stream wobbles instead of looking like a laser.
        ParticleSystem.NoiseModule noise = ps.noise;
        noise.enabled = true;
        noise.strength = 0.5f;
        noise.frequency = 1.5f;
        noise.scrollSpeed = 1f;
        noise.damping = true;
        noise.quality = ParticleSystemNoiseQuality.Low;

        // Blobs swell as they travel...
        ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
        size.enabled = true;
        size.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(new Keyframe(0f, 0.6f), new Keyframe(1f, 1.4f)));

        // ...and fade out near the end of their range.
        ParticleSystem.ColorOverLifetimeModule color = ps.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeOut(0.6f));

        // Flip through the sprite sheet's frames so every blob is animated.
        ParticleSystem.TextureSheetAnimationModule sheet = ps.textureSheetAnimation;
        sheet.enabled = look.StreamFrames > 1;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = Mathf.Max(1, look.StreamFrames);
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.cycleCount = 2;

        ParticleSystemRenderer renderer = ps.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = look.StreamMaterial;
        renderer.sortingLayerName = look.SortingLayerName;
        renderer.sortingOrder = look.SortingOrder;

        if (look.SplashMaterial != null && look.SplashChance > 0f)
            AddSplash(ps, look);

        return ps;
    }

    // Small droplet puffs where some blobs die, so the stream "lands" instead of vanishing.
    private static void AddSplash(ParticleSystem stream, Look look)
    {
        GameObject go = new GameObject("Splash");
        go.transform.SetParent(stream.transform, false);
        ParticleSystem splash = go.AddComponent<ParticleSystem>();
        splash.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

        ParticleSystem.MainModule main = splash.main;
        main.loop = false;
        main.playOnAwake = false;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.gravityModifier = 0f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.2f, 0.35f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.3f, 1.2f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.2f, 0.4f);
        main.startColor = look.StreamColor;
        main.maxParticles = 200;

        ParticleSystem.EmissionModule emission = splash.emission;
        emission.rateOverTime = 0f;
        emission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1, 2) });

        ParticleSystem.ShapeModule shape = splash.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.1f;

        ParticleSystem.ColorOverLifetimeModule color = splash.colorOverLifetime;
        color.enabled = true;
        color.color = new ParticleSystem.MinMaxGradient(FadeOut(0.4f));

        ParticleSystem.TextureSheetAnimationModule sheet = splash.textureSheetAnimation;
        sheet.enabled = look.SplashFrames > 1;
        sheet.mode = ParticleSystemAnimationMode.Grid;
        sheet.numTilesX = Mathf.Max(1, look.SplashFrames);
        sheet.numTilesY = 1;
        sheet.animation = ParticleSystemAnimationType.WholeSheet;
        sheet.cycleCount = 1;

        ParticleSystemRenderer renderer = splash.GetComponent<ParticleSystemRenderer>();
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        renderer.sharedMaterial = look.SplashMaterial;
        renderer.sortingLayerName = look.SortingLayerName;
        renderer.sortingOrder = look.SortingOrder + 1;

        ParticleSystem.SubEmittersModule subEmitters = stream.subEmitters;
        subEmitters.enabled = true;
        subEmitters.AddSubEmitter(splash, ParticleSystemSubEmitterType.Death,
            ParticleSystemSubEmitterProperties.InheritNothing, look.SplashChance);
    }

    // White gradient that stays opaque until fadeStart, then fades to transparent.
    private static Gradient FadeOut(float fadeStart)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(1f, fadeStart), new GradientAlphaKey(0f, 1f) });
        return gradient;
    }
}
