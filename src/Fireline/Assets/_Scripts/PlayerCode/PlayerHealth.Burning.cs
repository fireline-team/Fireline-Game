using System;
using UnityEngine;

public partial class PlayerHealth
{
    [Header("Burning")]
    [Tooltip("Accumulated enemy damage needed to ignite. Blocked hits do not build fire.")]
    [SerializeField, Min(1f)] private float damageToIgnite = 30f;
    [Tooltip("Water units teammates must apply to extinguish a burning player.")]
    [SerializeField, Min(1f)] private float waterToExtinguish = 45f;
    [SerializeField, Min(0f)] private float burningDamagePerSecond = 5f;
    private float fireBuildup;
    private float extinguishingWater;

    public bool IsBurning { get; private set; }
    public float FireBuildup => fireBuildup;
    public float FireBuildupProgress => Mathf.Clamp01(fireBuildup / Mathf.Max(1f, damageToIgnite));
    public float ExtinguishProgress => IsBurning ? Mathf.Clamp01(extinguishingWater / Mathf.Max(1f, waterToExtinguish)) : 0f;
    /// <summary>Current buildup and ignition threshold, in damage units.</summary>
    public event Action<float, float> FireBuildupChanged;
    /// <summary>Accumulated extinguishing water and required water units.</summary>
    public event Action<float, float> ExtinguishProgressChanged;
    public event Action Ignited;
    public event Action Extinguished;

    public void TakeEnemyDamage(float amount)
    {
        if (!isActiveAndEnabled || IsDead || IsInvulnerable || amount <= 0f
            || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        float before = CurrentHealth;
        nextContactTime = Time.time + contactDamageCooldown;
        TakeDamage(amount);
        if (IsDead || IsBurning || CurrentHealth >= before) return;
        fireBuildup = Mathf.Min(Mathf.Max(1f, damageToIgnite), fireBuildup + before - CurrentHealth);
        bool ignite = FireBuildupProgress >= 1f;
        if (ignite) IsBurning = true;
        FireBuildupChanged?.Invoke(fireBuildup, Mathf.Max(1f, damageToIgnite));
        if (ignite) Ignited?.Invoke();
    }

    private void UpdateBurning()
    {
        if (!IsBurning || IsDead || Fireline.Missions.GameManager.BlocksGameplay) return;
        // Existing contact immunity does not suppress the ongoing burn.
        TakeDamage(Mathf.Max(0f, burningDamagePerSecond) * Time.deltaTime);
    }

    public void ApplyExtinguishingWater(PlayerHealth teammate, float amount)
    {
        if (!isActiveAndEnabled || IsDead || !IsBurning || teammate == null || teammate == this
            || !teammate.isActiveAndEnabled || teammate.IsDead || teammate.gameObject.scene != gameObject.scene
            || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount)) return;
        extinguishingWater = Mathf.Min(Mathf.Max(1f, waterToExtinguish), extinguishingWater + amount);
        ExtinguishProgressChanged?.Invoke(extinguishingWater, Mathf.Max(1f, waterToExtinguish));
        if (ExtinguishProgress >= 1f) Extinguish();
    }

    /// <summary>Successful rolls and sufficient teammate water clear burning and reset buildup.</summary>
    public void Extinguish()
    {
        if (IsBurning) ClearFire();
    }

    private void ClearFire()
    {
        bool wasBurning = IsBurning;
        bool hadBuildup = fireBuildup > 0f;
        IsBurning = false;
        fireBuildup = 0f;
        extinguishingWater = 0f;
        if (hadBuildup) FireBuildupChanged?.Invoke(0f, Mathf.Max(1f, damageToIgnite));
        if (wasBurning)
        {
            ExtinguishProgressChanged?.Invoke(0f, Mathf.Max(1f, waterToExtinguish));
            Extinguished?.Invoke();
        }
    }
}
