# Hose water VFX Graph

The three nozzle modes share `Assets/Resources/HoseVfx/HoseVfxLibrary.asset`:

| Graph | Trigger | Look |
| --- | --- | --- |
| HoseStream | Continuous while firing | An extending ribbon with a world-space lagged endpoint, a falling tail, and a short release fade; droplets shed from the visible curve |
| HoseMist | Continuous while firing | Wide cone of fine, translucent droplets |
| HoseBurst | One event per JetHose shot | One long, pointed water spear; default flight time 0.12 seconds |
| HoseSplash | Continuous beam endpoint/ground landing; one stronger sniper impact | Droplets fan upward from the ground or outward from an object |
| HoseSteam | Water applied to an active FireZone | Pale rising, expanding steam/smoke puffs |

## First import and preview

With Unity 6000.6.1f1 and VFX Graph 17.6 installed, refresh the Editor. A local authoring request creates the five graphs, soft droplet texture, library, and `Assets/_Scenes/HoseVfxPreview.unity`. If the request was not present, use **Fireline > VFX > Create Missing Hose Graphs**. This menu creates missing assets; it does not replace an artist's existing graphs or edit a level scene. The generator uses the installed package's internal graph model through Editor-only reflection and may require updating after a package upgrade. Runtime uses Unity's public VisualEffect API.

Open the preview scene and enter Play Mode to see all three modes, splashes, and steam together. It has its own camera and does not need players, enemies, hose supply, or input. The mist is deliberately wide; the sniper fires once every 0.8 seconds in this scene.

Existing version-one hose assets can be upgraded with **Fireline > VFX > Upgrade Stream and Jet Graphs**. The upgrade preserves graph GUIDs, library references, and the mist graph. Once the library is version two, this command validates without regenerating artist edits.

Use **Fireline > VFX > Upgrade Stream Motion** for the version-three extending/curved stream. This upgrades only the stream graph and adds a ribbon mesh template; jet and mist graphs are preserved. Repeating the command validates the current version without replacing artist edits.

**Fireline > VFX > Upgrade Water Pixelation and Contact Splash** upgrades to version four. It switches the stream, mist, jet, and splash outputs to the shared `PixelatedWater.shadergraph`, preserving their existing simulation blocks and textures. The splash gains a second, constant-rate spawner for beam contact; its original burst spawner still handles jet impacts. Existing graph GUIDs and the soft steam graph are preserved. Repeating this command validates without replacing edits.

**Fireline > VFX > Upgrade Hose Feel and Ambient Fire Smoke** upgrades to version five. This adds matching curve bindings for shed droplets, independent extended pixel controls for all three nozzles, and a separate fire-smoke graph/profile/prefab. Existing shared pixel-size/enabled values migrate into each nozzle's settings once. Repeat upgrades preserve tuned settings and existing assets.

## Integration and tuning

`HoseWeapon` uses the shared library automatically when **Use Vfx Graph** is enabled, the library is complete, the platform supports compute shaders, and no custom `streamPrefab` is assigned. An explicit **Hose Vfx** library overrides the Resources default. An assigned legacy ParticleSystem prefab retains its original behavior. Turning **Use Vfx Graph** off restores the previous water visual. Existing serialized water materials and fields are preserved.

Select **Assets/Resources/HoseVfx/HoseVfxLibrary.asset** in the Project browser to tune the appearance in the Inspector. Expand these settings:

| Inspector group | Controls |
| --- | --- |
| Stream Appearance | Beam width/color, extension speed, aim response time, release fade, beam gravity, maximum drop; droplet rate/size/lifetime/motion/color, and its own Pixelation settings |
| Jet Appearance | Spear length, width, flight time, color, fade start, and its own Pixelation settings |
| Mist Appearance | Density, size, lifetime, spread multipliers, and its own Pixelation settings |
| Splash Appearance | Stream Rate for continuous contact, jet burst count, droplet size/lifetime/speed/spread/color; independent Pixelation settings; legacy stream count/interval support older custom graphs |
| Steam Appearance | Cloud size/lifetime/rise speed/spread/color, puff interval, particles per water unit, minimum/maximum puff counts, and independent Pixelation settings |

Colors include an alpha channel for transparency. Dimensions are in world units, durations are in seconds, and speeds are world units per second. Changes to the library are read during firing and are visible in both gameplay and the preview scene. The library is an asset: Inspector edits during Play Mode persist, so undo experiments or make a duplicate library for alternate art settings. Assign a duplicate in a hose component's **Hose Vfx** slot for a separate per-nozzle style; assign it to the preview component's **Library** slot to preview that same style.

Expand **Pixelation** inside **Stream Appearance**, **Mist Appearance**, **Jet Appearance**, **Splash Appearance**, or **Steam Appearance**. Each group has its own filter. Splash and steam settings apply across all nozzles and update existing effects during Play Mode. Both contact filters default to enabled with Pixel Size 2. Disable Steam Pixelation to restore smooth sampling of the original soft texture. **Fireline > VFX > Upgrade Contact Pixelation** upgrades older steam graphs while preserving their texture, simulation, tint, and lifetime fades. The filter has:

- **Enabled:** bypass the whole filter when disabled.
- **Pixel Size** (1–32): block size in render-target pixels; larger gives coarser pixels.
- **Strength** (0–1): blend smooth UV sampling toward fully snapped blocks.
- **Pixel Aspect:** horizontal/vertical block proportions; `(1, 1)` makes square blocks.
- **Grid Offset:** shifts the pixel grid in render-target pixels.
- **Color Steps:** levels per texture color channel; zero preserves gradients, two or more posterizes them.
- **Alpha Cutoff:** discards faint texture edges while preserving particle lifetime fades.

Fully extinguished environmental fires, extinguished players (including combat rolls), and revived players also use the library's Steam graph and Steam Appearance settings. Completion emits one puff using Maximum Count and the configured Lifetime; it does not reuse the old long-lived prefab ParticleSystem. Reigniting or disabling the owner clears the puff.

The unlit VFX shader samples the original texture on a screen-aligned grid. No full-screen post-process, extra camera, layer, or renderer feature is needed. Camera upscaling/post-processing can soften the final pixels. Fine droplets may occupy part of a block; use larger droplets or smaller Pixel Size to retain fine mist density.

For stream motion, start with these controls:

- **Extension Speed** (20): how fast the front advances. A five-unit stream reaches full length in about 0.25 seconds. Faster water also has less time to fall.
- **Release Fade** (0.12): how quickly the remaining beam dissolves after release. Droplet emission stops immediately; previously shed droplets finish their own lifetimes.
- **Aim Response Time** (0.22): time constant for the far end to follow aim in world space. Higher gives more trailing motion; zero follows instantly. The muzzle follows the player immediately while the ribbon bends toward its lagged end.
- **Beam Gravity** (18): downward curvature of the connected stream, distinct from **Gravity**, which accelerates detached droplets.
- **Maximum Drop** (0.75): cap on the gravity portion of the bend, further limited by the muzzle's ground clearance. The final falling tail still reaches the ground when no object is at the endpoint, even with low gravity/drop settings.

The front grows at extension speed. The far end uses frame-rate independent exponential interpolation toward the aimed world position, while a quadratic curve keeps the near end aligned with the nozzle. A nearer range limit still clips promptly. Gravity adds a parabolic bend; in the absence of an endpoint object, a fourth-power tail reaches the flat gameplay ground plane. CPU mesh geometry and GPU droplet spawning use the same curve/drop controls. Steam waits for front arrival. Damage, hitboxes, water application, and jet timing retain the existing physics-step aiming; the endpoint lag is presentation only.

On the Player prefab, **Range** still controls gameplay/visual reach and **Width** controls the hitbox. **Fire Interval** still controls jet cooldown. The stream's beam/droplet appearance and the jet's shape/speed now come from the library, so changing legacy particle size/rate or particles-per-shot does not turn the new effects into blobs. Mist continues to use its existing prefab particle settings multiplied by the library's mist controls. These visual settings do not change damage, hitbox size, or extinguishing strength.

Runtime sets exposed graph properties from the library, so edit the library for gameplay defaults. Edit the VFX Graph itself for new behavior, output materials/textures, or custom block formulas. `WaterBeam.asset` is a continuous ribbon texture; `WaterSpear.asset` has a pointed profile. `SoftDroplet.asset` remains the round texture for secondary droplets and mist.

Particles simulate in world space. The connected stream is one mesh particle: `HoseStreamRibbon` maintains a small, continuous ribbon mesh with an advancing front and a parabolic curve. Each nozzle owns and releases its own runtime mesh. Detached droplets start on that same curved, currently visible portion. Setting droplet rate to zero still leaves a solid stream. The spear emits exactly one particle per shot, captures its launch direction and dimensions, and clamps its length to the available range so its tip reaches the impact without overshooting. Automatic bounds follow the particles. New child renderers inherit the nozzle's sorting layer/order. Effects are owned by the nozzle hierarchy and disappear with its player; switching nozzles allows the short stream fade and already-fired sniper impacts to complete on the visual child. Disabling the player clears the stream and scheduled impacts.

## Contact behavior

Gameplay remains authoritative: the existing 2D wall raycast limits stream range, and overlap queries supply enemy/fire contacts. The beam always has a splash at its visible endpoint while emitting. A fresh object contact uses its normal once the lagged endpoint reaches it; otherwise the falling tail and splash land on the ground, including when firing into empty space. The stream can still pierce enemies according to its existing damage rules. Sniper splashes use the captured shot position and normal when the spear tip reaches that distance. Fire-contact steam occurs only while applying positive water to a burning zone. Fire material, resistance, damage, and extinguished-event behavior remain independent of the new ambient smoke.

The mist's **visual** shape is a cone; its gameplay hitbox retains the existing rectangle. Raised water, ground landings, and rising smoke use `TiltedView.Up`. GPU particles do not trigger Physics2D callbacks or secondary damage.

The endpoint splash uses **Splash Appearance > Stream Rate** (default 155 droplets/second). It follows the actual rendered tip as the beam grows, bends, and trails aim. Loss of object contact falls back to the ground splash. Release, player disable, or a zero rate stops new droplets; existing droplets finish their lifetimes. A single reusable effect handles the endpoint. Older custom graphs retain their legacy fallback. Steam reuses up to eight contact effects per nozzle.

## Ambient fire smoke

Existing `FireZone` instances create one smoke child automatically when **Emit Ambient Smoke** is enabled. No level or fire-prefab rewiring is needed. Its optional **Smoke Profile** field overrides **Assets/Resources/FireVfx/FireSmokeProfile.asset**. Tune rate, lifetime, size/variation, rise speed, drift, radius relative to the fire footprint, spawn height, growth, color, and independent Pixelation settings there. Default smoke is gray, rises continuously while burning, and gets less dense as fire strength falls.

Extinguishing stops emission while existing world-space clouds finish fading. Reignition reuses the same emitter. Smoke sits outside the shrinking flame visual, so it is not scaled away with the fire. Disabling the fire also stops its emitter. The graph has a fixed 512-particle capacity per fire.

For decorative fire artwork without a `FireZone`, drag **Assets/_Prefabs/FireAmbientSmoke.prefab** beneath it. This emits constantly on its own; when placed under a FireZone it follows that fire's burning state. Its profile can be overridden on the smoke component. `FireAmbientSmoke.vfx` is separate from the water-contact steam graph.

## Verification

**Fireline > VFX > Run Hose VFX Tests** runs the focused hose integration/rendering tests and existing `FireZoneTests`. These cover wall range, release/disable, fire-contact steam reuse, distinct nozzle assets, one spear per shot, continuity, extension, ground clearance, fading, endpoint lag/convergence, moving the muzzle in world space, ground splashes without colliders, independent nozzle filters, zero-strength smooth rendering, and ambient-smoke lifecycle. Results are written to `Temp/HoseVfxTests.xml` and `Temp/HoseVfxTests-result.txt`. These checks need a graphics-capable machine.

The additional render checks compare smooth and pixelated water, verify visibly flat color blocks and retained tint, and check the pixelation toggle. A sustained splash test checks front arrival, live particle emission, rendered droplets around the impact, moving contact, loss of contact, release, and expiration. These produce `Temp/HoseWater-smooth.png`, `Temp/HoseWater-pixelated.png`, `Temp/HoseBeam-contact-splash.png`, and a contact-state diagnostic text file. Test settings use cloned libraries and do not overwrite the shared appearance defaults.

Version-five captures include `Temp/HoseBeam-ground-splash.png`, `Temp/HoseBeam-aim-lag.png`, `Temp/HosePixelation-modes.png`, and `Temp/FireAmbientSmoke.png`. The nozzle comparison and smoke capture use cloned settings to make shape/filter differences easy to inspect. Default appearance assets retain their tuned values.

Generated asset import checks require every runtime property to be present. Check the Console for graph/shader errors, then visually inspect the preview and a playable level for transparency, density, muzzle alignment, wall contact, and fire placement. C# compilation alone does not prove GPU appearance or performance.

Validated on 2026-11-05 in Unity 6000.6.1f1: all 15 focused PlayMode checks passed. Ground landing, curved aim lag, nozzle filter comparison, and smoke renders were inspected. The runtime, Editor, and test assemblies also compile with Unity's Roslyn compiler. Existing Burst compiler named-pipe errors remain a separate Editor baseline issue. Shader Graph slider ranges must match their domains: strength 0–1, color steps 0–32, and alpha cutoff 0–0.95. VFX Graph applies these slider constraints to linked inputs, so copying the pixel-size range would clip soft droplets and smoke.
