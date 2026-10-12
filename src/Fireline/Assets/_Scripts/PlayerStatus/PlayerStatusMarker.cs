using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>Screen-space presentation and priority arbitration, independent of status producers.</summary>
[DisallowMultipleComponent]
public class PlayerStatusMarker : MonoBehaviour
{
    [SerializeField] private Transform anchor;
    [Tooltip("Offset along the camera's up direction, measured from the player origin.")]
    [SerializeField, Min(0f)] private float heightAbovePlayer = 1.8f;
    [SerializeField] private Vector2 badgeSize = new Vector2(72, 52);
    [SerializeField, Min(12)] private int fontSize = 32;
    [SerializeField] private Color backgroundColor = new Color(0.025f, 0.035f, 0.05f, 0.94f);
    [SerializeField] private bool useHosePlayerColors = true;
    [SerializeField] private Color identityColor = Color.white;

    private struct Entry { public Object owner; public PlayerStatusDefinition status; public long order; }
    private readonly List<Entry> entries = new List<Entry>();
    private long nextOrder;
    private PlayerInput input;
    private Canvas canvas;
    private RectTransform badge;
    private Text text;
    private Image background, stripe, icon;
    private PlayerSkullGraphic skull;
    public PlayerStatusDefinition CurrentStatus { get; private set; }
    public string PlayerLabel => $"P{Mathf.Max(0, input != null ? input.playerIndex : 0) + 1}";
    public bool IsVisible => badge != null && badge.gameObject.activeInHierarchy;

    public void SetStatus(Object owner, PlayerStatusDefinition status)
    {
        if (owner == null) return;
        ClearStatus(owner);
        if (status != null) entries.Add(new Entry { owner = owner, status = status, order = ++nextOrder });
        ResolveStatus();
    }

    public void ClearStatus(Object owner)
    {
        entries.RemoveAll(entry => entry.owner == owner);
        ResolveStatus();
    }

    private void ResolveStatus()
    {
        entries.RemoveAll(entry => entry.owner == null || entry.status == null);
        CurrentStatus = null;
        long order = -1;
        foreach (var entry in entries)
            if (CurrentStatus == null || entry.status.priority > CurrentStatus.priority
                || (entry.status.priority == CurrentStatus.priority && entry.order > order))
            {
                CurrentStatus = entry.status;
                order = entry.order;
            }
    }

    private void Awake()
    {
        input = GetComponent<PlayerInput>();
        var root = new GameObject("Player overhead UI", typeof(RectTransform), typeof(Canvas), typeof(CanvasGroup));
        SceneManager.MoveGameObjectToScene(root, gameObject.scene);
        canvas = root.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;
        var group = root.GetComponent<CanvasGroup>();
        group.interactable = false;
        group.blocksRaycasts = false;
        badge = Rect("Status badge", root.transform);
        badge.anchorMin = badge.anchorMax = Vector2.zero;
        background = badge.gameObject.AddComponent<Image>();
        stripe = Rect("Player color", badge).gameObject.AddComponent<Image>();
        text = Rect("Label", badge).gameObject.AddComponent<Text>();
        text.font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        text.fontStyle = FontStyle.Bold;
        text.alignment = TextAnchor.MiddleCenter;
        text.horizontalOverflow = HorizontalWrapMode.Overflow;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        icon = Rect("Status icon", badge).gameObject.AddComponent<Image>();
        icon.preserveAspect = true;
        skull = Rect("Skull", badge).gameObject.AddComponent<PlayerSkullGraphic>();
        foreach (var graphic in root.GetComponentsInChildren<Graphic>()) graphic.raycastTarget = false;
    }

    private static RectTransform Rect(string name, Transform parent)
    {
        var rect = new GameObject(name, typeof(RectTransform)).GetComponent<RectTransform>();
        rect.SetParent(parent, false);
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        return rect;
    }

    private void LateUpdate()
    {
        ResolveStatus();
        var camera = input != null && input.camera != null ? input.camera : Camera.main;
        if (camera == null) { badge.gameObject.SetActive(false); return; }
        var point = camera.WorldToScreenPoint((anchor != null ? anchor.position : transform.position)
            + camera.transform.up * heightAbovePlayer);
        bool visible = point.z > 0f && camera.pixelRect.Contains(new Vector2(point.x, point.y));
        badge.gameObject.SetActive(visible);
        if (!visible) return;
        badge.anchoredPosition = new Vector2(point.x, point.y);
        badge.sizeDelta = badgeSize;
        background.color = backgroundColor;
        var rules = HoseWaterRules.ForPlayer(gameObject);
        int index = input != null ? input.playerIndex : 0;
        stripe.color = useHosePlayerColors
            ? (rules != null ? rules.GetHoseColor(index) : PlayerHoseConnection.ColorForPlayer(index)) : identityColor;
        stripe.rectTransform.sizeDelta = new Vector2(badgeSize.x, 4);
        stripe.rectTransform.anchoredPosition = new Vector2(0, -badgeSize.y / 2 + 2);
        text.rectTransform.sizeDelta = badgeSize;
        text.fontSize = fontSize;
        text.text = CurrentStatus != null ? CurrentStatus.label : PlayerLabel;
        Color tint = CurrentStatus != null ? CurrentStatus.tint : Color.white;
        text.color = icon.color = skull.color = tint;
        bool showSkull = CurrentStatus != null && CurrentStatus.useSkull;
        bool showIcon = !showSkull && CurrentStatus != null && CurrentStatus.icon != null;
        text.gameObject.SetActive(!showSkull && !showIcon);
        icon.gameObject.SetActive(showIcon);
        skull.gameObject.SetActive(showSkull);
        icon.sprite = CurrentStatus != null ? CurrentStatus.icon : null;
        float size = Mathf.Max(1f, Mathf.Min(badgeSize.x, badgeSize.y) - 8);
        icon.rectTransform.sizeDelta = skull.rectTransform.sizeDelta = new Vector2(size, size);
    }

    private void OnEnable() { if (canvas != null) canvas.gameObject.SetActive(true); }
    private void OnDisable() { if (canvas != null) canvas.gameObject.SetActive(false); }
    private void OnDestroy() { if (canvas != null) Destroy(canvas.gameObject); }
}
