# World sprite lighting

Fireline uses a perspective camera, XY gameplay plane, and the Universal Renderer. Buildings and standard world sprites now receive URP **3D** directional, point, and spot lights. Light 2D is a different renderer path.

- `Fireline/StructureDepth` retains opaque cutout, alpha cutoff, depth writing, two-sided drawing, and depth texture support. It now uses diffuse lighting, ambient spherical harmonics, realtime shadow receiving, and a cutout shadow-caster pass. Casting also requires the renderer's Cast Shadows setting; additional-light shadows remain governed by the existing URP asset.
- `Fireline/SpriteWorldLit` / `Assets/_VFX/SpriteWorldLit.mat` provides transparent diffuse lighting, sprite tint/alpha, flips, and normal sprite sorting. The player, enemy, grass, hydrant, building fade material, and direct default sprite assignments in scenes use it. UI and custom VFX materials retain their existing shaders.
- `Fireline/SpriteFlash` lights the base sprite before blending toward the bright hit-flash color.
- New Structure objects resolve the same transparent lit material in the Editor. Existing Structure depth material GUIDs and gameplay behavior are preserved.

## Use and tune

Add a regular Point Light or Spot Light near the building, on the visible side of its planes. This game's ground is XY, so elevation is along negative Z / TiltedView.Up, rather than conventional world Y. Adjust range and intensity. Ambient environment lighting supplies the baseline brightness; sprites can appear dark when there is little ambient or direct light.

Normals follow the actual sprite plane, including skewed side faces, and face the viewer for two-sided rendering. Painted-in shading remains part of the source artwork. This does not add sprite normal maps or convert the existing fire/water VFX.

## Verification

Run **Fireline > Lighting > Validate World Sprite Lighting**. The check uses an isolated preview scene with a perspective camera, compares a red point light on/off for all three shaders, checks flips, verifies transparent fading, checks shader compiler errors, and writes results / PNGs to `src/Fireline/Temp/SpriteLighting-validation.txt` and adjacent images. It preserves the open gameplay scene.

## LevelRevamp lighting setup

Configured in the open Unity 6000.6.1f1 Editor on 2026-11-12. The active scene is
`Assets/_Scenes/LevelRevamp.unity`. Its camera uses perspective projection, a
60-degree field of view, and the existing 30-degree tilt.

| Setting | Value |
| --- | --- |
| Renderer | Universal Renderer, Forward+ |
| SRP Batcher | Disabled: the current custom sprite shaders corrupt shadow draws when it is enabled |
| Color space | Linear |
| Lighting | Per-pixel realtime directional, point, and spot lights |
| Environment | Realtime Gradient: sky `(0.38, 0.43, 0.52)`, equator `(0.28, 0.31, 0.37)`, ground `(0.16, 0.18, 0.22)` |
| Main directional light | Intensity `0.45`, cool tint `(0.86, 0.91, 1)`, soft shadows at strength `0.8` |
| Directional shadows | 2048 map, 2 cascades, split `0.3`, maximum distance `65` |
| Shadow bias | Pipeline depth `0.5`, normal `0.25` |
| Local shadows | 2048 atlas; six street spotlights use the 512 medium tier |
| Sprite geometry | 196 building faces cast two-sided cutout shadows; world renderers receive shadows |
| Image features | HDR, HDR color grading, depth texture, 4x MSAA |
| Scene post-processing | Neutral tonemapping; Bloom intensity `0.12`, threshold `1.1`, scatter `0.5` |

`Assets/Settings/URPconfig/LevelRevampLighting.lighting` disables baked and
Enlighten realtime GI and uses realtime environment lighting. The sprite shaders
sample ambient spherical harmonics rather than baked lightmap UVs, so this scene
uses direct realtime lights and the gradient for its illumination. The previous
baked lighting data is detached from the scene; its assets are preserved.

The scene's **LevelRevamp Lighting** global Volume uses
`Assets/Settings/URPconfig/LevelRevampLightingVolume.asset`. Tune the main light
and gradient for overall readability, the existing street lights for local
highlights, and the Volume profile for highlight glow. The point fill light has
no shadow map. New structure instances need **Cast Shadows: Two Sided** and
**Receive Shadows** to match the configured building faces.

The URP asset and Universal Renderer are shared across the project's quality
levels and scenes. The ambient colors, light overrides, lighting settings asset,
and Volume assignment belong to LevelRevamp. Forward+ accommodates overlapping
local lights on large sprite surfaces without Forward's per-object light limit;
see Unity's [rendering paths reference](https://docs.unity.com/en-us/engine/6000.3/manual/render-pipelines/rendering-paths-introduction).
`ProjectSettings/QualitySettings.asset` also records 4x anti-aliasing for the
active Very Low quality tier.

The authoring session saved a pre-change scene copy and URP asset copies under
`src/Fireline/Temp/LevelRevampLighting/`, together with before/after scene renders
and inspection reports. These are local temporary artifacts.

Validation passed with the original sprite shaders: colored point-light
response, flipX/flipY, transparent fading, shader compilation, and rendered
directional/spot/point shadows. The shadow check compared casting on/off in an
isolated perspective-camera scene and checked that lit pixels remained visible.
A standard mesh caster provided a control: it worked with SRP batching enabled,
while the custom sprite caster failed or caused blank output. Both caster types
worked with SRP batching disabled. This setting prioritizes correct sprite
rendering; frame-time performance has not been profiled. The existing Burst
compiler named-pipe errors are unrelated to this lighting setup.
