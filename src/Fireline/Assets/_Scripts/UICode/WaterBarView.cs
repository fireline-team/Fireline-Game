using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// (3) UI THAT LISTENS: draws a player's WaterTank as a bar. Same look and feel as the health bar.
///
/// It never polls the tank. It subscribes to WaterTank.Changed, works out what to show when
/// something changes, and Update only animates toward that. It never changes the tank.
///
/// Put it on the water bar in the HUD panel and drag in the fill images. PlayerHud calls Bind()
/// with the right player's tank.
/// </summary>
public class WaterBarView : MonoBehaviour
{
    [Tooltip("The blue fill. Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left.")]
    [SerializeField] private Image fill;
    [Tooltip("Optional. A lighter bar BEHIND the fill (same settings) that drains slowly, so you can see how much a shot just used.")]
    [SerializeField] private Image trail;
    [SerializeField] private Color waterColor = new Color(0.2f, 0.55f, 1f);
    [Tooltip("Shown while a hydrant is refilling the tank.")]
    [SerializeField] private Color refillingColor = new Color(0.55f, 0.85f, 1f);
    [Tooltip("Below this fraction (and off a hydrant) the bar pulses.")]
    [SerializeField, Range(0f, 1f)] private float lowPulseBelow = 0.25f;
    [Tooltip("How fast the fill catches up to the real value. Higher = snappier.")]
    [SerializeField, Min(1f)] private float fillSpeed = 12f;
    [Tooltip("Seconds the trail waits before draining.")]
    [SerializeField, Min(0f)] private float trailDelay = 0.4f;
    [SerializeField, Min(0.1f)] private float trailSpeed = 1.5f;

    private WaterTank _tank;
    private bool _subscribed;

    // What the tank last told us. Only OnTankChanged writes these.
    private float _target = 1f;
    private bool _refilling;
    private bool _low;

    // Animation state.
    private float _shown = 1f;
    private float _trail = 1f;
    private float _trailWait;

    /// <summary>Shows this tank. Pass null to clear. Safe to call again with a different tank.</summary>
    public void Bind(WaterTank tank)
    {
        Unsubscribe();
        _tank = tank;
        if (isActiveAndEnabled) Subscribe();

        // Jump straight to the new tank's level instead of animating from the old one.
        _shown = _trail = _target;
        Draw();
    }

    private void OnEnable() => Subscribe();
    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_subscribed || _tank == null) return;
        _tank.Changed += OnTankChanged;
        _subscribed = true;
        OnTankChanged(); // pick up the current state right away
    }

    private void Unsubscribe()
    {
        if (!_subscribed) return;
        _tank.Changed -= OnTankChanged;
        _subscribed = false;
    }

    // The data changed: decide what the bar should show.
    private void OnTankChanged()
    {
        float target = _tank.Fraction;
        if (target < _target - 0.0001f) _trailWait = trailDelay; // water was used: hold the trail
        _target = target;
        _refilling = _tank.IsConnected && !_tank.IsFull;
        _low = !_tank.IsConnected && target <= lowPulseBelow;
    }

    // Animation only. UI runs on unscaled time so hit-stop doesn't freeze it.
    private void Update()
    {
        if (_tank == null) return;
        float dt = Time.unscaledDeltaTime;

        _shown = Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-fillSpeed * dt));

        // Trail: holds after water is used, then drains to the real value. Snaps up on refills.
        if (_target > _trail) _trail = _target;
        else if (_trailWait > 0f) _trailWait -= dt;
        else _trail = Mathf.MoveTowards(_trail, _target, trailSpeed * dt);

        Draw();
    }

    private void Draw()
    {
        if (trail != null) trail.fillAmount = _trail;
        if (fill == null) return;

        Color color = _refilling ? refillingColor : waterColor;
        if (_low && _target > 0f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            color = Color.Lerp(waterColor, Color.white, pulse * 0.45f);
        }
        fill.fillAmount = _shown;
        fill.color = color;
    }
}
