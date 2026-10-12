using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Works out what the player's axe would hit right now (Focused) and shows a prompt for it.
/// The swing itself, and its cooldown, live on AxeSwing; this just does the targeting.
/// </summary>
public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("How far above the player the prompt is drawn, in world units.")]
    [SerializeField] private float promptHeight = 2.4f;

    private PlayerHealth _health;
    private PlayerInput _input;
    private AxeSwing _axe;
    private HoseLoadout _loadout;

    public Interactable Focused { get; private set; }

    public HoseLoadout Loadout => _loadout != null ? _loadout : (_loadout = GetComponent<HoseLoadout>());
    public AxeSwing Axe => _axe != null ? _axe : (_axe = GetComponent<AxeSwing>());

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _input = GetComponent<PlayerInput>();
    }

    private void Update() => RefreshFocus();

    private void RefreshFocus()
    {
        bool dead = _health != null && _health.IsDead;
        if (dead) { Focused = null; return; }

        GetSwing(out Vector2 origin, out Vector2 facing, out float range, out float arc);
        Focused = Interactable.FindInArc(origin, facing, range, arc, this);
    }
    
    private void GetSwing(out Vector2 origin, out Vector2 facing, out float range, out float arc)
    {
        AxeSwing axe = Axe;
        origin = transform.position;
        facing = axe != null ? axe.Facing : Vector2.right;
        range = axe != null ? axe.Range : AxeSwing.DefaultRange;
        arc = axe != null ? axe.ArcDegrees : 360f;
    }

    public bool CanReach(Interactable target)
    {
        if (target == null) return false;
        GetSwing(out Vector2 origin, out Vector2 facing, out float range, out float arc);
        return target.IsInArc(origin, facing, range, arc, out _);
    }
    
    public bool TryInteract() => TryInteract(out _);

    public bool TryInteract(out Interactable used)
    {
        RefreshFocus();
        used = Focused;
        if (used == null) return false;

        used.Interact(this);
        RefreshFocus();
        return true;
    }
    
    private void OnGUI()
    {
        if (Focused == null || Camera.main == null) return;

        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * promptHeight);
        if (screen.z < 0f) return;

        string text = $"[{InteractKeyName()}] {Focused.GetPrompt(this)}";
        AxeSwing axe = Axe;
        if (axe != null && !axe.IsReady) text += $"  ({axe.CooldownRemaining:0.0}s)";
        
        Vector2 size = GUI.skin.box.CalcSize(new GUIContent(text));
        GUI.Box(new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y), text);
    }
    
    private string InteractKeyName()
    {
        if (_input == null || _input.actions == null) return "E";
        InputAction action = _input.actions.FindAction("Interact");
        if (action == null) return "E";

        var scheme = _input.user.controlScheme;
        string shown = scheme.HasValue
            ? action.GetBindingDisplayString(InputBinding.MaskByGroup(scheme.Value.bindingGroup))
            : action.GetBindingDisplayString();
        return string.IsNullOrEmpty(shown) ? "E" : shown;
    }
}