using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

public class JetHose : HoseWeapon
{
    [Header("Jet Nozzle")]
    [SerializeField, Min(0f)] private float damagePerShot = 15f;
    [Tooltip("Seconds between shots while the button is held.")]
    [SerializeField, Min(0.05f)] private float fireInterval = 0.5f;
    [Tooltip("Knockback speed given to each enemy hit. It then wears off over time.")]
    [SerializeField, Min(0f)] private float knockbackImpulse = 6f;
    [Tooltip("How long the stream stays visible after each shot, in seconds.")]
    [SerializeField, Min(0f)] private float flashDuration = 0.12f;

    private float _nextFireTime;
    private float _lastShotTime = float.NegativeInfinity;
    
    public bool IsReady => Time.time >= _nextFireTime;
    
    private void Reset() => SetHitboxSize(9f, 0.25f);

    protected override void OnSpray(float deltaTime)
    {
        if (!IsReady) return;

        _nextFireTime = Time.time + fireInterval;
        _lastShotTime = Time.time;

        List<HordeEnemy> hits = FindEnemiesInHitbox();
        Vector2 knock = AimDirection * knockbackImpulse;

        for (int i = 0; i < hits.Count; i++)
        {
            hits[i].ApplyKnockback(knock);
            hits[i].TakeDamage(damagePerShot);
        }
    }

    protected override bool ShouldShowStream() => Time.time - _lastShotTime < flashDuration;
}
