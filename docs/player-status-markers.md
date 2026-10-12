# Player overhead status markers

The shared `Player` prefab now has **Player Status Marker** and **Player Death Status** components, so players joining any scene get the feature automatically.

Normal markers show **P1–P4** in bold white text on a dark backing. The stripe beneath the marker follows the player's Inspector-configured hose color (or the default player palette in scenes without hose settings). Markers stay a fixed screen size, face the viewer, and follow the player above their head. They do not intercept mouse input. A dead player's number is replaced by a white skull; its color stripe remains for player identification.

## Inspector settings

Select `Assets/_Prefabs/Player.prefab` and adjust **Player Status Marker**:

- **Height Above Player**: offset along the camera's up direction, starting from the player origin or optional Anchor.
- **Badge Size**: screen pixels (default 72 × 52).
- **Font Size**: default 32.
- **Background Color**: dark backing for contrast.
- **Use Hose Player Colors**: follows the existing four player colors; turn off to use Identity Color.

Death uses `Assets/_Prefabs/PlayerDeadStatus.asset`. Its default priority is 1000. The skull is drawn from simple UI geometry, so it does not depend on a font containing a skull character. You can replace it by disabling Use Skull and assigning your own imported Sprite to Icon.

## Add another status

Use **Create → Fireline → Player Status** in the Project window. Set its priority, label, optional sprite, and tint. Assign the asset to a gameplay component and use:

```csharp
[SerializeField] private PlayerStatusDefinition exhaustedStatus;
private PlayerStatusMarker marker;

private void Awake() => marker = GetComponent<PlayerStatusMarker>();
private void ShowExhausted() => marker.SetStatus(this, exhaustedStatus);
private void HideExhausted() => marker.ClearStatus(this);
private void OnDisable() { if (marker != null) marker.ClearStatus(this); }
```

Each producer owns one status slot. The highest priority wins; equal priorities use the most recently set status. Clearing the winner reveals the next status, or restores the player number if none remain. Destroyed owners are automatically removed. Producers should clear their own slot when disabled or when their condition ends. Keep ordinary statuses below death's priority if they should never obscure the skull.

`PlayerDeathStatus` is the health adapter: it subscribes to the existing death event and initializes correctly when enabled on an already-dead player. The renderer and priority system do not depend on health or specific status types. Future revival logic should clear the death adapter's status when reviving.

Markers use the player's assigned camera when present and otherwise the main camera. With no camera, or when the anchor is behind/outside that camera's viewport, the badge hides. Disabling or destroying a player also hides/removes its separate screen-space UI canvas.

## Verification

Unity 6000.6.1f1, 2026-10-12: player status tests cover all four labels, death priority, skull geometry, per-player isolation, disabling/re-enabling, UI cleanup, clearing statuses, and destroyed status owners. A rendered four-player preview confirms the label/skull appearance. The full project suite passed 26/27 tests; the existing health-reset test still references the missing `BasicScene.unity`. The separate temporary visual-capture check passed.
