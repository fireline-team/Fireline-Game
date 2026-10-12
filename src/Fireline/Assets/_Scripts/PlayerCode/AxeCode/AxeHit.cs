using UnityEngine;

/// <summary>
/// Implement this on anything that should react to the axe: enemies (damage + knockback),
/// parryable attacks and projectiles, breakable props, shields, etc.
/// Loadout lockers and water sources are NOT this; they're Interactables, and a swing uses
/// only the nearest one so you can't trigger two at once.
/// </summary>
public interface IAxeHittable
{
    AxeHitResult OnAxeHit(in AxeHit hit);
}

public enum AxeHitResult
{
    /// <summary>Not affected (wrong timing, immune, already dead...).</summary>
    Ignored,
    /// <summary>Normal hit: took damage, got pushed, broke, etc.</summary>
    Hit,
    /// <summary>A successful parry. The axe fires its Parried event.</summary>
    Parried
}

/// <summary>Everything a target needs to know about the swing that hit it.</summary>
public readonly struct AxeHit
{
    /// <summary>The axe that swung. Use Axe.gameObject for the player.</summary>
    public readonly AxeSwing Axe;
    /// <summary>Where the swing came from (the player's feet).</summary>
    public readonly Vector2 Origin;
    /// <summary>The direction the player was aiming.</summary>
    public readonly Vector2 Facing;
    /// <summary>From the player toward the hit point, normalized. Use this for knockback so
    /// things get pushed away from the player, not just "forward".</summary>
    public readonly Vector2 Direction;
    /// <summary>The point on the target's collider that was hit.</summary>
    public readonly Vector2 Point;
    public readonly float Damage;
    /// <summary>Knockback speed to apply along Direction.</summary>
    public readonly float Knockback;

    public GameObject Swinger => Axe != null ? Axe.gameObject : null;

    public AxeHit(AxeSwing axe, Vector2 origin, Vector2 facing, Vector2 direction, Vector2 point, float damage, float knockback)
    {
        Axe = axe;
        Origin = origin;
        Facing = facing;
        Direction = direction;
        Point = point;
        Damage = damage;
        Knockback = knockback;
    }
}

public struct AxeSwingResult
{
    public int Hits;
    public int Parries;
    public Interactable Used;

    public bool Whiffed => Hits == 0 && Parries == 0 && Used == null;
}