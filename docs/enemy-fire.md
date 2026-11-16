# Enemy fire appearance

The shared `Assets/_Prefabs/HordeEnemy.prefab` uses `Assets/_VFX/EnemyFire.mat`
and the `Fireline/EnemyFire` shader. All enemy definitions continue to supply
their existing sprite frames, animation controllers, size variation, and tints.
The shader retains the original texture coordinates and alpha silhouette.

## Material controls

Select **EnemyFire.mat** to tune:

- **Emission Color / Emission Strength:** HDR glow, multiplied by the existing sprite colors.
- **Keep Glow Across Enemy Tints:** keeps dark enemy-type tints bright enough to glow, while retaining their hues. Reduce toward zero to retain their original brightness differences.
- **Hot Core Contrast:** emphasizes brighter painted flame details.
- **Flicker Amount / Flicker Speed:** gentle, offset brightness pulses across the crowd.
- **Rising Flame Shimmer / Flame Detail Scale / Flame Rise Speed:** upward-moving brightness variation inside the sprite.
- **Ember Glints:** small moving highlights within the flame silhouette.

Setting Flicker Amount, Rising Flame Shimmer, and Ember Glints to zero leaves a
steady emissive version of the same sprite. Glow around the silhouette uses the
camera's HDR and Bloom settings; LevelRevamp already has both configured.

## Realtime lighting

The prefab's **Fire Light** child is a regular URP 3D Point Light with a warm
color, intensity `1.8`, range `3.5`, and realtime light mode. Tune these values
on the child before entering Play Mode. `EnemyFireLight` follows the sprite
center, lifts the light toward negative Z for the XY gameplay plane, applies
the enemy tint, and scales the radius with the square root of the enemy size.
Its flicker reads the material's Flicker Amount and Flicker Speed, including
when the hit-flash material temporarily replaces the fire material.

Up to **24** nearby enemy lights run at once. Selection refreshes every `0.15s`,
includes offscreen fires whose range reaches the camera frustum, and favors
already selected lights to reduce switching. Every enemy remains emissive.
Lights use no shadow maps. Pooling disables the light immediately; reused
enemies regain their light and emissive material.

## Verification

- **Fireline > Lighting > Validate Enemy Fire Rendering** compares the original
  alpha silhouette, checks HDR emission and light spill on an adjacent lit
  sprite, checks shader errors, and renders a preview without saving the open scene.
- **Fireline > Lighting > Run Enemy Fire Tests** exercises the actual prefab,
  hit-flash restoration, pooling, flicker, tracking, and the shared light limit,
  alongside the existing enemy ability tests.

Results and preview PNGs are written to `Temp/EnemyFire/` within the Unity project.

Validated on 2026-11-16 in an isolated Unity 6000.6.1f1 project using copies of
the actual prefab, scripts, sprites, materials, URP configuration, and current
LevelRevamp volume profile. Shader compilation, the original silhouette, HDR
glow for all six enemy tints, a bloom halo, and realtime light spill passed.
All **10 PlayMode tests passed**: the three enemy-fire checks and seven existing
enemy behavior checks. The gameplay, Editor, and test assemblies also compiled
against the original project's Unity references. Level scenes and pipeline
settings were preserved. A player build and frame-time profiling were not run.
