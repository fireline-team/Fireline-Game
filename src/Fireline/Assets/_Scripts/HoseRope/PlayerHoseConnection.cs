using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>One source and rope per player, shared by all equipped nozzle types.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(100)]
public class PlayerHoseConnection : MonoBehaviour
{
    private Rigidbody2D body;
    private Color playerColor;
    private float brokenMessageUntil;
    public float Strain01 { get; private set; }

    private void Awake() => body = GetComponent<Rigidbody2D>();

    public HoseRopeSource Source { get; private set; }
    public HoseRope Rope { get; private set; }
    public bool IsConnected => Source != null && Source.IsAvailable && Rope != null;

    public static Color ColorForPlayer(int index)
    {
        switch (Mathf.Max(0, index) % 4)
        {
            case 0: return Color.red;
            case 1: return Color.yellow;
            case 2: return Color.green;
            default: return Color.blue;
        }
    }

    private HoseWeapon CurrentNozzle()
    {
        var loadout = GetComponent<HoseLoadout>();
        return loadout != null ? loadout.Current : GetComponent<HoseWeapon>();
    }

    public bool Attach(HoseRopeSource source)
    {
        var nozzle = CurrentNozzle();
        if (!isActiveAndEnabled || source == null || !source.IsAvailable || nozzle == null
            || source.gameObject.scene != gameObject.scene) return false;
        if (Source == source && IsConnected) return true;
        Detach();
        Source = source;
        Rope = source.CreateRope(this, nozzle.NozzleTransform);
        var input = GetComponent<PlayerInput>();
        playerColor = ColorForPlayer(input != null ? input.playerIndex : 0);
        Rope.SetColor(playerColor);
        brokenMessageUntil = 0f;
        ApplyLengthSettings();
        return true;
    }

    public void Detach()
    {
        if (Source != null) Source.Release(this);
        Source = null;
        Strain01 = 0f;
        if (Rope != null)
        {
            Rope.gameObject.SetActive(false);
            Destroy(Rope.gameObject);
        }
        Rope = null;
        StopIfDry();
    }

    private void Update()
    {
        if (Source != null && !IsConnected) Detach();
        if (Rope != null)
        {
            var nozzle = CurrentNozzle();
            Rope.SetNozzle(nozzle != null ? nozzle.NozzleTransform : null);
        }
        UpdateWarning();
        StopIfDry();
    }

    private HoseWaterRules ApplyLengthSettings()
    {
        var rules = HoseWaterRules.ForPlayer(gameObject);
        if (Rope != null) Rope.SetMaximumLength(rules != null && rules.LimitHoseLength ? rules.HoseLength : 0f);
        return rules;
    }

    // Runs after PlayerController writes its desired velocity and before the rope simulation.
    private void FixedUpdate()
    {
        if (!IsConnected || body == null || !body.simulated) return;
        var rules = ApplyLengthSettings();
        if (rules == null || !rules.LimitHoseLength)
        {
            Strain01 = 0f;
            return;
        }
        var nozzle = CurrentNozzle();
        if (nozzle == null) return;
        Vector3 offset = nozzle.NozzleTransform.position - Rope.Source.position;
        // Gameplay moves in XY. Nozzle height uses part of the total hose length.
        float reach = Mathf.Sqrt(Mathf.Max(0f, rules.HoseLength * rules.HoseLength - offset.z * offset.z));
        Vector2 radial = new Vector2(offset.x, offset.y);
        float distance = radial.magnitude;
        Vector2 direction = distance > 0.0001f ? radial / distance : Vector2.zero;
        Vector2 desired = body.linearVelocity;
        float outwardSpeed = Mathf.Max(0f, Vector2.Dot(desired, direction));
        Vector2 predicted = radial + desired * Time.fixedDeltaTime;
        bool pulling = predicted.magnitude >= reach - 0.005f && outwardSpeed > 0.01f;

        // Correct aim changes, source motion, or an external displacement beyond the limit.
        if (distance > reach)
        {
            body.position -= direction * (distance - reach);
            radial = direction * reach;
        }
        Vector2 allowed = Vector2.ClampMagnitude(radial + desired * Time.fixedDeltaTime, reach);
        body.linearVelocity = (allowed - radial) / Time.fixedDeltaTime;

        if (pulling)
            Strain01 += Time.fixedDeltaTime * Mathf.Clamp01(outwardSpeed / rules.FullPullSpeed) / rules.SecondsToBreak;
        else
            Strain01 = Mathf.Max(0f, Strain01 - Time.fixedDeltaTime / rules.StrainRecoveryTime);
        if (Strain01 >= 1f)
        {
            Detach();
            brokenMessageUntil = Time.unscaledTime + 2.5f;
            body.linearVelocity = desired;
        }
    }

    private void UpdateWarning()
    {
        if (Rope == null) return;
        // White flashing is distinct even for the red player's hose; thickness also changes.
        float pulse = Strain01 > 0.05f ? (0.5f + 0.5f * Mathf.Sin(Time.time * (12f + 20f * Strain01))) : 0f;
        Rope.SetColor(Color.Lerp(playerColor, Color.white, pulse * Mathf.Clamp01(Strain01 * 2f)));
        Rope.SetWarning(Strain01);
    }

    private void OnGUI()
    {
        if (Time.unscaledTime >= brokenMessageUntil || Camera.main == null) return;
        var screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * 1.5f);
        if (screen.z < 0f) return;
        const string message = "Hose broke!";
        var size = GUI.skin.box.CalcSize(new GUIContent(message));
        GUI.Box(new Rect(screen.x - size.x / 2f, Screen.height - screen.y - size.y, size.x, size.y), message);
    }

    private void StopIfDry()
    {
        if (HoseWaterRules.CanSpray(gameObject)) return;
        foreach (var nozzle in GetComponents<HoseWeapon>()) nozzle.StopSpraying();
    }

    private void OnDisable() => Detach();
}
