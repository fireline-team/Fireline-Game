# Environmental fire and hose interaction

The pitch's ground-control loop uses predefined fire zones: the same hose fights enemies and clears burning areas. This implementation provides placed zones, not a simulated spreading-fire system.

## Play it

Open `Assets/_Scenes/BasicScene.unity` and press Play. Join with the keyboard or a controller. Three 2x2 fire zones are placed around the starting position at (3, 1), (-3, 1), and (0, 4).

Aim and hold left mouse/right trigger to spray a zone. With default settings it takes two seconds of continuous overlap to extinguish a zone. Flames shrink and emit fewer particles as their strength drops, then disappear with a steam burst. Partial progress remains when you stop spraying. Multiple players can extinguish a zone together.

Standing in a burning zone deals 10 damage per contact hit through the player's existing 0.75-second cooldown. The cooldown is shared with enemy contact. Once extinguished, the zone stops hurting players immediately; its GameObject and trigger remain so the environment can be reused. Reloading the scene restores its initial fire state.

## Place and tune zones

Drag `Assets/_Prefabs/FireZone.prefab` into a scene and position it. Keep fire visuals under the assigned child, separate from the ground or building artwork. The prefab reuses the existing Fire and steam Burst particle effects from PlayerGymVFX; that source scene is unchanged. The zone uses a dedicated soft, transparent steam material so the completion puff has no hard square edges.

- FireZone / Extinguish Resistance: 60 water units by default.
- HoseWeapon / Extinguish Per Second: 30, independent of enemy damage.
- FireZone / Contact Damage: 10 per hit; set to zero for a harmless burning objective.
- FireZone / Starts Burning: initial state on scene load.
- BoxCollider2D: full gameplay footprint, unaffected by shrinking visual effects. Additional child hitboxes are supported without multiplying hose damage.
- Fire Visual and Extinguish Steam: replaceable visual references, independent of hit detection.
- On Extinguished: Inspector event fires once per burn cycle; a future rescue objective can listen to it.
- Ignite(): callable by a future mission timer/event. An extinguished zone only reignites through this method or scene reload; toggling the object does not refill it.

The current hose still uses its existing rectangular overlap: it can affect enemies and fires in the same tick, and does not implement wall occlusion. Timed ignition schedules, civilian release, and fire propagation are outside this change.

## Checks

Run Unity PlayMode tests: FireZoneTests plus the existing HealthCombatTests. These cover gradual extinguishing, invalid water values, one completion event per burn cycle, duplicate colliders, hose range/direction, cooperative spraying, hazard damage, safety after extinguishing, and reignition under a stationary player.
