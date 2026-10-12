using System;
using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Drop this on an attack that the axe can parry: an enemy's lunge hitbox, a thrown ember,
/// a boss slam's warning zone. It needs a Collider2D (a trigger is fine) on this object or a child.
///
/// Turn Is Parryable on only during the window the attack can be parried (e.g. from an
/// animation event during the wind-up), or leave it on for things like projectiles.
/// </summary>
public class AxeParryable : MonoBehaviour, IAxeHittable
{
    [Tooltip("Can the axe parry this right now? Toggle it from code or animation events to make a parry window.")]
    [SerializeField] private bool isParryable = true;
    [Tooltip("Deactivate this GameObject when parried (handy for projectiles).")]
    [SerializeField] private bool disableOnParry;
    [Tooltip("Called when the axe parries this.")]
    [SerializeField] private UnityEvent onParried = new UnityEvent();

    /// <summary>Fired on a successful parry, with details of the swing.</summary>
    public event Action<AxeHit> Parried;

    public bool IsParryable
    {
        get => isParryable;
        set => isParryable = value;
    }

    public AxeHitResult OnAxeHit(in AxeHit hit)
    {
        if (!isActiveAndEnabled || !isParryable) return AxeHitResult.Ignored;

        HandleParry(hit);
        Parried?.Invoke(hit);
        onParried.Invoke();

        if (disableOnParry) gameObject.SetActive(false);
        return AxeHitResult.Parried;
    }

    protected virtual void HandleParry(in AxeHit hit) { }
}