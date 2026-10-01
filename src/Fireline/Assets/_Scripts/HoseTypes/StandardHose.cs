using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

public class StandardHose : HoseWeapon
{
    [Header("Standard Nozzle")]
    [SerializeField, Min(0f)] private float damagePerSecond = 20f;
    [Tooltip("How hard the stream shoves enemies away while they're in it. Enemies walking at speed 2 get pushed back once this is above roughly 2 x the HordeManager's Knockback Damping.")]
    [SerializeField, Min(0f)] private float knockbackStrength = 24f;

    private void Reset() => SetHitboxSize(5f, 0.6f);

    protected override void OnSpray(float deltaTime)
    {
        List<HordeEnemy> hits = FindEnemiesInHitbox();
        Vector2 push = AimDirection * (knockbackStrength * deltaTime);
        float damage = damagePerSecond * deltaTime;

        for (int i = 0; i < hits.Count; i++)
        {
            hits[i].ApplyKnockback(push);
            hits[i].TakeDamage(damage);
        }

        WaterFiresInHitbox(ExtinguishPerSecond * deltaTime);
    }
}