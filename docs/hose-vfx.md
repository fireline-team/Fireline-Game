# Hose water VFX Graph

The three nozzle modes share `Assets/Resources/HoseVfx/HoseVfxLibrary.asset`:

| Graph | Trigger | Look |
| --- | --- | --- |
| HoseStream | Continuous while firing | An uninterrupted ribbon that extends from the muzzle, bends downward, and fades quickly on release; droplets shed from the visible stream |
| HoseMist | Continuous while firing | Wide cone of fine, translucent droplets |
| HoseBurst | One event per JetHose shot | One long, pointed water spear; default flight time 0.12 seconds |
| HoseSplash | Stream contact pulses; one stronger sniper impact | Droplets fan outward from the surface normal |
| HoseSteam | Water applied to an active FireZone | Pale rising, expanding steam/smoke puffs |

## First import and preview

With Unity 6000.6.1f1 and VFX Graph 17.6 installed, refresh the Editor. A local authoring request creates the five graphs, soft droplet texture, library, and `Assets/_Scenes/HoseVfxPreview.unity`. If the request was not present, use **Fireline > VFX > Create Missing Hose Graphs**. This menu creates missing assets; it does not replace an artist's existing graphs or edit a level scene. The generator uses the installed package's internal graph model through Editor-only reflection and may require updating after a package upgrade. Runtime uses Unity's public VisualEffect API.

Open the preview scene and enter Play Mode to see all three modes, splashes, and steam together. It has its own camera and does not need players, enemies, hose supply, or input. The mist is deliberately wide; the sniper fires once every 0.8 seconds in this scene.

Existing version-one hose assets can be upgraded with **Fireline > VFX > Upgrade Stream and Jet Graphs**. The upgrade preserves graph GUIDs, library references, and the mist graph. Once the library is version two, this command validates without regenerating artist edits.

Use **Fireline > VFX > Upgrade Stream Motion** for the version-three extending/curved stream. This upgrades only the stream graph and adds a ribbon mesh template; jet and mist graphs are preserved. Repeating the command validates the current version without replacing artist edits.

## Integration and tuning

`HoseWeapon` uses the shared library automatically when **Use Vfx Graph** is enabled, the library is complete, the platform supports compute shaders, and no custom `streamPrefab` is assigned. An explicit **Hose Vfx** library overrides the Resources default. An assigned legacy ParticleSystem prefab retains its original behavior. Turning **Use Vfx Graph** off restores the previous water visual. Existing serialized water materials and fields are preserved.

Select **Assets/Resources/HoseVfx/HoseVfxLibrary.asset** in the Project browser to tune the appearance in the Inspector. Expand these settings:

| Inspector group | Controls |
| --- | --- |
| Stream Appearance | Beam width/color, extension speed, release fade, beam gravity, maximum drop; droplet rate, size, size variation, lifetime, forward speed, sideways spread, initial falling speed, gravity, and color |
| Jet Appearance | Spear length, width, flight time, color, and the point in its lifetime when fading begins |
| Mist Appearance | Density, size, lifetime, and visual spread multipliers; all default to one to preserve the existing look |
| Splash Appearance | Stream and jet particle counts, stream splash interval, droplet size/lifetime/speed/spread/color |
| Steam Appearance | Cloud size/lifetime/rise speed/spread/color, puff interval, particles per water unit, and minimum/maximum puff counts |

Colors include an alpha channel for transparency. Dimensions are in world units, durations are in seconds, and speeds are world units per second. Changes to the library are read during firing and are visible in both gameplay and the preview scene. The library is an asset: Inspector edits during Play Mode persist, so undo experiments or make a duplicate library for alternate art settings. Assign a duplicate in a hose component's **Hose Vfx** slot for a separate per-nozzle style; assign it to the preview component's **Library** slot to preview that same style.

For stream motion, start with these four controls:

- **Extension Speed** (20): how fast the front advances. A five-unit stream reaches full length in about 0.25 seconds. Faster water also has less time to fall.
- **Release Fade** (0.12): how quickly the remaining beam dissolves after release. Droplet emission stops immediately; previously shed droplets finish their own lifetimes.
- **Beam Gravity** (18): downward curvature of the connected stream, distinct from **Gravity**, which accelerates detached droplets.
- **Maximum Drop** (0.75): cap on downward bend. Gameplay further caps it to the nozzle's measured height above the ground, so the stream does not bend underneath the ground plane. Zero beam gravity produces a straight stream.

The visual drop follows `0.5 * beamGravity * (distance / extensionSpeed)^2`, limited by maximum drop and ground clearance. The front grows at extension speed and clips immediately if a nearer wall appears. Splash and steam visuals wait until that front reaches their contact distance. This presentation timing does not change the existing damage/extinguishing calculations or jet timing.

On the Player prefab, **Range** still controls gameplay/visual reach and **Width** controls the hitbox. **Fire Interval** still controls jet cooldown. The stream's beam/droplet appearance and the jet's shape/speed now come from the library, so changing legacy particle size/rate or particles-per-shot does not turn the new effects into blobs. Mist continues to use its existing prefab particle settings multiplied by the library's mist controls. These visual settings do not change damage, hitbox size, or extinguishing strength.

Runtime sets exposed graph properties from the library, so edit the library for gameplay defaults. Edit the VFX Graph itself for new behavior, output materials/textures, or custom block formulas. `WaterBeam.asset` is a continuous ribbon texture; `WaterSpear.asset` has a pointed profile. `SoftDroplet.asset` remains the round texture for secondary droplets and mist.

Particles simulate in world space. The connected stream is one mesh particle: `HoseStreamRibbon` maintains a small, continuous ribbon mesh with an advancing front and a parabolic curve. Each nozzle owns and releases its own runtime mesh. Detached droplets start on that same curved, currently visible portion. Setting droplet rate to zero still leaves a solid stream. The spear emits exactly one particle per shot, captures its launch direction and dimensions, and clamps its length to the available range so its tip reaches the impact without overshooting. Automatic bounds follow the particles. New child renderers inherit the nozzle's sorting layer/order. Effects are owned by the nozzle hierarchy and disappear with its player; switching nozzles allows the short stream fade and already-fired sniper impacts to complete on the visual child. Disabling the player clears the stream and scheduled impacts.

## Contact behavior

Gameplay remains authoritative: the existing 2D wall raycast limits stream length, and the existing overlap query supplies enemy/fire contacts. The nearest wall/enemy gets the shared splash, with wall normals used for outward spray. No splash appears solely because maximum range was reached in empty air. The stream can still pierce enemies according to its existing damage rules. Sniper splashes use the captured shot position and normal when the spear tip reaches that distance, even if aim changes or the trigger is released. Fire-contact steam occurs for all three modes only when applying positive water to a burning zone, including the final extinguishing hit. No FireZone material, resistance, damage, or UnityEvent logic is changed.

The mist's **visual** shape is a cone. Its gameplay hitbox intentionally retains the existing rectangle; changing combat coverage is a separate balance decision. Water remains in the muzzle's raised plane while steam starts near the fire's ground footprint using `TiltedView.Up`. GPU particles do not trigger Physics2D callbacks. Splash motion is decorative; it does not add GPU scene collisions or secondary damage.

Splash events are throttled and reused. Steam uses up to eight reusable contact effects per nozzle, with no per-puff Instantiate/Destroy. If all slots are still occupied, additional overlapping fire zones still receive gameplay water but omit steam until a slot expires.

## Verification

**Fireline > VFX > Run Hose VFX Tests** runs the focused integration and rendering tests. `HoseVfxTests` covers wall-range binding, stopping on release/disable, delayed fire-contact effect reuse, distinct nozzle assets, and the sniper cooldown/continuous-stream distinction. `HoseVfxRenderingTests` checks continuity at zero droplet rate, gradual extension, downward curvature, ground-clearance limiting, release fading, restart behavior, and one spear per jet shot. It writes a comparison image to `Temp/HoseVfxVerification.png` (stream top, unchanged mist middle, spear bottom), plus `Temp/HoseStreamMotion-growing.png`, `Temp/HoseStreamMotion-full.png`, and `Temp/HoseStreamMotion-fading.png`. The comparison slows only a cloned test library's jet shot so its shape is easy to inspect. Results are written to `Temp/HoseVfxTests.xml` and `Temp/HoseVfxTests-result.txt`. These tests need a graphics-capable machine. Older `FireZoneTests` assume particle-era fire visuals; the current branch's shader fire implementation has a separate baseline mismatch with those tests.

Generated asset import checks require every runtime property to be present. Check the Console for graph/shader errors, then visually inspect the preview and a playable level for transparency, density, muzzle alignment, wall contact, and fire placement. C# compilation alone does not prove GPU appearance or performance.

Validated on 2026-10-27 in Unity 6000.6.1f1: all six focused PlayMode tests passed, including rendered continuity, gradual extension, downward curvature, ground-clearance limiting, release fading, restart behavior, one spear per shot, wall-range binding, and fire-contact effect reuse. The growth, full-stream, and comparison renders were inspected. Jet and mist graph hashes were unchanged by the stream-motion upgrade. Existing Burst compiler named-pipe errors remain a separate Editor baseline issue.
