using System;
using UnityEngine;

/// <summary>
/// (1) DATA: one player's water tank. Just stores the numbers and announces changes.
///
/// Doesn't decide anything: WaterTankController changes these values (refilling, spraying),
/// and UI like WaterTankGauge listens to Changed to redraw. Plain C# class, not a component.
/// </summary>
public class WaterTank
{
    private float _amount;
    private float _capacity;
    private bool _isConnected;

    /// <summary>Fired after any value below changes. Listeners re-read what they need.</summary>
    public event Action Changed;

    /// <summary>
    /// Fired when the player tries to spray but there isn't enough water. Not a value change,
    /// so it's its own event. Fires every frame the trigger is held on an empty tank.
    /// </summary>
    public event Action EmptySprayAttempted;

    public WaterTank(float capacity)
    {
        _capacity = Mathf.Max(0.01f, capacity);
        _amount = _capacity;
    }

    /// <summary>Water in the tank right now, 0 to Capacity.</summary>
    public float Amount
    {
        get => _amount;
        set => Set(ref _amount, Mathf.Clamp(value, 0f, _capacity));
    }

    /// <summary>Most water the tank holds. Lowering it below Amount spills the extra.</summary>
    public float Capacity
    {
        get => _capacity;
        set
        {
            if (!Set(ref _capacity, Mathf.Max(0.01f, value))) return;
            if (_amount > _capacity) Amount = _capacity;
        }
    }

    /// <summary>Hooked up to a hydrant right now.</summary>
    public bool IsConnected
    {
        get => _isConnected;
        set
        {
            if (_isConnected == value) return;
            _isConnected = value;
            Changed?.Invoke();
        }
    }

    /// <summary>How full, 0 to 1. Handy for bars.</summary>
    public float Fraction => _amount / _capacity;
    public bool IsEmpty => _amount <= 0f;
    public bool IsFull => _amount >= _capacity;

    /// <summary>Announces a failed spray. Called by WaterTankController.</summary>
    public void ReportEmptySpray() => EmptySprayAttempted?.Invoke();

    private bool Set(ref float field, float value)
    {
        if (field == value) return false; // no change, no event
        field = value;
        Changed?.Invoke();
        return true;
    }
}