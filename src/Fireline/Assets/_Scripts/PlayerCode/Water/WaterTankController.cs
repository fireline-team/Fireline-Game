using UnityEngine;

/// <summary>
/// (2) UPDATES THE DATA: owns this player's WaterTank and is the only thing that changes it.
///
/// While the hose is connected to a hydrant the tank refills, and spraying is free.
/// Detached, every spray draws from the tank, so the player can still fight until it runs dry.
/// Nozzles ask this component for water (see HoseWeapon.UseWater / TryUseWater).
///
/// Put it on the player. If it's missing, the hose adds one with default settings.
/// </summary>
[DisallowMultipleComponent]
public class WaterTankController : MonoBehaviour
{
    [Tooltip("Most water the tank holds.")]
    [SerializeField, Min(1f)] private float capacity = 100f;
    [Tooltip("Water added per second while connected to a hydrant.")]
    [SerializeField, Min(0f)] private float refillPerSecond = 25f;
    [Tooltip("OFF: spraying is free while on a hydrant. ON: spraying always uses the tank, and the hydrant only refills it.")]
    [SerializeField] private bool useTankWhileConnected;

    /// <summary>The data. Read it or listen to it; change it only through this component.</summary>
    public WaterTank Tank { get; private set; }

    /// <summary>Can anything come out of the nozzle right now?</summary>
    public bool HasWater
    {
        get
        {
            RefreshConnection();
            return IsFree || !Tank.IsEmpty;
        }
    }

    private bool IsFree => Tank.IsConnected && !useTankWhileConnected;

    private void Awake()
    {
        Tank = new WaterTank(capacity);
    }

    private void Update()
    {
        RefreshConnection();
        if (Tank.IsConnected && !Tank.IsFull)
            Tank.Amount += refillPerSecond * Time.deltaTime;
    }

    /// <summary>
    /// For continuous nozzles (standard, mist): takes up to this much water.
    /// The last frames before running dry use whatever is left.
    /// </summary>
    public void Use(float amount)
    {
        RefreshConnection();
        if (amount <= 0f || IsFree) return;
        Tank.Amount -= amount;
    }

    /// <summary>
    /// For shots that need a full charge (the jet). Takes the water only if there's enough.
    /// </summary>
    /// <returns>True if the shot can fire.</returns>
    public bool TryUse(float amount)
    {
        RefreshConnection();
        if (IsFree || amount <= 0f) return true;
        if (Tank.Amount < amount) return false;
        Tank.Amount -= amount;
        return true;
    }

    /// <summary>Tops the tank off (respawn, pickups, cheats).</summary>
    public void Fill() => Tank.Amount = Tank.Capacity;

    // "Connected" means water is flowing to this player: hooked to a hydrant, or a scene
    // whose HoseWaterRules don't require one (legacy scenes keep unlimited water).
    private void RefreshConnection()
    {
        Tank.IsConnected = HoseWaterRules.CanSpray(gameObject);
    }

    // Lets you tune capacity in the Inspector during Play Mode.
    private void OnValidate()
    {
        if (Tank != null) Tank.Capacity = capacity;
    }
}
