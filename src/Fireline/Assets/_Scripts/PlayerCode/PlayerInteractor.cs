using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerInteractor : MonoBehaviour
{
    [Tooltip("How far above the player the prompt is drawn, in world units.")]
    [SerializeField] private float promptHeight = 2.4f;

    private PlayerHealth _health;
    private PlayerInput _input;
    
    public Interactable Focused { get; private set; }

    private HoseLoadout _loadout;
    
    public HoseLoadout Loadout => _loadout != null ? _loadout : (_loadout = GetComponent<HoseLoadout>());

    private void Awake()
    {
        _health = GetComponent<PlayerHealth>();
        _input = GetComponent<PlayerInput>();
    }

    private void Update() => RefreshFocus();

    private void RefreshFocus()
    {
        bool dead = _health != null && _health.IsDead;
        Focused = dead ? null : Interactable.FindNearest(transform.position, this);
    }
    
    public bool TryInteract()
    {
        RefreshFocus(); // don't rely on last frame's result; the player may have just moved
        if (Focused == null) return false;

        Focused.Interact(this);
        RefreshFocus(); // e.g. after equipping, this station no longer applies
        return true;
    }

    // Placeholder prompt until there's real UI.
    private void OnGUI()
    {
        if (Focused == null || Camera.main == null) return;

        Vector3 screen = Camera.main.WorldToScreenPoint(transform.position + Vector3.up * promptHeight);
        if (screen.z < 0f) return;

        string key = _input != null && _input.currentControlScheme == "KeyboardMouse" ? "E" : "South";
        string text = $"[{key}] {Focused.GetPrompt(this)}";

        // IMGUI measures from the top-left, world-to-screen from the bottom-left.
        Vector2 size = GUI.skin.box.CalcSize(new GUIContent(text));
        GUI.Box(new Rect(screen.x - size.x * 0.5f, Screen.height - screen.y - size.y, size.x, size.y), text);
    }
}
