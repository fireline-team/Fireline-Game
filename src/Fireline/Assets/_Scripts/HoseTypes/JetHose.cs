using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

/// <summary>
/// The sniper: a long, narrow blast that fires in pulses. Each pulse hits everything
/// in the line hard and knocks it back, then the nozzle has to recharge.
/// </summary>
public class JetHose : HoseWeapon
{
    [Header("Jet Nozzle")]
    [SerializeField, Min(0f)] private float damagePerShot = 15f;
    [Tooltip("Seconds between shots while the button is held.")]
    [SerializeField, Min(0.05f)] private float fireInterval = 0.5f;
    [Tooltip("Knockback speed given to each enemy hit. It then wears off over time.")]
    [SerializeField, Min(0f)] private float knockbackImpulse = 6f;
    [Tooltip("How many water blobs each shot throws out.")]
    [SerializeField, Min(1)] private int particlesPerShot = 18;

    private float _nextFireTime;

    /// <summary>True if a shot is ready. Useful for a charge-up UI or sound.</summary>
    public bool IsReady => Time.time >= _nextFireTime;

    // Called by Unity when this component is first added in the Inspector:
    // long and narrow.
    private void Reset() => SetHitboxSize(9f, 0.25f);

    protected override void OnSpray(float deltaTime)
    {
        // The cooldown keeps running after release, so tapping the button can't fire faster.
        if (!IsReady) return;

        _nextFireTime = Time.time + fireInterval;
        PulseStream(particlesPerShot);

        List<HordeEnemy> hits = FindEnemiesInHitbox();
        Vector2 knock = AimDirection * knockbackImpulse;

        for (int i = 0; i < hits.Count; i++)
        {
            hits[i].ApplyKnockback(knock);
            hits[i].TakeDamage(damagePerShot); // last, since it can despawn the enemy
        }

        // Each pulse delivers a whole interval's worth of water, so over time the jet
        // puts out fires at its Extinguish Per Second rate like the other nozzles.
        WaterFiresInHitbox(ExtinguishPerSecond * fireInterval);
    }

    // The jet only shows water when it fires (PulseStream above), not while held.
    protected override bool ShowsContinuousStream() => false;
}
