using System;
using System.Collections;
using Game.Runtime;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// A breakable obstacle: a crate, a pile of rubble, a fallen beam. Put it on a Structure.
///
/// Because it's a Structure, it already blocks players, enemies (HordeNavGrid) and hose water,
/// and the hose wraps around its footprint like any wall. This adds the axe: every hit makes it
/// shake, and after Hits To Break hits it breaks. Breaking switches off the collider, so the
/// hose slips free and enemies get a fresh path through the gap.
///
/// For objectives: listen to Broken (code) or On Broken (Inspector), or check IsBroken.
/// DebrisObjectiveStep does this for mission sites; Restore puts it back for a new run.
/// </summary>
[DisallowMultipleComponent]
[RequireComponent(typeof(Structure))]
public class Debris : MonoBehaviour, IAxeHittable
{
    [Tooltip("Axe hits it takes to break. Each swing counts once, no matter the axe's damage.")]
    [SerializeField, Min(1)] private int hitsToBreak = 3;

    [Header("Hit Shake")]
    [Tooltip("How far the art jiggles on a hit, in world units.")]
    [SerializeField, Min(0f)] private float shakeDistance = 0.08f;
    [Tooltip("How long the shake lasts, in seconds.")]
    [SerializeField, Min(0.01f)] private float shakeDuration = 0.2f;
    [Tooltip("Back-and-forth wobbles per second.")]
    [SerializeField, Min(1f)] private float shakeFrequency = 35f;

    [Header("Breaking")]
    [Tooltip("Optional. Spawned where the debris stood when it breaks (splinters, dust puff...).")]
    [SerializeField] private GameObject breakEffect;
    [Tooltip("Hide the whole object once broken. Turn off to keep it around, e.g. to swap in a rubble sprite from On Broken.")]
    [SerializeField] private bool hideWhenBroken = true;

    [Header("Events")]
    [Tooltip("Every hit that doesn't break it. Good for a thunk sound.")]
    [SerializeField] private UnityEvent onHit = new UnityEvent();
    [Tooltip("The hit that breaks it. Good for a crash sound or opening a path.")]
    [SerializeField] private UnityEvent onBroken = new UnityEvent();

    /// <summary>Fired on each non-breaking hit, with how many hits are left.</summary>
    public event Action<Debris, int> Hit;
    /// <summary>Fired once, when it breaks.</summary>
    public event Action<Debris> Broken;

    public int HitsToBreak => hitsToBreak;
    public int HitsTaken { get; private set; }
    public int HitsRemaining => Mathf.Max(0, hitsToBreak - HitsTaken);
    public bool IsBroken { get; private set; }

    private Collider2D[] _colliders;
    private Transform[] _visuals;
    private Vector3[] _visualRest;
    private Coroutine _shake;

    private void Awake()
    {
        _colliders = GetComponents<Collider2D>();

        // Shake the art (the Structure's child faces), never the root: the root holds the
        // footprint collider, and moving it would nudge players and the hose around.
        _visuals = new Transform[transform.childCount];
        _visualRest = new Vector3[_visuals.Length];
        for (int i = 0; i < _visuals.Length; i++) _visuals[i] = transform.GetChild(i);
    }

    public AxeHitResult OnAxeHit(in AxeHit hit)
    {
        if (IsBroken || !isActiveAndEnabled) return AxeHitResult.Ignored;

        HitsTaken++;
        if (HitsTaken >= hitsToBreak)
        {
            Break();
            return AxeHitResult.Hit;
        }

        Shake(hit.Direction);
        Hit?.Invoke(this, HitsRemaining);
        onHit.Invoke();
        return AxeHitResult.Hit;
    }

    /// <summary>Breaks it right away (also usable from scripts, cutscenes or tests).</summary>
    public void Break()
    {
        if (IsBroken) return;
        IsBroken = true;
        HitsTaken = Mathf.Max(HitsTaken, hitsToBreak);
        StopShake();

        SetBlocking(false);

        if (breakEffect != null) Instantiate(breakEffect, transform.position, Quaternion.identity);

        Broken?.Invoke(this);
        onBroken.Invoke();

        if (hideWhenBroken) gameObject.SetActive(false);
    }

    /// <summary>
    /// Puts it back in one piece: full health, blocking, visible. Used when an objective starts
    /// (or restarts) so a mission always begins with the debris in place.
    /// </summary>
    public void Restore()
    {
        StopShake();
        IsBroken = false;
        HitsTaken = 0;
        gameObject.SetActive(true);
        SetBlocking(true);
    }

    /// <summary>
    /// Shows or hides it without counting as broken, e.g. for objectives at sites the mission
    /// didn't pick. Hidden debris doesn't block anything.
    /// </summary>
    public void SetPresent(bool present)
    {
        if (present)
        {
            gameObject.SetActive(true);
            if (!IsBroken) SetBlocking(true);
            return;
        }
        // Collider off first so the nav grid sees the gap before the object goes inactive.
        SetBlocking(false);
        gameObject.SetActive(false);
    }

    private void SetBlocking(bool blocking)
    {
        if (_colliders == null) _colliders = GetComponents<Collider2D>();
        bool changed = false;
        foreach (Collider2D col in _colliders)
        {
            if (col == null || col.enabled == blocking) continue;
            col.enabled = blocking;
            changed = true;
        }
        if (!changed || HordeNavGrid.Instance == null) return;

        // Re-read the footprint so enemies path through the gap (or around the new obstacle).
        Bounds area = default;
        bool any = false;
        foreach (Collider2D col in _colliders)
        {
            if (col == null) continue;
            // A disabled collider reports empty bounds, so fall back to the shape's own size.
            Bounds b = col is BoxCollider2D box
                ? new Bounds(transform.TransformPoint(box.offset), Vector3.Scale(box.size, transform.lossyScale))
                : col.bounds;
            if (!any) { area = b; any = true; }
            else area.Encapsulate(b);
        }
        if (any) HordeNavGrid.Instance.RebakeArea(area);
    }

    // ---- Shake ------------------------------------------------------------------------------

    private void Shake(Vector2 direction)
    {
        if (shakeDistance <= 0f || _visuals.Length == 0) return;
        StopShake();
        for (int i = 0; i < _visuals.Length; i++)
            if (_visuals[i] != null) _visualRest[i] = _visuals[i].localPosition;
        _shake = StartCoroutine(ShakeRoutine(direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector2.right));
    }

    private IEnumerator ShakeRoutine(Vector2 direction)
    {
        // Wobble along the hit direction, flattened onto the ground plane in local space.
        Vector3 axis = transform.InverseTransformDirection(new Vector3(direction.x, direction.y, 0f));
        for (float t = 0f; t < shakeDuration; t += Time.deltaTime)
        {
            float fade = 1f - t / shakeDuration;
            Vector3 offset = axis * (Mathf.Sin(t * shakeFrequency * Mathf.PI * 2f) * shakeDistance * fade);
            for (int i = 0; i < _visuals.Length; i++)
                if (_visuals[i] != null) _visuals[i].localPosition = _visualRest[i] + offset;
            yield return null;
        }
        RestoreVisuals();
        _shake = null;
    }

    private void StopShake()
    {
        if (_shake == null) return;
        StopCoroutine(_shake);
        _shake = null;
        RestoreVisuals();
    }

    private void RestoreVisuals()
    {
        for (int i = 0; i < _visuals.Length; i++)
            if (_visuals[i] != null) _visuals[i].localPosition = _visualRest[i];
    }

    private void OnDisable() => StopShake();
}