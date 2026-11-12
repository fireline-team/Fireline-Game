using System;
using UnityEngine;
using UnityEngine.InputSystem;

public partial class PlayerController
{
    [Header("Combat roll")]
    [SerializeField, Range(0.01f, 0.99f)] private float rollDuration = 0.25f;
    [Tooltip("Damage immunity in seconds from the start of the roll, independent of movement duration.")]
    [SerializeField, Range(0.01f, 2f)] private float immunityDuration = 0.5f;
    [SerializeField, Min(1f)] private float rollSpeedMultiplier = 3f;
    [Tooltip("Seconds after the roll ends before another roll is available.")]
    [SerializeField, Min(0f)] private float rollCooldown = 1.5f;

    private InputAction rollAction;
    private Vector2 rollDirection;
    private float rollEndsAt;
    private float immunityEndsAt;
    private float rollReadyAt;
    private bool cooldownPending;
    private SpriteRenderer[] rollSprites;
    private Material[] rollMaterials;

    public bool IsRolling { get; private set; }
    public float RollCooldownRemaining => Mathf.Max(0f, rollReadyAt - Time.time);
    public bool CanCombatRoll => isActiveAndEnabled && !health.IsDead
        && !Fireline.Missions.GameManager.BlocksGameplay && !IsRolling && RollCooldownRemaining <= 0f;

    public event Action RollStarted;
    public event Action RollEnded;
    /// <summary>Remaining seconds until ready, and total cooldown seconds. Starts after the dash.</summary>
    public event Action<float, float> RollCooldownChanged;
    public event Action RollReady;

    public bool TryCombatRoll(Vector2 direction)
    {
        if (!CanCombatRoll || direction.sqrMagnitude < 0.01f) return false;
        rollDirection = direction.normalized;
        rollEndsAt = Time.time + Mathf.Clamp(rollDuration, 0.01f, 0.99f);
        rollReadyAt = rollEndsAt + Mathf.Max(0f, rollCooldown);
        IsRolling = true;
        EndRollImmunity();
        immunityEndsAt = Time.time + Mathf.Clamp(immunityDuration, 0.01f, 2f);
        health.RollInvulnerable = true;
        health.Extinguish();
        var flash = Resources.Load<Material>("CombatRollFlash");
        rollSprites = GetComponentsInChildren<SpriteRenderer>(true);
        rollMaterials = new Material[rollSprites.Length];
        for (int i = 0; i < rollSprites.Length; i++)
        {
            rollMaterials[i] = rollSprites[i].sharedMaterial;
            if (flash != null) rollSprites[i].sharedMaterial = flash;
        }
        RollStarted?.Invoke();
        return true;
    }

    private void UpdateRoll()
    {
        bool cancelled = health.IsDead || Fireline.Missions.GameManager.BlocksGameplay;
        if (health.RollInvulnerable && (Time.time >= immunityEndsAt || cancelled))
            EndRollImmunity();
        if (IsRolling && (Time.time >= rollEndsAt || cancelled))
            EndRoll();
        if (!cooldownPending) return;
        float remaining = RollCooldownRemaining;
        RollCooldownChanged?.Invoke(remaining, Mathf.Max(0f, rollCooldown));
        if (remaining > 0f) return;
        cooldownPending = false;
        RollReady?.Invoke();
    }

    private void EndRollImmunity()
    {
        if (health == null || !health.RollInvulnerable) return;
        health.RollInvulnerable = false;
        for (int i = 0; i < rollSprites.Length; i++)
            if (rollSprites[i] != null) rollSprites[i].sharedMaterial = rollMaterials[i];
    }

    private void EndRoll()
    {
        if (!IsRolling) return;
        IsRolling = false;
        rb.linearVelocity = Vector2.zero;
        rollReadyAt = Time.time + Mathf.Max(0f, rollCooldown);
        cooldownPending = true;
        RollEnded?.Invoke();
        RollCooldownChanged?.Invoke(RollCooldownRemaining, Mathf.Max(0f, rollCooldown));
    }
}
