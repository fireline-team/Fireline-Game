using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

/// <summary>
/// Drives one player's HUD panel: health bar, hose ring, axe cooldown and portrait.
/// Put this on the root of the HUD panel prefab and drag the pieces in.
/// PlayerHudManager spawns one per player and calls Bind(); you can also call Bind() yourself.
///
/// Every reference is optional, so the panel still works while art is half-finished.
/// </summary>
public class PlayerHud : MonoBehaviour
{
    [Header("Health Bar")]
    [Tooltip("The red fill. Image Type = Filled, Fill Method = Horizontal, Fill Origin = Left. Sits on top of the grey bar.")]
    [SerializeField] private Image healthFill;
    [Tooltip("Optional. A lighter bar BEHIND the red fill (same settings) that drains slowly after a hit, so you can see how much you just lost.")]
    [SerializeField] private Image healthTrail;
    [SerializeField] private Color healthColor = new Color(0.85f, 0.12f, 0.1f);
    [Tooltip("While downed, the bar shows revive progress in this color instead (water refilling you).")]
    [SerializeField] private Color reviveColor = new Color(0.25f, 0.65f, 1f);
    [Tooltip("Below this fraction of health the bar pulses.")]
    [SerializeField, Range(0f, 1f)] private float lowHealthPulseBelow = 0.25f;
    [Tooltip("How fast the red fill catches up to the real value. Higher = snappier.")]
    [SerializeField, Min(1f)] private float fillSpeed = 12f;
    [Tooltip("Seconds the trail waits before draining.")]
    [SerializeField, Min(0f)] private float trailDelay = 0.4f;
    [SerializeField, Min(0.1f)] private float trailSpeed = 1.5f;

    [Header("Hose Ring")]
    [Tooltip("The red coiled hose. Spins when the player switches nozzles.")]
    [SerializeField] private RectTransform hoseRing;
    [SerializeField, Min(0.05f)] private float hoseSpinDuration = 0.45f;
    [Tooltip("Full turns per switch. Negative spins clockwise.")]
    [SerializeField] private float hoseSpinTurns = -1f;
    [Tooltip("Optional. Tints the hose image per nozzle (Standard, Mist, Jet). Leave empty to keep its color.")]
    [SerializeField] private Image hoseImage;
    [SerializeField] private Color[] nozzleTints = new Color[0];

    [Header("Axe Cooldown")]
    [Tooltip("The axe icon itself.")]
    [SerializeField] private Image axeIcon;
    [Tooltip("A copy of the axe icon on top of it: same sprite, dark see-through color, Image Type = Filled (Vertical, origin Top). It drains away as the cooldown finishes.")]
    [SerializeField] private Image axeCooldownOverlay;
    [Tooltip("Axe icon color while on cooldown (multiplied with the art).")]
    [SerializeField] private Color axeCoolingTint = new Color(0.6f, 0.6f, 0.6f, 1f);
    [Tooltip("Size bump when the axe is ready again.")]
    [SerializeField, Min(1f)] private float axeReadyPop = 1.25f;

    [Header("Portrait")]
    [SerializeField] private Image portrait;
    [Tooltip("Expressions from healthy to worst. Picked by health: [0] above 66%, [1] above 33%, [2] below that. Any number works; they split the range evenly.")]
    [SerializeField] private Sprite[] healthExpressions = new Sprite[0];
    [Tooltip("Shown briefly whenever the player takes damage. Optional.")]
    [SerializeField] private Sprite hitExpression;
    [Tooltip("Shown while downed. Optional.")]
    [SerializeField] private Sprite downedExpression;
    [SerializeField, Min(0f)] private float hitExpressionTime = 0.35f;

    [Header("Hit Shake")]
    [Tooltip("What shakes when the player is hurt, e.g. the badge (portrait included). Optional.")]
    [SerializeField] private RectTransform shakeTarget;
    [SerializeField, Min(0f)] private float shakeStrength = 8f;
    [SerializeField, Min(0f)] private float shakeTime = 0.25f;

    [Header("Status Text (optional)")]
    [Tooltip("Shows 'DOWNED' / 'Press E to restart' etc. Replaces the old debug box.")]
    [SerializeField] private TMP_Text statusText;
    [Tooltip("Optional label like 'P1'.")]
    [SerializeField] private TMP_Text playerLabel;

    public PlayerHealth Player { get; private set; }

    private HoseLoadout _loadout;
    private AxeSwing _axe;
    private PlayerInput _input;

    private float _shownHealth = 1f;
    private float _trailHealth = 1f;
    private float _trailWait;
    private float _lastHealth = -1f;

    private float _spinTime = -1f;
    private float _spinFrom;
    private float _spinTo;

    private bool _axeWasReady = true;
    private float _axePop;
    private Vector3 _axeBaseScale = Vector3.one;

    private float _hitExpressionLeft;
    private float _shakeLeft;
    private Vector2 _shakeBase;

    /// <summary>Connects this panel to a player. Safe to call again with a different player.</summary>
    public void Bind(PlayerHealth player)
    {
        Unbind();
        Player = player;
        if (player == null) return;

        _loadout = player.GetComponentInChildren<HoseLoadout>();
        _axe = player.GetComponentInChildren<AxeSwing>();
        _input = player.GetComponent<PlayerInput>();

        if (_loadout != null)
        {
            _loadout.Changed += OnNozzleChanged;
            ApplyNozzleTint(_loadout.CurrentType);
        }

        float fraction = HealthFraction();
        _shownHealth = _trailHealth = fraction;
        _lastHealth = player.CurrentHealth;

        if (playerLabel != null && _input != null)
            playerLabel.text = $"P{_input.playerIndex + 1}";
    }

    private void Unbind()
    {
        if (_loadout != null) _loadout.Changed -= OnNozzleChanged;
        _loadout = null;
        _axe = null;
        _input = null;
        Player = null;
    }

    private void Awake()
    {
        if (axeIcon != null) _axeBaseScale = axeIcon.rectTransform.localScale;
        if (shakeTarget != null) _shakeBase = shakeTarget.anchoredPosition;
    }

    private void OnDestroy() => Unbind();

    // UI animates on unscaled time so hit-stop / pause effects don't freeze it.
    private void Update()
    {
        if (Player == null) return;
        float dt = Time.unscaledDeltaTime;

        UpdateHealth(dt);
        UpdateHoseSpin(dt);
        UpdateAxe(dt);
        UpdatePortrait(dt);
        UpdateShake(dt);
        UpdateStatus();
    }

    // ------------------------------------------------------------------ health

    private float HealthFraction() =>
        Player.MaxHealth > 0f ? Mathf.Clamp01(Player.CurrentHealth / Player.MaxHealth) : 0f;

    private void UpdateHealth(float dt)
    {
        // Detect hits by watching health drop (PlayerHealth has no damage event).
        float current = Player.CurrentHealth;
        if (_lastHealth >= 0f && current < _lastHealth - 0.01f)
            OnDamaged();
        _lastHealth = current;

        if (Player.IsDowned)
        {
            // Bar becomes the revive meter.
            SetFill(healthFill, Player.ReviveProgress, reviveColor);
            if (healthTrail != null) healthTrail.fillAmount = 0f;
            _shownHealth = _trailHealth = 0f;
            return;
        }

        float target = HealthFraction();
        _shownHealth = Mathf.Lerp(_shownHealth, target, 1f - Mathf.Exp(-fillSpeed * dt));

        // Trail: holds after a hit, then drains down to the real value. Snaps up on heals.
        if (target > _trailHealth) _trailHealth = target;
        else if (_trailWait > 0f) _trailWait -= dt;
        else _trailHealth = Mathf.MoveTowards(_trailHealth, target, trailSpeed * dt);
        if (healthTrail != null) healthTrail.fillAmount = _trailHealth;

        Color color = healthColor;
        if (target <= lowHealthPulseBelow && target > 0f)
        {
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 10f);
            color = Color.Lerp(healthColor, Color.white, pulse * 0.45f);
        }
        SetFill(healthFill, _shownHealth, color);
    }

    private static void SetFill(Image image, float amount, Color color)
    {
        if (image == null) return;
        image.fillAmount = amount;
        image.color = color;
    }

    private void OnDamaged()
    {
        _trailWait = trailDelay;
        _hitExpressionLeft = hitExpressionTime;
        _shakeLeft = shakeTime;
    }

    // ------------------------------------------------------------------ hose

    private void OnNozzleChanged(NozzleType type)
    {
        float now = hoseRing != null ? hoseRing.localEulerAngles.z : 0f;
        _spinFrom = now;
        _spinTo = now + 360f * hoseSpinTurns;
        _spinTime = 0f;
        ApplyNozzleTint(type);
    }

    private void ApplyNozzleTint(NozzleType type)
    {
        int i = (int)type;
        if (hoseImage != null && nozzleTints != null && i < nozzleTints.Length)
            hoseImage.color = nozzleTints[i];
    }

    private void UpdateHoseSpin(float dt)
    {
        if (hoseRing == null || _spinTime < 0f) return;

        _spinTime += dt;
        float t = Mathf.Clamp01(_spinTime / hoseSpinDuration);
        float eased = 1f - Mathf.Pow(1f - t, 3f); // fast start, soft stop
        hoseRing.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(_spinFrom, _spinTo, eased));
        if (t >= 1f) _spinTime = -1f;
    }

    // ------------------------------------------------------------------ axe

    private void UpdateAxe(float dt)
    {
        if (_axe == null) return;

        float remaining = _axe.Cooldown > 0f ? Mathf.Clamp01(_axe.CooldownRemaining / _axe.Cooldown) : 0f;
        bool ready = remaining <= 0f;

        if (axeCooldownOverlay != null)
        {
            axeCooldownOverlay.enabled = !ready;
            axeCooldownOverlay.fillAmount = remaining;
        }
        if (axeIcon != null)
            axeIcon.color = ready ? Color.white : axeCoolingTint;

        if (ready && !_axeWasReady) _axePop = 1f; // just came off cooldown
        _axeWasReady = ready;

        if (axeIcon != null)
        {
            _axePop = Mathf.MoveTowards(_axePop, 0f, dt * 5f);
            float bump = 1f + (axeReadyPop - 1f) * Mathf.Sin(_axePop * Mathf.PI);
            axeIcon.rectTransform.localScale = _axeBaseScale * bump;
        }
    }

    // ------------------------------------------------------------------ portrait

    private void UpdatePortrait(float dt)
    {
        if (portrait == null) return;
        _hitExpressionLeft -= dt;

        Sprite face = null;
        if (Player.IsDowned) face = downedExpression;
        else if (_hitExpressionLeft > 0f) face = hitExpression;

        if (face == null && healthExpressions != null && healthExpressions.Length > 0)
        {
            // Split 0-1 health evenly: 3 sprites -> above 66%, above 33%, the rest.
            float missing = 1f - HealthFraction();
            int index = Mathf.Min(healthExpressions.Length - 1, Mathf.FloorToInt(missing * healthExpressions.Length));
            face = healthExpressions[index];
        }

        if (face != null && portrait.sprite != face) portrait.sprite = face;
    }

    private void UpdateShake(float dt)
    {
        if (shakeTarget == null) return;
        if (_shakeLeft > 0f)
        {
            _shakeLeft -= dt;
            float strength = shakeStrength * Mathf.Clamp01(_shakeLeft / Mathf.Max(0.01f, shakeTime));
            shakeTarget.anchoredPosition = _shakeBase + Random.insideUnitCircle * strength;
        }
        else shakeTarget.anchoredPosition = _shakeBase;
    }

    // ------------------------------------------------------------------ status text

    private void UpdateStatus()
    {
        if (statusText == null) return;

        if (!Player.IsDowned)
        {
            statusText.text = "";
            return;
        }
        if (Player.CanResetScene)
        {
            bool keyboard = _input != null && _input.currentControlScheme == "KeyboardMouse";
            statusText.text = $"Everyone is down. Press {(keyboard ? "E" : "A")} to restart.";
        }
        else statusText.text = $"DOWNED - spray to revive {Player.ReviveProgress:P0}";
    }
}
