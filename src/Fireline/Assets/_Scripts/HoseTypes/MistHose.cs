using System.Collections.Generic;
using Game.Runtime;
using UnityEngine;

public class MistHose : HoseWeapon
{
    [Header("Mist Nozzle")]
    [SerializeField, Min(0f)] private float damagePerSecond = 8f;
    [Tooltip("Speed multiplier while slowed. 0.4 = enemies move at 40% speed.")]
    [SerializeField, Range(0.05f, 1f)] private float slowMultiplier = 0.4f;
    [Tooltip("How long the slow lasts after an enemy leaves the mist, in seconds.")]
    [SerializeField, Min(0f)] private float slowDuration = 1f;

    private void Reset() => SetHitboxSize(2.5f, 3f);

    protected override void OnSpray(float deltaTime)
    {
        List<HordeEnemy> hits = FindEnemiesInHitbox();
        float damage = damagePerSecond * deltaTime;

        for (int i = 0; i < hits.Count; i++)
        {
            hits[i].ApplySlow(slowMultiplier, slowDuration);
            hits[i].TakeDamage(damage);
        }

        WaterFiresInHitbox(ExtinguishPerSecond * deltaTime);
    }
}