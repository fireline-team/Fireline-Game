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
