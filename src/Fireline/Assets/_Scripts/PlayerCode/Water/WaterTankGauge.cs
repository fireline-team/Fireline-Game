using UnityEngine;

/// <summary>
/// (3) UI THAT LISTENS: a small water gauge next to the player, so they don't have to look
/// away from their character to check their tank.
///
/// Hidden most of the time. It listens to the WaterTank and:
///  - fades in when the water level changes (spraying off a hydrant, or refilling on one),
///  - stays up while it keeps changing, and fades out a second after it stops
///    (so a refill fades a second after the tank is full),
///  - pops up and flashes red when the player tries to spray with an empty tank.
/// It never changes the tank and never polls it; Update only animates.
///
/// Put it on the Player prefab. It builds its own simple bar; tweak size and colors here.
/// </summary>
[DisallowMultipleComponent]
public class WaterTankGauge : MonoBehaviour
{
    [Header("Placement")]
    [Tooltip("Where the gauge sits relative to the player's feet: x = sideways, y = height off the ground.")]
    [SerializeField] private Vector2 offset = new Vector2(0.6f, 0.9f);
    [Tooltip("Gauge size in world units (width, height). Taller than wide = a vertical tank.")]
    [SerializeField] private Vector2 size = new Vector2(0.14f, 0.7f);
    [Tooltip("Dark edge around the gauge, in world units.")]
    [SerializeField, Min(0f)] private float border = 0.03f;
    [Tooltip("Draw order. Above players (0) so it's never hidden behind them.")]
    [SerializeField] private int sortingOrder = 30;

    [Header("Colors")]
    [SerializeField] private Color waterColor = new Color(0.25f, 0.6f, 1f);
    [Tooltip("Fill color while a hydrant is refilling the tank.")]
    [SerializeField] private Color refillingColor = new Color(0.55f, 0.85f, 1f);
    [SerializeField] private Color backgroundColor = new Color(0.08f, 0.1f, 0.15f, 0.85f);
    [SerializeField] private Color borderColor = new Color(0f, 0f, 0f, 0.9f);
    [Tooltip("Flash color when trying to spray on an empty tank.")]
    [SerializeField] private Color emptyFlashColor = new Color(1f, 0.15f, 0.1f);

    [Header("Timing")]
    [Tooltip("Seconds the gauge stays up after the water level stops changing.")]
    [SerializeField, Min(0f)] private float lingerTime = 1f;
    [Tooltip("Seconds to fade in or out.")]
    [SerializeField, Min(0.01f)] private float fadeTime = 0.2f;
    [Tooltip("Red flashes per second while out of water.")]
    [SerializeField, Min(0.5f)] private float flashRate = 6f;
    [Tooltip("How fast the fill catches up to the real level. Higher = snappier.")]
    [SerializeField, Min(1f)] private float fillSpeed = 14f;

    private static Sprite _centerSquare;
    private static Sprite _bottomSquare;

    private WaterTankController _controller;
    private WaterTank _tank;

    private SpriteRenderer _border;
    private SpriteRenderer _background;
    private SpriteRenderer _fill;
    private Transform _fillTransform;

    // What the tank last told us. Only the event handlers write these.
    private float _lastAmount = -1f;
    private float _target = 1f;
    private bool _refilling;
    private float _showUntil = -1f;
    private float _flashUntil = -1f;

    private bool _started;

    // Animation state.
    private float _shown = 1f;
    private float _alpha;

    private void Awake()
    {
        _controller = GetComponent<WaterTankController>();
        Build();
        SetAlpha(0f);
    }

    // The controller may be added by the hose after this Awake, so hook up in Start.
    private void Start()
    {
        if (_controller == null) _controller = GetComponent<WaterTankController>();
        _started = true;
        Subscribe();
    }

    private void OnEnable()
    {
        if (_started) Subscribe();
    }

    private void OnDisable() => Unsubscribe();

    private void Subscribe()
    {
        if (_tank != null || _controller == null || _controller.Tank == null) return;
        _tank = _controller.Tank;
        _tank.Changed += OnTankChanged;
        _tank.EmptySprayAttempted += OnEmptySpray;
        _lastAmount = _tank.Amount;
        _target = _shown = _tank.Fraction; // start hidden, at the right level
    }

    private void Unsubscribe()
    {
        if (_tank == null) return;
        _tank.Changed -= OnTankChanged;
        _tank.EmptySprayAttempted -= OnEmptySpray;
        _tank = null;
    }

    // ------------------------------------------------------------------ listening

    private void OnTankChanged()
    {
        _target = _tank.Fraction;
        _refilling = _tank.IsConnected && _tank.Amount > _lastAmount;
        // Only the water level counts; plugging in or out alone doesn't pop the gauge up.
        if (!Mathf.Approximately(_tank.Amount, _lastAmount))
            _showUntil = Time.unscaledTime + lingerTime;
        _lastAmount = _tank.Amount;
    }

    private void OnEmptySpray()
    {
        float now = Time.unscaledTime;
        _flashUntil = now + 0.35f;          // keeps flashing while the trigger is held
        _showUntil = now + lingerTime;
    }

    // ------------------------------------------------------------------ animation

    private void LateUpdate()
    {
        if (_fill == null) return;
        float now = Time.unscaledTime;
        float dt = Time.unscaledDeltaTime;

        bool visible = now < _showUntil;
        _alpha = Mathf.MoveTowards(_alpha, visible ? 1f : 0f, dt / fadeTime);
        _shown = Mathf.Lerp(_shown, _target, 1f - Mathf.Exp(-fillSpeed * dt));

        _fillTransform.localScale = new Vector3(Inner().x, Inner().y * _shown, 1f);

        bool flashing = now < _flashUntil;
        float flash = flashing ? 0.5f + 0.5f * Mathf.Sin(now * flashRate * Mathf.PI * 2f) : 0f;
        _fill.color = Fade(_refilling ? refillingColor : waterColor);
        _background.color = Fade(Color.Lerp(backgroundColor, emptyFlashColor, flash * 0.8f));
        _border.color = Fade(Color.Lerp(borderColor, emptyFlashColor, flash));

        bool show = _alpha > 0.001f;
        _border.enabled = _background.enabled = _fill.enabled = show;
    }

    private Color Fade(Color c)
    {
        c.a *= _alpha;
        return c;
    }

    private void SetAlpha(float alpha)
    {
        _alpha = alpha;
        if (_border == null) return;
        _border.enabled = _background.enabled = _fill.enabled = alpha > 0.001f;
    }

    // ------------------------------------------------------------------ building

    private Vector2 Inner() => new Vector2(Mathf.Max(0.01f, size.x - border * 2f), Mathf.Max(0.01f, size.y - border * 2f));

    private void Build()
    {
        // One upright root beside the player; the bars are flat quads on it.
        var root = new GameObject("Water Gauge").transform;
        root.SetParent(transform, false);
        root.localPosition = new Vector3(offset.x, 0f, 0f) + TiltedView.Up * offset.y;
        root.rotation = TiltedView.StandingRotation;

        _border = Quad(root, "Border", CenterSquare(), sortingOrder, Vector3.zero, size);
        _background = Quad(root, "Background", CenterSquare(), sortingOrder + 1, Vector3.zero, Inner());
        // The fill is anchored at the bottom, so scaling its height drains it downward.
        _fill = Quad(root, "Fill", BottomSquare(), sortingOrder + 2, new Vector3(0f, -Inner().y * 0.5f, 0f), Inner());
        _fillTransform = _fill.transform;
    }

    private static SpriteRenderer Quad(Transform parent, string name, Sprite sprite, int order, Vector3 localPosition, Vector2 scale)
    {
        var go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = new Vector3(scale.x, scale.y, 1f);
        var renderer = go.AddComponent<SpriteRenderer>();
        renderer.sprite = sprite;
        renderer.sortingOrder = order;
        return renderer;
    }

    // 1x1-unit white squares, made once and shared.
    private static Sprite CenterSquare() => _centerSquare != null ? _centerSquare : _centerSquare = Square(new Vector2(0.5f, 0.5f));
    private static Sprite BottomSquare() => _bottomSquare != null ? _bottomSquare : _bottomSquare = Square(new Vector2(0.5f, 0f));

    private static Sprite Square(Vector2 pivot)
    {
        var texture = new Texture2D(4, 4, TextureFormat.RGBA32, false) { filterMode = FilterMode.Point };
        var pixels = new Color32[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = new Color32(255, 255, 255, 255);
        texture.SetPixels32(pixels);
        texture.Apply();
        return Sprite.Create(texture, new Rect(0, 0, 4, 4), pivot, 4);
    }
}
