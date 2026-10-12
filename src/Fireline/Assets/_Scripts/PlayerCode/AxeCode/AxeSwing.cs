using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// The player's off-hand axe: a "get off me" melee and the interact button in one.
/// Pressing Interact swings it through an arc in front of the player (toward where they're aiming):
///
///  1. The nearest Interactable in the arc is used immediately: a loadout locker swaps your
///     nozzle, a hydrant / truck / extinguisher connects or disconnects your hose.
///  2. The blade then sweeps across the arc over Swing Duration. Each IAxeHittable in the arc
///     (enemies, parryable attacks) is hit at the moment the blade passes it, so hit-freeze
///     and effects line up with the visual.
///
/// One swing per cooldown, like a Left 4 Dead shove. A swing that hits nothing still uses it.
/// The swing is checked on the flat ground plane (XY), from the player's feet.
/// The orange wedge gizmo shows the hitbox; it flashes during a swing and greys out while cooling down.
/// AxeSwingVisual draws the axe and trail from this component's state.
/// </summary>
[DisallowMultipleComponent]
public class AxeSwing : MonoBehaviour
{
    public const float DefaultRange = 1.5f;

    [Header("Swing")]
    [Tooltip("How far the axe reaches from the player's feet, in world units.")]
    [SerializeField, Min(0.1f)] private float range = DefaultRange;
    [Tooltip("Width of the swing, centered on the aim direction. 180 = everything in front of the player.")]
    [SerializeField, Range(10f, 180f)] private float arcDegrees = 180f;
    [Tooltip("Seconds between swings (counted from the start of a swing).")]
    [SerializeField, Min(0f)] private float cooldown = 2f;
    [Tooltip("How long the blade takes to sweep the whole arc. Short (0.1–0.2) feels snappy.")]
    [SerializeField, Min(0.01f)] private float swingDuration = 0.15f;
    [Tooltip("Shape of the sweep over time (0 = start of arc, 1 = end). Default starts fast and eases out.")]
    [SerializeField] private AnimationCurve swingCurve = new AnimationCurve(
        new Keyframe(0f, 0f, 0f, 2.2f), new Keyframe(1f, 1f, 0f, 0f));
    [Tooltip("Alternate swinging left-to-right and right-to-left, like a back-and-forth chop.")]
    [SerializeField] private bool alternateDirection = true;

    [Header("Combat")]
    [Tooltip("Damage dealt to each enemy in the arc.")]
    [SerializeField, Min(0f)] private float damage = 5f;
    [Tooltip("Knockback speed, pushing enemies directly away from the player. Wears off at the HordeManager's Knockback Damping, " +
             "so enemies slide roughly Knockback / Damping units (12 / 8 = 1.5).")]
    [SerializeField, Min(0f)] private float knockback = 12f;
    [Tooltip("Layers the axe can hit. Leave on Everything unless the query gets expensive.")]
    [SerializeField] private LayerMask hitLayers = ~0;

    [Header("Parry")]
    [Tooltip("A successful parry makes the axe ready again as soon as the swing ends.")]
    [SerializeField] private bool parryRefundsCooldown = true;

    [Header("Gizmo")]
    [Tooltip("Draw the hitbox even when the player isn't selected.")]
    [SerializeField] private bool alwaysShowGizmo = true;

    private PlayerController _controller;
    private PlayerInteractor _interactor;
    private PlayerHealth _health;
    private float _readyAt;

    // Current swing
    private bool _swinging;
    private float _progress;            // 0..1 through the swing, in scaled time (so hit-freeze pauses it)
    private Vector2 _swingFacing;       // aim locked at the start of the swing
    private float _startAngle, _endAngle; // degrees relative to _swingFacing
    private float _sweptAngle;          // how far the blade has travelled, relative to _startAngle (signed)
    private bool _nextSwingReversed;
    private AxeSwingResult _result;
    private bool _usedPending;          // the used Interactable's feedback is waiting for the blade
    private float _usedAngle;
    private Vector2 _usedPoint;

    private readonly List<Collider2D> _colliderHits = new List<Collider2D>(64);
    private readonly List<IAxeHittable> _componentBuffer = new List<IAxeHittable>(4);
    private readonly HashSet<IAxeHittable> _seen = new HashSet<IAxeHittable>();
    private readonly List<PendingHit> _pending = new List<PendingHit>(32);

    private struct PendingHit
    {
        public IAxeHittable Target;
        public AxeHit Hit;
        public float Angle; // relative to the swing facing
        public bool Done;
    }

    public float Range => range;
    public float ArcDegrees => arcDegrees;
    public float Cooldown => cooldown;
    public float SwingDuration => swingDuration;
    public float Damage => damage;
    public float Knockback => knockback;

    /// <summary>Center of the swing, on the ground.</summary>
    public Vector2 Origin => transform.position;

    /// <summary>Direction the swing is centered on: the locked aim during a swing, the live aim otherwise.</summary>
    public Vector2 Facing => _swinging ? _swingFacing : LiveAim;

    private Vector2 LiveAim
    {
        get
        {
            if (_controller == null) _controller = GetComponent<PlayerController>();
            Vector2 aim = _controller != null ? _controller.AimDirection : Vector2.right;
            return aim.sqrMagnitude > 0.0001f ? aim.normalized : Vector2.right;
        }
    }

    public bool IsReady => Time.time >= _readyAt && !_swinging;

    /// <summary>Seconds until the next swing is allowed (0 when ready).</summary>
    public float CooldownRemaining => Mathf.Max(0f, _readyAt - Time.time);

    /// <summary>True while the blade is sweeping.</summary>
    public bool IsSwinging => _swinging;

    /// <summary>0 at the start of the sweep, 1 at the end.</summary>
    public float SwingProgress => _swinging ? _progress : 0f;

    /// <summary>+1 if this swing travels counter-clockwise, -1 if clockwise.</summary>
    public float SwingSign => _endAngle >= _startAngle ? 1f : -1f;

    /// <summary>Where the blade is pointing right now (on the ground plane, normalized).</summary>
    public Vector2 BladeDirection => Rotate(_swingFacing, _startAngle + _sweptAngle);

    /// <summary>Fired when a swing starts. Good for a whoosh sound or starting the trail.</summary>
    public event Action SwingStarted;

    /// <summary>Fired each time the blade connects with something, as it happens.
    /// Hook hit-freeze, impact sounds and sparks up to this.</summary>
    public event Action<AxeHit, IAxeHittable, AxeHitResult> TargetHit;

    /// <summary>Fired once per successful parry, with the swing details and what was parried.</summary>
    public event Action<AxeHit, IAxeHittable> Parried;

    /// <summary>Fired when the blade reaches the locker / water source this swing used, with the
    /// hit point. (The interaction itself already happened at the start of the swing; this is
    /// for timing feedback like hit-freeze and clang sounds to the visual.)</summary>
    public event Action<Interactable, Vector2> InteractableHit;

    /// <summary>Fired when a swing finishes, with a summary of what happened.</summary>
    public event Action<AxeSwingResult> Swung;

    private void Awake()
    {
        _controller = GetComponent<PlayerController>();
        _interactor = GetComponent<PlayerInteractor>();
        _health = GetComponent<PlayerHealth>();
    }

    private void OnDisable()
    {
        if (_swinging) FinishSwing();
    }

    /// <summary>Starts a swing if the cooldown allows. Returns true if a swing started.</summary>
    public bool TrySwing()
    {
        if (!IsReady) return false;
        if (_health != null && _health.IsDead) return false;

        _readyAt = Time.time + cooldown;
        _swinging = true;
        _progress = 0f;
        _swingFacing = LiveAim;
        _result = new AxeSwingResult();

        float half = arcDegrees * 0.5f;
        bool reversed = alternateDirection && _nextSwingReversed;
        _startAngle = reversed ? half : -half;
        _endAngle = reversed ? -half : half;
        _sweptAngle = 0f;
        _nextSwingReversed = !_nextSwingReversed;

        // Utility first and instantly, so lockers and hoses feel responsive.
        _usedPending = false;
        if (_interactor != null && _interactor.TryInteract(out Interactable used))
        {
            _result.Used = used;
            _usedPoint = used.ClosestPoint(Origin);
            Vector2 to = _usedPoint - Origin;
            _usedAngle = to.sqrMagnitude < 0.0025f ? _startAngle : Vector2.SignedAngle(_swingFacing, to);
            _usedPending = true;
        }

        CollectTargets();
        SwingStarted?.Invoke();

        ResolvePassedTargets(); // anything right on top of the player gets hit on frame one
        return true;
    }

    /// <summary>Lets the next swing happen immediately (e.g. on respawn).</summary>
    public void ResetCooldown() => _readyAt = 0f;

    private void Update()
    {
        if (!_swinging) return;
        if (_health != null && _health.IsDead) { FinishSwing(); return; }

        // Scaled time: a hit-freeze (timeScale 0) pauses the blade mid-swing.
        _progress = Mathf.Min(1f, _progress + Time.deltaTime / swingDuration);
        _sweptAngle = (_endAngle - _startAngle) * Mathf.Clamp01(swingCurve.Evaluate(_progress));
        ResolvePassedTargets();

        if (_progress >= 1f) FinishSwing();
    }

    private void FinishSwing()
    {
        _sweptAngle = _endAngle - _startAngle;
        ResolvePassedTargets(); // never miss something because of a big frame
        _pending.Clear();
        _usedPending = false;
        _swinging = false;

        if (_result.Parries > 0 && parryRefundsCooldown)
            _readyAt = Time.time;

        Swung?.Invoke(_result);
    }

    // Collect every target at the start, then hit them as the blade reaches them. Damage can
    // despawn enemies (returning them to a pool), which mustn't disturb the search.
    private void CollectTargets()
    {
        Vector2 origin = Origin;
        Vector2 facing = _swingFacing;

        _pending.Clear();
        _seen.Clear();

        // Horde movement writes Transforms directly, so queries need current physics poses.
        Physics2D.SyncTransforms();
        var filter = new ContactFilter2D { useTriggers = true };
        filter.SetLayerMask(hitLayers);
        Physics2D.OverlapCircle(origin, range, filter, _colliderHits);

        for (int i = 0; i < _colliderHits.Count; i++)
        {
            Collider2D col = _colliderHits[i];
            if (col == null || col.transform.IsChildOf(transform)) continue; // don't hit yourself

            Vector2 point = col.ClosestPoint(origin);
            if (!IsInArc(origin, facing, range, arcDegrees, point, 0f, out float distance)) continue;

            bool onTop = distance <= 0.05f;
            Vector2 direction = onTop ? facing : (point - origin) / distance;
            // Things right on top of the player are hit at the very start of the sweep.
            float angle = onTop ? _startAngle : Vector2.SignedAngle(facing, direction);
            var hit = new AxeHit(this, origin, facing, direction, point, damage, knockback);

            col.GetComponentsInParent(false, _componentBuffer);
            for (int c = 0; c < _componentBuffer.Count; c++)
                if (_seen.Add(_componentBuffer[c]))
                    _pending.Add(new PendingHit { Target = _componentBuffer[c], Hit = hit, Angle = angle });
        }
    }

    private void ResolvePassedTargets()
    {
        float sign = SwingSign;
        float bladeAngle = _startAngle + _sweptAngle;

        if (_usedPending && (bladeAngle - _usedAngle) * sign >= -0.001f)
        {
            _usedPending = false;
            if (_result.Used != null) InteractableHit?.Invoke(_result.Used, _usedPoint);
        }

        for (int i = 0; i < _pending.Count; i++)
        {
            PendingHit p = _pending[i];
            if (p.Done) continue;
            // Has the blade reached this target's angle yet?
            if ((bladeAngle - p.Angle) * sign < -0.001f) continue;

            p.Done = true;
            _pending[i] = p;

            if (p.Target is UnityEngine.Object obj && obj == null) continue; // destroyed by an earlier hit
            if (p.Target is Behaviour b && !b.isActiveAndEnabled) continue;  // e.g. despawned to a pool

            AxeHitResult outcome = p.Target.OnAxeHit(p.Hit);
            switch (outcome)
            {
                case AxeHitResult.Hit:
                    _result.Hits++;
                    break;
                case AxeHitResult.Parried:
                    _result.Parries++;
                    Parried?.Invoke(p.Hit, p.Target);
                    break;
            }
            if (outcome != AxeHitResult.Ignored)
                TargetHit?.Invoke(p.Hit, p.Target, outcome);

            if (!_swinging) return; // a handler ended the swing (e.g. the player died)
        }
    }

    /// <summary>
    /// Shared arc test (also used for Interactables). <paramref name="point"/> is the nearest point
    /// of the target; <paramref name="padding"/> grows the reach for targets measured from their center.
    /// </summary>
    public static bool IsInArc(Vector2 origin, Vector2 facing, float range, float arcDegrees,
                               Vector2 point, float padding, out float distance)
    {
        Vector2 toTarget = point - origin;
        distance = toTarget.magnitude;

        if (distance > range + padding) return false;
        // Standing on/inside it: there's no meaningful direction, so it counts.
        if (distance <= padding + 0.05f || arcDegrees >= 360f) return true;

        return Vector2.Angle(facing, toTarget) <= arcDegrees * 0.5f;
    }

    private static Vector2 Rotate(Vector2 v, float degrees) => Quaternion.Euler(0f, 0f, degrees) * v;

#if UNITY_EDITOR
    private void OnDrawGizmos()
    {
        if (alwaysShowGizmo) DrawHitbox();
    }

    private void OnDrawGizmosSelected()
    {
        if (!alwaysShowGizmo) DrawHitbox();
    }

    private void DrawHitbox()
    {
        bool playing = Application.isPlaying;
        Vector2 facing = playing ? Facing : Vector2.right;

        bool ready = !playing || IsReady;
        Color color = _swinging ? new Color(1f, 0.25f, 0.15f)
                    : ready ? new Color(1f, 0.6f, 0.1f)
                    : new Color(0.6f, 0.6f, 0.6f);

        Vector3 center = transform.position;
        Vector3 from = Rotate(facing, -arcDegrees * 0.5f);
        Vector3 to = Rotate(facing, arcDegrees * 0.5f);

        UnityEditor.Handles.color = new Color(color.r, color.g, color.b, _swinging ? 0.3f : 0.12f);
        UnityEditor.Handles.DrawSolidArc(center, Vector3.forward, from, arcDegrees, range);

        UnityEditor.Handles.color = new Color(color.r, color.g, color.b, 0.9f);
        UnityEditor.Handles.DrawWireArc(center, Vector3.forward, from, arcDegrees, range);
        UnityEditor.Handles.DrawLine(center, center + from * range);
        UnityEditor.Handles.DrawLine(center, center + to * range);

        if (_swinging) // the blade's current position
            UnityEditor.Handles.DrawLine(center, center + (Vector3)BladeDirection * range);
    }
#endif
}