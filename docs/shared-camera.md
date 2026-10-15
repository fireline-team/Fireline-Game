# Shared firefighter camera

The shared Cinemachine setup frames standing firefighters only. Its zoom limits
are configured by Cinemachine Group Framing's **Ortho Size Range** (X = minimum,
Y = maximum). **Framing Size** controls how much of the frame the group normally
occupies before zooming; it is separate from the physical screen boundary.

PlayerController constrains the **PlayerSprite body bounds**, including its head
and feet, to the screen. Extra padding defaults to **0 pixels** on all four sides.
Projection uses the sprite's local bounds and transform, so the tilted camera and
feet pivot do not create asymmetric margins. Weapons, overhead badges, and effects
are not part of the body boundary. Movement along an edge remains possible.
The Cinemachine extension uses the same body bounds to keep camera panning and
zoom-in from clipping standing players, without teleporting physics bodies.

## Where to tune the boundary

Open `Assets/_Prefabs/Player.prefab`, select its root **Player**, then expand
**Player Controller → Shared camera boundary**:

- **Screen Edge Padding Pixels**: extra space on every edge. `0` lets the body
  touch the border; `12` leaves a 12-pixel gap on every side. This is independent
  of Cinemachine's zoom limit or Framing Size.
- **Boundary Sprite**: assigned to `PlayerSprite`. Change this only if you replace
  the character body renderer. Its current size, pivot, scale, and orientation
  determine how far the player origin must stay from each edge.

Edit the prefab outside Play Mode to persist changes for all spawned players.
Scene-instance overrides take precedence. Maximum camera zoom remains on
**Camera Configuration → Target Group → Cinemachine Group Framing → Ortho Size Range → Y**.

CameraTargetSetter discovers scene players and updates membership on join,
downing, revival, disable, and destruction. Downed bodies remain where they
fell, and PlayerStatusMarker draws a directional arrow in their hose/player
color when their position is off-screen. The arrow disappears on revival or
when the body comes back into view.

## Playtest

- Join four players. Spread in opposite directions: zoom must stop at your configured maximum.
- Push against each edge and corner; slide along edges and move back inward.
- Walk together across the map; verify camera follow and regrouping zoom.
- Down one player, then leave them behind. Verify their body stays put and the
  colored arrow points back to them. Return and spray them to revive.
- Down everyone, reset, disconnect a player, and try different window aspects.
- Check edge movement beside obstacles and verify the camera waits for trailing
  players rather than pushing them through the level.

Automated coverage is in the PlayMode `SharedCameraTests` suite.
