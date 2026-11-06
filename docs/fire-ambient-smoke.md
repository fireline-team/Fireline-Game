# Ambient fire smoke

`FireZone` creates one reusable smoke emitter when **Emit Ambient Smoke** is enabled. The emitter follows burning state, reduces its rate as intensity falls, stops adding particles when extinguished, and resumes on reignition. Existing clouds finish fading in world space. The smoke child is separate from the shrinking flame artwork.

Select `src/Fireline/Assets/Resources/FireVfx/FireSmokeProfile.asset` to tune:

| Setting | Effect |
| --- | --- |
| Rate | Puffs per second at full intensity; zero stops new puffs |
| Lifetime | Time before each puff expires |
| Size / Size Variation | Starting cloud dimensions and variation |
| Rise Speed / Drift | Upward and sideways motion |
| Radius | Emission radius relative to a FireZone's collider footprint; absolute radius for a standalone emitter |
| Spawn Height | Height above the fire's ground position along `TiltedView.Up` |
| Growth | Expansion over a puff's lifetime |
| Color | Smoke tint and opacity |
| Pixelation | Independent enabled, pixel size, strength, aspect, grid offset, color steps, and alpha-cutoff controls |

The default profile uses 14 puffs/second, 2.8-second lifetime, 0.8-unit starting size, gray tint, and six-pixel blocks. Each emitter has a fixed 512-particle capacity. These are presentation settings; fire resistance, damage, and extinguished events are unchanged.

For a particular FireZone, assign a duplicate profile in its **Smoke Profile** field. For decorative fire artwork without a FireZone, place `src/Fireline/Assets/_Prefabs/FireAmbientSmoke.prefab` beneath the artwork. The standalone prefab emits constantly; under a FireZone it follows that fire's burning state. FireZone fires already receive their emitter automatically.

The editable graph is `Assets/Resources/FireVfx/FireAmbientSmoke.vfx`. It uses world-space particles, a procedural cloud texture, and the shared pixelation shader. Water-contact steam remains a separate soft effect.

Creation/upgrade command: **Fireline > VFX > Upgrade Hose Feel and Ambient Fire Smoke**. This creates missing smoke assets and preserves an existing tuned profile and prefab.
