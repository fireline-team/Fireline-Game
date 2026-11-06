using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// Gives every player their own HUD panel. Put this on a Screen Space - Overlay canvas
/// (Canvas Scaler set to Scale With Screen Size, e.g. 1920x1080) in each gameplay scene.
///
/// Watches PlayerHealth.ActivePlayers, so it picks up players whenever they join.
/// Panel N goes in slot N (P1 = slot 0). If no slots are set, panels go in the four
/// screen corners: P1 top-left, P2 top-right, P3 bottom-left, P4 bottom-right.
/// </summary>
public class PlayerHudManager : MonoBehaviour
{
    [Tooltip("The HUD panel prefab (has a PlayerHud on its root).")]
    [SerializeField] private PlayerHud hudPrefab;
    [Tooltip("Optional. Empty RectTransforms to parent each player's panel to, in player order.")]
    [SerializeField] private RectTransform[] slots = new RectTransform[0];
    [Tooltip("Gap from the screen edge when using automatic corners.")]
    [SerializeField] private Vector2 cornerPadding = new Vector2(24f, 24f);

    private readonly Dictionary<PlayerHealth, PlayerHud> _huds = new Dictionary<PlayerHealth, PlayerHud>();
    private readonly List<PlayerHealth> _gone = new List<PlayerHealth>();

    private void Update()
    {
        if (hudPrefab == null) return;

        foreach (PlayerHealth player in PlayerHealth.ActivePlayers)
            if (player != null && player.gameObject.scene == gameObject.scene && !_huds.ContainsKey(player))
                _huds.Add(player, CreateHud(player));

        // Remove panels for players that left.
        _gone.Clear();
        foreach (var pair in _huds)
            if (pair.Key == null || !pair.Key.isActiveAndEnabled) _gone.Add(pair.Key);
        foreach (PlayerHealth player in _gone)
        {
            if (_huds[player] != null) Destroy(_huds[player].gameObject);
            _huds.Remove(player);
        }
    }

    private PlayerHud CreateHud(PlayerHealth player)
    {
        PlayerInput input = player.GetComponent<PlayerInput>();
        int index = input != null ? Mathf.Max(0, input.playerIndex) : _huds.Count;

        PlayerHud hud;
        if (index < slots.Length && slots[index] != null)
        {
            hud = Instantiate(hudPrefab, slots[index], false);
        }
        else
        {
            hud = Instantiate(hudPrefab, transform, false);
            PlaceInCorner((RectTransform)hud.transform, index);
        }

        hud.name = $"HUD P{index + 1}";
        hud.Bind(player);
        return hud;
    }

    private void PlaceInCorner(RectTransform rect, int index)
    {
        bool right = index % 2 == 1;
        bool bottom = index >= 2;
        Vector2 corner = new Vector2(right ? 1f : 0f, bottom ? 0f : 1f);

        rect.anchorMin = rect.anchorMax = rect.pivot = corner;
        rect.anchoredPosition = new Vector2(
            right ? -cornerPadding.x : cornerPadding.x,
            bottom ? cornerPadding.y : -cornerPadding.y);
    }
}
