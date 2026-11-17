# Pixel minimap

The shared multiplayer camera prefab includes PixelMinimap. Older rigs with
CameraTargetSetter automatically receive it. It appears at bottom right, north-up,
and tracks the main camera's center ray where it intersects the XY ground at Z=0.
The center snaps to one map texel to keep static terrain from shimmering.

The map uses a 128×128 point-filtered Texture2D displayed at an integer 2× scale.
It rasterizes gameplay footprints instead of using a second camera/RenderTexture:
this avoids showing tilted roofs, effects, or temporary debris as map terrain and
works without extra camera layers or render-pipeline configuration.

- Muted green: ground tiles. Dark: empty space beyond the tiles.
- Gray: non-trigger Obstacles-layer collider footprints, excluding Debris parents.
- Red pulses: enemy density within fixed 5-world-unit cells; higher counts grow
  from 1 to 5 map pixels in radius and pulse from 0.6 to 2 Hz, capped at 20 enemies.
- Player-colored filled diamonds: standing allies; X: downed allies.
- Orange flame glyphs: active fires, removed when extinguished or disabled.
- Yellow outlined diamonds: active objectives; cyan cross: active evac point.
- Allies and objectives outside the map stay on its border, with an outward stem.

Tune **Pixel Minimap** on
`Assets/_Prefabs/Camera/LocalMultiplayerManagerAndCameraSet.prefab` outside Play Mode:
resolution, world span (60 units across by default), integer display scale, refresh
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
