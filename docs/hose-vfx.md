# Hose water VFX Graph

The three nozzle modes share `Assets/Resources/HoseVfx/HoseVfxLibrary.asset`:

| Graph | Trigger | Look |
| --- | --- | --- |
| HoseStream | Continuous while firing | Dense, narrow blue-white water jet |
| HoseMist | Continuous while firing | Wide cone of fine, translucent droplets |
| HoseBurst | One event per JetHose shot | Compact fast pulse, about 0.09 seconds in flight |
| HoseSplash | Stream contact pulses; one stronger sniper impact | Droplets fan outward from the surface normal |
| HoseSteam | Water applied to an active FireZone | Pale rising, expanding steam/smoke puffs |

## First import and preview

With Unity 6000.6.1f1 and VFX Graph 17.6 installed, refresh the Editor. A local authoring request creates the five graphs, soft droplet texture, library, and `Assets/_Scenes/HoseVfxPreview.unity`. If the request was not present, use **Fireline > VFX > Create Missing Hose Graphs**. This menu creates missing assets; it does not replace an artist's existing graphs or edit a level scene. The generator uses the installed package's internal graph model through Editor-only reflection and may require updating after a package upgrade. Runtime uses Unity's public VisualEffect API.

Open the preview scene and enter Play Mode to see all three modes, splashes, and steam together. It has its own camera and does not need players, enemies, hose supply, or input. The mist is deliberately wide; the sniper fires once every 0.8 seconds in this scene.

## Integration and tuning

`HoseWeapon` uses the shared library automatically when **Use Vfx Graph** is enabled, the library is complete, the platform supports compute shaders, and no custom `streamPrefab` is assigned. An explicit **Hose Vfx** library overrides the Resources default. An assigned legacy ParticleSystem prefab retains its original behavior. Turning **Use Vfx Graph** off restores the previous water visual. Existing serialized water materials and fields are preserved.

The graph blackboards expose **Origin**, **Direction**, **Up**, **Range**, **Width**, **Lifetime**, **ParticleSize**, and either **Rate** or **Count**. During gameplay the controller sets water properties from the nozzle's existing range, width, particle lifetime, particle rate, and size. Water droplets use 0.24 times the legacy sprite size; continuous density uses 6 times the legacy rate for stream and 9 times for mist. Adjust these multipliers in `HoseGraphVisual.ConfigureWater`/`SetSpraying` if changing art scale. Splash/steam defaults can be tuned in their graph blackboards. Initialize and fade blocks contain editable Custom HLSL; the other spawn, update, orientation, and output contexts are ordinary VFX Graph nodes.

Particles simulate in world space and face the camera. Automatic bounds follow the particles. New child renderers inherit the nozzle's sorting layer/order. Effects are owned by the nozzle hierarchy and disappear with its player; switching nozzles stops continuous emission, while already-fired sniper impact scheduling completes on the visual child. Disabling the player clears scheduled impacts.

## Contact behavior

Gameplay remains authoritative: the existing 2D wall raycast limits stream length, and the existing overlap query supplies enemy/fire contacts. The nearest wall/enemy gets the shared splash, with wall normals used for outward spray. No splash appears solely because maximum range was reached in empty air. The stream can still pierce enemies according to its existing damage rules. Sniper splashes use the captured shot position and normal after its short visual flight, even if aim changes or the trigger is released. Fire-contact steam occurs for all three modes only when applying positive water to a burning zone, including the final extinguishing hit. No FireZone material, resistance, damage, or UnityEvent logic is changed.

The mist's **visual** shape is a cone. Its gameplay hitbox intentionally retains the existing rectangle; changing combat coverage is a separate balance decision. Water remains in the muzzle's raised plane while steam starts near the fire's ground footprint using `TiltedView.Up`. GPU particles do not trigger Physics2D callbacks. Splash motion is decorative; it does not add GPU scene collisions or secondary damage.

Splash events are throttled and reused. Steam uses up to eight reusable contact effects per nozzle, with no per-puff Instantiate/Destroy. If all slots are still occupied, additional overlapping fire zones still receive gameplay water but omit steam until a slot expires.

## Verification

`HoseVfxTests` covers wall-range binding, stopping on release/disable, fire-contact effect reuse, distinct nozzle assets, and the sniper cooldown/continuous-stream distinction. Run these in Unity's PlayMode test runner on a graphics-capable machine. Older `FireZoneTests` assume particle-era fire visuals; the current branch's shader fire implementation has a separate baseline mismatch with those tests.

Generated asset import checks require every runtime property to be present. Check the Console for graph/shader errors, then visually inspect the preview and a playable level for transparency, density, muzzle alignment, wall contact, and fire placement. C# compilation alone does not prove GPU appearance or performance.
