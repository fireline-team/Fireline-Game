# Pixel minimap

The shared multiplayer camera prefab includes PixelMinimap. Older rigs with
CameraTargetSetter automatically receive it. It appears at bottom right, north-up,
and tracks the main camera's center ray where it intersects the XY ground at Z=0.
The center snaps to one map texel to keep static terrain from shimmering.

The map uses a 128×128 point-filtered Texture2D in a square panel. The shared prefab is currently set to 400 screen pixels,
including the border. There is no heading or color legend.
It rasterizes gameplay footprints instead of using a second camera/RenderTexture:
this avoids showing tilted roofs, effects, or temporary debris as map terrain and
works without extra camera layers or render-pipeline configuration.

- Muted green: ground tiles. Dark: empty space beyond the tiles.
- Gray: non-trigger Obstacles-layer collider footprints, excluding Debris parents.
- Red pulses: enemy density within fixed 5-world-unit cells; higher counts grow
  from 1 to 5 map pixels in radius and pulse from 0.6 to 2 Hz, capped at 20 enemies.
- Player-colored filled diamonds: standing allies; X: downed allies.
- Pale-blue person glyphs: released civilians still awaiting rescue, including
  border markers when off-map. Saved or lost civilians disappear.
- Orange flame glyphs: active fires, removed when extinguished or disabled.
- Yellow outlined diamonds: active objectives; cyan cross: active evac point.
- Allies and objectives outside the map stay on its border, with an outward stem.

Tune **Pixel Minimap** on
`Assets/_Prefabs/Camera/LocalMultiplayerManagerAndCameraSet.prefab` outside Play Mode:
resolution, world span (60 units across by default), **Minimap Size Pixels**, refresh
interval, enemy cell size, density cap, and pulse frequencies. Changes to resolution
require restarting Play Mode. Sources are rediscovered every 0.5 seconds and the map
redraws at 10 Hz by default, including while the gameplay clock is paused.

Ground Tilemaps can be assigned explicitly; otherwise non-Obstacles-layer tilemaps
in the same scene are used. With no ground tilemaps, the background represents open
ground. The map deliberately does not infer roofs or walls from decorative sprites:
permanent blockers need an enabled collider on the Obstacles layer. Debris is never
part of this terrain map, even while it blocks gameplay.

Playtest in a populated level: move as a team, down an ally, extinguish a fire,
complete objectives and enter evac, approach a dense enemy wave, and resize the
window. Check the bottom-right HUD against other panels and adjust World Span to
suit the level. Automated coverage is in PixelMinimapTests.

## Mission panels

Two panels sit above the map: a countdown and the objective checklist. The width
tracks **Minimap Size Pixels**, and the full stack fits the available screen height.

- **Countdown Seconds** defaults to 600 (10:00). The mission owns elapsed time;
  it advances while a standing player is present and gameplay is active. Pausing
  or completing the mission stops it, and restarting the mission resets it. At
  zero it stays at 00:00; this HUD does not introduce a mission-loss condition.
- **Objective Reveal Distance** defaults to 8 world units. Any standing teammate
  near a site or one of its targets expands that site's checklist. It stays open
  until the mission is restarted.
- Site names describe their steps, such as Put out fires or Break crates. Child
  entries show each fire/crate, including completed targets. Completed rows use
  bold text with a bright red strikethrough; completed site/extraction rows also
  display a red COMPLETE label. Duplicate target references do not duplicate rows.
- Pixel glyphs replace bullets and match minimap colors/shapes. Mission crates
  get brown crate icons while active; temporary debris remains excluded from the
  terrain layer.
- After every site is complete, Extract the team appears below the completed
  list, using the cyan evac symbol. Extraction completion strikes it through.
- Tall lists are clipped to a viewport and can be scrolled with the mouse wheel.
  They automatically reveal newly expanded sites and the extraction row.

### Adjusting text size

Select the root of `LocalMultiplayerManagerAndCameraSet.prefab`, then expand
**Pixel Minimap → Mission text sizes**:

- **Timer Font Size**: 48 by default.
- **Objective Font Size**: 30 by default.
- **Subtask Font Size**: 26 by default (individual fires/crates).

Values apply to the generated HUD each refresh, including while playing. Automatic
font shrinking is disabled; panels grow and checklist rows wrap/scroll instead.
Edit the prefab outside Play Mode to persist values. Scene-instance overrides
can override the prefab values. Game-view preview scaling also affects how large
the UI looks in the editor; judge readability at the intended game resolution.
