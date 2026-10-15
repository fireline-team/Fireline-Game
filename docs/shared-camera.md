# Shared firefighter camera

The shared Cinemachine setup uses **perspective projection** with the existing
30-degree camera/billboard tilt. The physics ground remains the XY plane.
Standing firefighters are framed dynamically using **Zoom Only**, with **FOV
Range = 60–78 degrees** and no dolly adjustment. At the existing 10-unit follow
distance, 78 degrees gives approximately 8 units of vertical half-height at the
target depth. Perspective makes the visible ground area vary with depth; it is
not an orthographic-size limit.

PlayerController constrains the **PlayerSprite body bounds**, including its head
and feet, to the screen. Extra padding defaults to **0 pixels** on all four sides.
The complete billboard is tested against the camera's view volume, including its
depth. Movement is constrained on the XY floor, so players slide along the
sloping perspective edges without moving their physics bodies off the floor. Weapons, overhead badges, and effects
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
**Camera Configuration → Target Group → Cinemachine Group Framing → FOV Range → Y**
(maximum vertical field of view). X is the closest zoom. Keep **Size Adjustment =
Zoom Only** to use this cap without additional dolly zoom-out. **Cinemachine Follow
→ Follow Offset** sets the camera distance. Orthographic Size Range has no effect
in perspective. The camera Lens Mode Override and Brain Default Mode are Perspective.

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
