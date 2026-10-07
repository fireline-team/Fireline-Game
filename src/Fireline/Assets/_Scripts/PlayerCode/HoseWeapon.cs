using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

/// <summary>
/// To make a new nozzle: inherit from this, then override OnSpray.
/// </summary>
public abstract class HoseWeapon : MonoBehaviour
{
    [Header("Hitbox")]
    [Tooltip("How far the stream reaches from the muzzle.")]
    [SerializeField, Min(0.1f)] private float range = 5f;
    [Tooltip("How wide the stream is, across the aim direction.")]
    [SerializeField, Min(0.01f)] private float width = 0.6f;
    [SerializeField] private LayerMask hitLayers = ~0;
    [SerializeField] private Transform muzzle;

    [Header("Firefighting")]
    [Tooltip("How much each fire zone in the stream is put out per second of spraying. Every nozzle has its own value, so e.g. mist can be better at fires.")]
    [SerializeField, Min(0f)] private float extinguishPerSecond = 30f;

    [Header("Stream Visual")]
    [Tooltip("Optional particle prefab from VFX that replaces the built-in stream. Author it pointing RIGHT (+X); the hose aims it.")]
    [SerializeField] private ParticleSystem streamPrefab;
    [Tooltip("Material for the water blobs: a horizontal sprite sheet, e.g. WaterStreamParticle.")]
    [SerializeField] private Material streamMaterial;
    [Tooltip("How many frames are in the stream material's sprite sheet, left to right.")]
    [SerializeField, Min(1)] private int streamSheetFrames = 3;
    [Tooltip("Tint for the water. White shows the art's own colors.")]
    [SerializeField] private Color streamColor = Color.white;
    [SerializeField, Min(0f)] private float particlesPerSecond = 60f;
    [Tooltip("Seconds a blob takes to reach the end of the range. Lower = faster, punchier water.")]
    [SerializeField, Min(0.05f)] private float particleLifetime = 0.3f;
    [SerializeField, Min(0.05f)] private float particleSize = 0.6f;
    [Tooltip("Optional droplet puffs where blobs land, e.g. WaterDroplet.")]
    [SerializeField] private Material splashMaterial;
    [SerializeField, Min(1)] private int splashSheetFrames = 6;
    [Tooltip("Chance that a blob leaves a splash when it fades out.")]
    [SerializeField, Range(0f, 1f)] private float splashChance = 0.3f;
    [Tooltip("Draw order. Backgrounds like tilemaps must be on a lower layer or order than this.")]
    [SerializeField] private string sortingLayerName = "Default";
    [SerializeField] private int sortingOrder = 10;
    [Tooltip("Tick this if the water blobs tilt the wrong way when aiming diagonally.")]
    [SerializeField] private bool flipParticleRotation;

    private readonly List<Collider2D> _colliderHits = new List<Collider2D>(64);
    private readonly HashSet<HordeEnemy> _seen = new HashSet<HordeEnemy>();
    private readonly List<HordeEnemy> _enemyHits = new List<HordeEnemy>(64);
    private readonly HashSet<FireZone> _seenFires = new HashSet<FireZone>();
    private readonly List<FireZone> _fireHits = new List<FireZone>(8);
    private bool _scannedThisStep;
    private HoseStreamVisual _stream;
    private PlayerHealth _health;

    public float Range => range;
    public float Width => width;
    /// <summary>True while the continuous water visual is running (pulses don't count).</summary>
    public bool IsStreamActive => _stream != null && _stream.IsEmitting;
    protected float ExtinguishPerSecond => extinguishPerSecond;

    /// <summary>Normalized aim direction from the current Spray call.</summary>
    protected Vector2 AimDirection { get; private set; } = Vector2.right;
    /// <summary>
    /// Where the stream starts this step, on the ground: the point directly below the muzzle.
    /// The hitbox uses this, so holding the hose at waist height doesn't shift what it hits.
    /// </summary>
    protected Vector2 Origin { get; private set; }

    /// <summary>The muzzle's actual position, possibly raised off the ground. Visuals use this.</summary>
    protected Vector3 MuzzlePosition { get; private set; }
    /// <summary>Where the stream ends this step.</summary>
    protected Vector2 End { get; private set; }

    protected virtual void Awake()
    {
        _health = GetComponent<PlayerHealth>();

        _stream = new HoseStreamVisual(transform, streamPrefab, new HoseStreamVisual.Look
        {
            StreamMaterial = streamMaterial,
            StreamFrames = streamSheetFrames,
            StreamColor = streamColor,
            SplashMaterial = splashMaterial,
            SplashFrames = splashSheetFrames,
            SplashChance = splashChance,
            SortingLayerName = sortingLayerName,
            SortingOrder = sortingOrder,
        }, flipParticleRotation);
    }

    protected virtual void OnDisable() => StopSpraying();

    // ---------------------------------------------------------------- called by PlayerController

    /// <summary>Call every physics step while the spray button is held.</summary>
    public void Spray(Vector2 direction, float deltaTime)
    {
        if (!isActiveAndEnabled || (_health != null && _health.IsDead) || direction.sqrMagnitude < 0.001f)
        {
            StopSpraying();
            return;
        }

        AimDirection = direction.normalized;
        MuzzlePosition = muzzle != null ? muzzle.position : transform.position;
        Origin = TiltedView.ToGround(MuzzlePosition, transform.position.z);
        End = Origin + AimDirection * range;
        _scannedThisStep = false;

        OnSpray(deltaTime);
        _stream.SetSpraying(ShowsContinuousStream(), MuzzlePosition, AimDirection,
            range, width, particleLifetime, particlesPerSecond, particleSize);
    }

    /// <summary>Call when the spray button is released.</summary>
    public void StopSpraying()
    {
        if (_stream != null) _stream.Stop();
        OnStopSpraying();
    }

    // ---------------------------------------------------------------- for subclasses

    /// <summary>
    /// One physics step of spraying. Use FindEnemiesInHitbox() to get what's in the stream,
    /// then apply this nozzle's effects (damage, knockback, slow...).
    /// </summary>
    protected abstract void OnSpray(float deltaTime);

    /// <summary>Optional: react to the spray button being released.</summary>
    protected virtual void OnStopSpraying() { }

    /// <summary>
    /// Whether water flows continuously while the button is held. Pulsing nozzles return
    /// false and call PulseStream when they fire instead.
    /// </summary>
    protected virtual bool ShowsContinuousStream() => true;

    /// <summary>Fires one burst of water along the current aim. Call from OnSpray.</summary>
    protected void PulseStream(int particleCount)
    {
        _stream.Pulse(MuzzlePosition, AimDirection,
            range, width, particleLifetime, particleSize, particleCount);
    }

    /// <summary>Lets a nozzle pick its own default hitbox size when it's added in the Inspector.</summary>
    protected void SetHitboxSize(float newRange, float newWidth)
    {
        range = newRange;
        width = newWidth;
    }

    /// <summary>
    /// Every live enemy inside the hitbox right now, each listed once even if it has
    /// several colliders. The list is reused, so use it right away and don't keep it.
    ///
    /// Always collect first, then apply effects. Killing enemies while still searching
    /// can make the search skip others.
    /// </summary>
    protected List<HordeEnemy> FindEnemiesInHitbox()
    {
        ScanHitbox();
        return _enemyHits;
    }

    /// <summary>Every burning fire zone inside the hitbox right now, each listed once.</summary>
    protected List<FireZone> FindFiresInHitbox()
    {
        ScanHitbox();
        return _fireHits;
    }

    /// <summary>Puts out every fire zone in the hitbox by <paramref name="amount"/>.</summary>
    protected void WaterFiresInHitbox(float amount)
    {
        List<FireZone> fires = FindFiresInHitbox();
        for (int i = 0; i < fires.Count; i++)
            fires[i].ApplyWater(amount);
    }

    // One physics query per Spray step, shared by the enemy and fire lists.
    private void ScanHitbox()
    {
        if (_scannedThisStep) return;
        _scannedThisStep = true;

        _enemyHits.Clear();
        _seen.Clear();
        _fireHits.Clear();
        _seenFires.Clear();

        // Horde movement writes Transforms directly, so queries need current physics poses.
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitLayers);

        float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
        Physics2D.OverlapBox((Origin + End) * 0.5f, new Vector2(range, width), angle, filter, _colliderHits);

        for (int i = 0; i < _colliderHits.Count; i++)
        {
            Collider2D hit = _colliderHits[i];
            if (hit == null) continue;

            HordeEnemy enemy = hit.GetComponentInParent<HordeEnemy>();
            if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0f && _seen.Add(enemy))
                _enemyHits.Add(enemy);

            FireZone fire = hit.GetComponentInParent<FireZone>();
            if (fire != null && fire.isActiveAndEnabled && fire.IsBurning && _seenFires.Add(fire))
                _fireHits.Add(fire);
        }
    }

#if UNITY_EDITOR
    // Select the player to see this nozzle's hitbox, even when not spraying.
    protected virtual void OnDrawGizmosSelected()
    {
        Vector2 dir = Application.isPlaying ? AimDirection : (Vector2)transform.right;
        Vector3 muzzlePosition = muzzle != null ? muzzle.position : transform.position;
        Vector2 start = TiltedView.ToGround(muzzlePosition, transform.position.z);
        Vector2 center = start + dir * (range * 0.5f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(range, width, 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}