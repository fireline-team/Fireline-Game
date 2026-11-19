using UnityEngine;

/// <summary>
/// A colored ring on the ground under a player, so you can tell who's who at a glance.
/// The ring lies flat on the ground, so the tilted camera shows it as an ellipse.
/// Its color comes from PlayerIdentity. Put it on the Player prefab.
///
/// Two ways to turn rings off: untick Show Ring here (for this prefab), or call
/// PlayerRing.SetRingsEnabled(false) from a settings menu. That one applies to every player
/// and is remembered between sessions.
/// </summary>
[DisallowMultipleComponent]
public class PlayerRing : MonoBehaviour
{
    [Tooltip("Untick to hide this ring. For a player-facing option, use PlayerRing.SetRingsEnabled from a settings menu instead.")]
    [SerializeField] private bool showRing = true;
    [Tooltip("Optional ring art. Empty = a plain ring drawn in code.")]
    [SerializeField] private Sprite ringSprite;
    [Tooltip("Ring width on the ground, in world units.")]
    [SerializeField, Min(0.1f)] private float diameter = 1.2f;
    [Tooltip("Ring thickness as a fraction of its radius (only for the built-in ring).")]
    [SerializeField, Range(0.05f, 0.6f)] private float thickness = 0.18f;
    [Tooltip("Shift from the player's position, e.g. if the feet aren't at the pivot.")]
    [SerializeField] private Vector2 offset = Vector2.zero;
    [SerializeField, Range(0f, 1f)] private float opacity = 0.85f;
    [Tooltip("Draw order: above the ground (-33) and below buildings (-1) and players (0).")]
    [SerializeField] private int sortingOrder = -5;
    [Tooltip("How faded the ring gets while the player is downed.")]
    [SerializeField, Range(0f, 1f)] private float downedOpacity = 0.35f;

    private const string PrefKey = "Fireline.ShowPlayerRings";
    private static bool? _ringsEnabled;

    /// <summary>The game-wide setting: are player rings shown? Saved between sessions. On by default.</summary>
    public static bool RingsEnabled
    {
        get
        {
            if (_ringsEnabled == null) _ringsEnabled = PlayerPrefs.GetInt(PrefKey, 1) == 1;
            return _ringsEnabled.Value;
        }
    }

    /// <summary>Turns rings on or off for every player and remembers the choice. Hook this to a settings toggle.</summary>
    public static void SetRingsEnabled(bool enabled)
    {
        _ringsEnabled = enabled;
        PlayerPrefs.SetInt(PrefKey, enabled ? 1 : 0);
        PlayerPrefs.Save();
    }

    private static Sprite _builtIn;
    private static float _builtInThickness = -1f;

    private SpriteRenderer _ring;
    private PlayerIdentity _identity;
    private PlayerHealth _health;

    private void Awake()
    {
        _identity = PlayerIdentity.Of(gameObject);
        _health = GetComponent<PlayerHealth>();

        var ring = new GameObject("Player Ring");
        ring.transform.SetParent(transform, false);
        _ring = ring.AddComponent<SpriteRenderer>();
        Apply();
    }

    private void LateUpdate()
    {
        if (_ring == null) return;
        _ring.enabled = showRing && RingsEnabled;
        if (!_ring.enabled) return;
        Color color = _identity.Color; // re-read so roster/Inspector edits show up live
        bool downed = _health != null && (_health.IsDowned || _health.IsDead);
        color.a *= opacity * (downed ? downedOpacity : 1f);
        _ring.color = color;
    }

    private void Apply()
    {
        if (_ring == null) return;
        _ring.sprite = ringSprite != null ? ringSprite : BuiltInRing(thickness);
        _ring.sortingOrder = sortingOrder;
        // Flat on the ground (no StandUpright), centered under the player.
        _ring.transform.localPosition = new Vector3(offset.x, offset.y, 0f);
        _ring.transform.localRotation = Quaternion.identity;
        float spriteWidth = _ring.sprite.bounds.size.x;
        float scale = spriteWidth > 0f ? diameter / spriteWidth : 1f;
        _ring.transform.localScale = new Vector3(scale, scale, 1f);
    }

    private void OnValidate() => Apply();

    // A white anti-aliased ring, built once and shared by every player.
    private static Sprite BuiltInRing(float thickness)
    {
        if (_builtIn != null && Mathf.Approximately(_builtInThickness, thickness)) return _builtIn;
        const int size = 128;
        var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
        {
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
        };
        var pixels = new Color32[size * size];
        float outer = size * 0.5f - 1f;
        float inner = outer * (1f - thickness);
        float center = size * 0.5f;
        for (int y = 0; y < size; y++)
        for (int x = 0; x < size; x++)
        {
            float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), new Vector2(center, center));
            // 1px soft edges on both sides of the band.
            float alpha = Mathf.Clamp01(outer - d + 0.5f) * Mathf.Clamp01(d - inner + 0.5f);
            pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
        }
        texture.SetPixels32(pixels);
        texture.Apply();
        _builtIn = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), size);
        _builtInThickness = thickness;
        return _builtIn;
    }
}