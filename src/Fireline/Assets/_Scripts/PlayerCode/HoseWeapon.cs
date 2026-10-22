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
    [Tooltip("Layers that stop the water, like walls and buildings. If left empty, the \"Obstacles\" layer is used.")]
    [SerializeField] private LayerMask blockingLayers;
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

    [Header("VFX Graph")]
    [Tooltip("Use the shared hose graphs when no custom Particle System prefab is assigned.")]
    [SerializeField] private bool useVfxGraph = true;
    [Tooltip("Optional override. Otherwise loads Resources/HoseVfx/HoseVfxLibrary.")]
    [SerializeField] private HoseVfxLibrary hoseVfx;

    private readonly List<Collider2D> _colliderHits = new List<Collider2D>(64);
    private readonly HashSet<HordeEnemy> _seen = new HashSet<HordeEnemy>();
    private readonly List<HordeEnemy> _enemyHits = new List<HordeEnemy>(64);
    private readonly HashSet<FireZone> _seenFires = new HashSet<FireZone>();
    private readonly List<FireZone> _fireHits = new List<FireZone>(8);
    private readonly List<Vector2> _fireContactPoints = new List<Vector2>(8);
    private bool _scannedThisStep;
    private HoseStreamVisual _stream;
    private HoseGraphVisual _graph;
    private PlayerHealth _health;
    private RaycastHit2D _wall;
    private float _enemyImpactDistance;
    private Vector2 _enemyImpactPoint;

    /// <summary>The held nozzle transform, used by the supply hose attachment.</summary>
    public Transform NozzleTransform => muzzle != null ? muzzle : transform;

    public float Range => range;
    /// <summary>How far the water actually reaches this step: Range, or less if a wall is in the way.</summary>
    public float CurrentRange { get; private set; }
    public float Width => width;
    /// <summary>True while the continuous water visual is running (pulses don't count).</summary>
    public bool IsStreamActive => _graph != null ? _graph.IsEmitting : _stream != null && _stream.IsEmitting;
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
        if (blockingLayers == 0)
            blockingLayers = LayerMask.GetMask(Structure.ObstacleLayer);

        if (useVfxGraph && streamPrefab == null && SystemInfo.supportsComputeShaders)
        {
            HoseVfxLibrary library = hoseVfx != null ? hoseVfx : Resources.Load<HoseVfxLibrary>(HoseVfxLibrary.ResourcePath);
            if (library != null && library.IsComplete)
            {
                var holder = new GameObject("Hose VFX Graph");
                holder.transform.SetParent(transform, false);
                _graph = holder.AddComponent<HoseGraphVisual>();
                NozzleType mode = this is MistHose ? NozzleType.Mist : this is JetHose ? NozzleType.Jet : NozzleType.Standard;
                _graph.Initialize(library, mode, sortingLayerName, sortingOrder);
                return;
            }
        }

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
        if (!isActiveAndEnabled || (_health != null && _health.IsDead)
            || !HoseWaterRules.CanSpray(gameObject) || direction.sqrMagnitude < 0.001f)
        {
            StopSpraying();
            return;
        }

        AimDirection = direction.normalized;
        MuzzlePosition = muzzle != null ? muzzle.position : transform.position;
        Origin = TiltedView.ToGround(MuzzlePosition, transform.position.z);

        // Walls block water: cut the stream short at the first obstacle along its center line.
        CurrentRange = range;
        _wall = default;
        if (blockingLayers != 0)
        {
            _wall = Physics2D.Raycast(Origin, AimDirection, range, blockingLayers);
            if (_wall.collider != null) CurrentRange = _wall.distance;
        }
        End = Origin + AimDirection * CurrentRange;
        _scannedThisStep = false;

        OnSpray(deltaTime);
        if (_graph != null)
        {
            _graph.SetSpraying(ShowsContinuousStream(), MuzzlePosition, AimDirection,
                CurrentRange, width, particleLifetime, particlesPerSecond, particleSize);
            if (ShowsContinuousStream() && !(this is MistHose))
            {
                bool hit = TryGetVisualImpact(out Vector3 point, out Vector3 normal);
                _graph.ShowContinuousImpact(hit, point, normal);
            }
        }
        else
            _stream.SetSpraying(ShowsContinuousStream(), MuzzlePosition, AimDirection,
                VisualRange, width, particleLifetime, particlesPerSecond, particleSize);
    }

    /// <summary>Call when the spray button is released.</summary>
    public void StopSpraying()
    {
        if (_stream != null) _stream.Stop();
        if (_graph != null) _graph.Stop();
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

    // Keep a little water visible even against a wall, so it splashes instead of vanishing.
    private float VisualRange => Mathf.Max(CurrentRange, 0.25f);

    /// <summary>Fires one burst of water along the current aim. Call from OnSpray.</summary>
    protected void PulseStream(int particleCount)
    {
        if (_graph != null)
        {
            bool hit = TryGetVisualImpact(out Vector3 point, out Vector3 normal);
            _graph.Pulse(MuzzlePosition, AimDirection, CurrentRange, width,
                particleLifetime, particleSize, particleCount, hit, point, normal);
        }
        else
            _stream.Pulse(MuzzlePosition, AimDirection,
                VisualRange, width, particleLifetime, particleSize, particleCount);
    }

    // Reuse the gameplay query; GPU particles never decide damage or extinguishing.
    private bool TryGetVisualImpact(out Vector3 point, out Vector3 normal)
    {
        ScanHitbox();
        bool found = _wall.collider != null;
        float nearest = CurrentRange;
        Vector2 groundPoint = End;
        normal = found ? (Vector3)_wall.normal : -(Vector3)AimDirection;
        if (_enemyImpactDistance < nearest)
        {
            nearest = _enemyImpactDistance;
            groundPoint = _enemyImpactPoint;
            normal = -(Vector3)AimDirection;
            found = true;
        }
        if (found && nearest == CurrentRange) groundPoint = _wall.point;
        Vector3 visualLift = MuzzlePosition - new Vector3(Origin.x, Origin.y, transform.position.z);
        point = new Vector3(groundPoint.x, groundPoint.y, transform.position.z) + visualLift;
        return found;
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
        {
            if (_graph != null && fires[i].IsBurning && amount > 0f)
            {
                Vector2 contact = _fireContactPoints[i];
                _graph.WaterFire(fires[i],
                    new Vector3(contact.x, contact.y, fires[i].transform.position.z) + TiltedView.Up * 0.15f, amount);
            }
            fires[i].ApplyWater(amount);
        }
        // Downed bodies leave physics simulation; query their registered ground positions directly.
        if (_health == null || CurrentRange <= 0.01f) return;
        Vector2 across = new Vector2(-AimDirection.y, AimDirection.x);
        foreach (var player in PlayerHealth.ActivePlayers)
        {
            if (player == null || !player.IsDowned || player == _health
                || player.gameObject.scene != gameObject.scene
                || (hitLayers.value & (1 << player.gameObject.layer)) == 0) continue;
            Vector2 offset = (Vector2)player.transform.position - Origin;
            float forward = Vector2.Dot(offset, AimDirection);
            if (forward < 0f || forward > CurrentRange || Mathf.Abs(Vector2.Dot(offset, across)) > width * 0.5f + 0.3f) continue;
            // A wide stream must not revive someone behind a wall beside its center line.
            if (blockingLayers != 0 && Physics2D.Linecast(Origin, player.transform.position, blockingLayers).collider != null) continue;
            player.ApplyRevivalWater(_health, amount);
        }
    }

    // One physics query per Spray step, shared by the enemy and fire lists.
    private void ScanHitbox()
    {
        if (_scannedThisStep) return;
        _scannedThisStep = true;

        _enemyHits.Clear();
        _seen.Clear();
        _fireHits.Clear();
        _fireContactPoints.Clear();
        _colliderHits.Clear();
        _seenFires.Clear();
        _enemyImpactDistance = float.PositiveInfinity;

        // Horde movement writes Transforms directly, so queries need current physics poses.
        Physics2D.SyncTransforms();
        ContactFilter2D filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitLayers);

        float angle = Mathf.Atan2(AimDirection.y, AimDirection.x) * Mathf.Rad2Deg;
        if (CurrentRange <= 0.01f) return; // muzzle is right against a wall: nothing gets hit
        Physics2D.OverlapBox((Origin + End) * 0.5f, new Vector2(CurrentRange, width), angle, filter, _colliderHits);

        for (int i = 0; i < _colliderHits.Count; i++)
        {
            Collider2D hit = _colliderHits[i];
            if (hit == null) continue;

            HordeEnemy enemy = hit.GetComponentInParent<HordeEnemy>();
            if (enemy != null && enemy.isActiveAndEnabled && enemy.CurrentHealth > 0f)
            {
                if (_seen.Add(enemy)) _enemyHits.Add(enemy);
                if (_graph != null)
                {
                    Vector2 point = hit.ClosestPoint(Origin);
                    float distance = Vector2.Dot(point - Origin, AimDirection);
                    if (distance >= 0f && distance < _enemyImpactDistance)
                    {
                        _enemyImpactDistance = distance;
                        _enemyImpactPoint = point;
                    }
                }
            }

            FireZone fire = hit.GetComponentInParent<FireZone>();
            if (fire != null && fire.isActiveAndEnabled && fire.IsBurning && _seenFires.Add(fire))
            {
                _fireHits.Add(fire);
                float along = Mathf.Clamp(Vector2.Dot((Vector2)fire.transform.position - Origin, AimDirection), 0f, CurrentRange);
                _fireContactPoints.Add(hit.ClosestPoint(Origin + AimDirection * along));
            }
        }
    }

#if UNITY_EDITOR
    // Select the player to see this nozzle's hitbox, even when not spraying.
    protected virtual void OnDrawGizmosSelected()
    {
        Vector2 dir = Application.isPlaying ? AimDirection : (Vector2)transform.right;
        Vector3 muzzlePosition = muzzle != null ? muzzle.position : transform.position;
        Vector2 start = TiltedView.ToGround(muzzlePosition, transform.position.z);
        float length = Application.isPlaying && CurrentRange > 0f ? CurrentRange : range;
        Vector2 center = start + dir * (length * 0.5f);
        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg;

        Gizmos.color = new Color(0.25f, 0.8f, 1f, 0.9f);
        Gizmos.matrix = Matrix4x4.TRS(center, Quaternion.Euler(0f, 0f, angle), Vector3.one);
        Gizmos.DrawWireCube(Vector3.zero, new Vector3(length, width, 0f));
        Gizmos.matrix = Matrix4x4.identity;
    }
#endif
}
