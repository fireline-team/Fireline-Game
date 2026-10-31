# JakeProtoLevel fire mission

This is the first mission feature: randomized starting/return point, one randomized building objective, required fires, then team evacuation. It is a scene-local component, not a Game Manager. It includes timed civilian release, following, damage, and extraction. It does not implement debris, a final stand, enemy waves, or generated streets.

## Play

Open `Assets/_Scenes/JakeProtoLevel.unity`, enter Play Mode, and join normally. Players spawn around the selected return point. Follow the numbered yellow OBJECTIVE markers to the chosen buildings, in either order. Orange FIRE markers identify the three required fires; ambient fires do not count. A nearby hydrant supports the existing source/tether rules: detach to travel and reconnect at the objective as needed.

Extinguishing every required fire at all selected buildings changes the instruction to return. Extraction arrows and labels stay hidden until all objectives are complete. Follow the cyan EVAC marker back to the original spawn. All surviving civilians must first enter the evacuation radius. Then all active players in the scene must be standing and inside the cyan three-unit radius for two continuous seconds. Leaving the circle or having a downed teammate resets the countdown. No players cannot complete a mission. Disconnected/disabled players do not block evacuation. Completion displays a success message and raises an Inspector event.

Use the existing hold-T restart control or the post-evac E / A prompt for a fresh mission. Existing team-wipe/reset behavior is unchanged. Late joiners appear at the selected spawn.

## Civilian release, escort, and extraction

The September 8 pitch (`FirelinePitchDocFINALGame340JakeMilam.docx.pdf`, pages 3, 6–8) describes releasing trapped civilians, escorting them to the truck, and a final hold. This iteration implements release and escort to the existing evacuation zone. The final hold remains future work. The casualty timings below are prototype tuning values.

JakeProtoLevel, PlayTestScene, and PlayTestLevel each select **one objective**. The selected building starts with **3 trapped civilians**, hidden inside the building. Unselected buildings have no civilians or clock. Completing **all ordered steps** stops the trapped-civilian timer and releases surviving civilians **one at a time, one second apart**. The first emerges on the next update. Clearing one fire or an intermediate step does not release them. Objective completion does **not** count anyone as saved.

A released civilian uses the player's body sprite at **70% of the player's visual size**, tinted pale blue. Each follows the **nearest living player** in the same scene, reevaluating during movement. Downed or disconnected players are ignored; if nobody is available, civilians wait. They walk at **80% of that player's configured speed** (normally 4 versus 5 units/second), stop 0.8 units away, and route around obstacle-layer buildings using a private navigation field. Their small ground-space collider and wall casts prevent walking through buildings. They are not player slots, cannot use hoses, and do not expand the shared camera; off-screen arrows help retrieve stragglers.

Released civilians have **50 health** and take existing fire/enemy contact damage with a shared **0.75-second contact cooldown**. Extinguished fire and dead/disabled enemies do no damage. Hits blink the sprite; dead civilians turn red and fall over. They cannot be revived. The HUD shows civilian health and counts for trapped, emerging, escorting, saved, and lost.

Civilians count as saved only when they physically enter the active evac radius. They then disappear safely and cannot take further damage. The team evacuation countdown waits until every civilian is saved or lost, including anyone still emerging, then requires all active players to be standing in evac as before. Losing everyone still permits team evacuation, with a zero-rescue result. Restart clears queued releases, follower objects, timers, and counts.

Before the objective is cleared, the first trapped civilian dies after **120 seconds**; another dies every **30 seconds**. The **20-second warning** names the objective and counts down. Timers run only while at least one player is active and use scaled gameplay time. Deaths and objective completion show six-second notices. Finishing the objective stops these timed losses; subsequent losses come from escort damage.

Tune **Civilian Count**, **First Loss Seconds**, **Loss Interval Seconds**, **Warning Seconds**, **Release Interval Seconds**, and **Civilian Sprite** on MissionObjectiveSite. CivilianEscort owns the movement/health defaults. Author new routes with clear building exits and the Obstacles layer; the private path area includes 12 units of detour space around civilian/player endpoints and bakes static obstacles when created. Moving or newly built obstacles require a new bake in a future dynamic-navigation task.

## Scene setup

The **Fire rescue mission** root contains:

- **FireRescueMission**: lists of spawn points and building objective sites, evacuation radius/time, optional deterministic seed, and evacuation-ready/completion events.
- **MissionGuidance**: objective instructions, required-fire markers, off-screen direction hints, and the active evacuation ring.
- **Spawn and evacuation candidates**: three editable spawn transforms with MissionPoint components.
- **Building objective candidates**: eleven authored building-front sites, each with three dedicated FireZone prefab instances and a nearby water supply. Buildings without a clear front approach were excluded during authoring.

Only the selected sites' required fires activate. Existing scenery and ambient hazards remain. The point lists are explicit; adding a GameObject alone does not register it. Assign new points/sites in the mission Inspector.

Select a **MissionPoint** to see its exclusion circle in the XY gameplay plane. A pair is valid when the distance between centers is at least the sum of both exclusion radii (currently 9 + 9 = 18 units). Visual elevation/Z is ignored. Selection chooses a spawn with enough eligible sites, then chooses distinct sites without replacement; there is no retry loop and no too-close fallback. If no valid pair exists, the mission shows an unavailable message and logs an actionable error. Random selection can repeat a previous pair. Set a nonzero seed to reproduce a selection.

The city block is handcrafted. Only mission locations change. Distance does not prove a walkable route, so route playtesting remains necessary when moving points or editing streets.

## Modular objectives

The **Objective Count** setting on FireRescueMission is set to **1** in all current mission scenes. The selection code still supports multiple sites for future authoring; the HUD shows completed objectives. If a spawn has fewer than the requested number of eligible sites, it cannot be selected.

A **MissionObjectiveSite** has an ordered **Steps** array. The mission starts step zero, waits for its completion event, then starts the next step. Evacuation becomes available only after the last step at every selected site completes.

**FireObjectiveStep** owns an explicit list of required FireZones. It activates/reignites those zones when begun and waits for all unique assigned fires to be extinguished. Unassigned fires cannot advance it. Missing fire references or empty step lists invalidate a site.

To add the future axe-debris objective, implement **MissionObjectiveStep** and append it after the fire step. Its `OnBegin` enables the debris interaction and `CompleteStep` signals success. No debris implementation or random selection of an unimplemented objective is included. Current sites are fire-only; future sites can supply different ordered combinations.

FireZone colliders must remain reachable by the ground-space hose. A fire can use a building-mounted visual while its water hitbox sits at the reachable facade; putting the collider behind the building's blocking collider will prevent spraying it. The authored candidates use ground fires in front of buildings. Their dedicated visuals do not change building artwork.

The FireZone update retains shader flame-height behavior, adds a safe fallback for non-shader visuals, and restores steam after extinguishing. Steam is reparented outside the flame child so hiding the flame cannot hide its completion effect.

## Verification

Run Unity PlayMode `FireMissionTests`, `JakeMissionSceneTests`, and `FireZoneTests`. Coverage includes exclusion boundaries, ground-plane distance, irrelevant/duplicate fires, ordered follow-up steps, repeat initialization, downed teammates, empty teams, invalid configuration, authored approach clearance, inactive unselected fires, real-player spawn integration, actual hose overlap extinguishing, civilian warning/loss boundaries, releasing only survivors after the final step, one-at-a-time emergence, capped losses after long frames, nearest living player selection, fire/enemy contact damage, wall navigation, walking into extraction, and reset cleanup.

The explicit editor command **Fireline > Missions > Set up JakeProtoLevel fire mission** is a one-time authoring helper. It does nothing if a mission already exists. It opens JakeProtoLevel after offering to save modified scenes; it does not execute automatically on import or Play. Edit the already-authored scene directly for normal iteration.

### Validation on October 19, 2026

Unity 6000.6.1f1 in an isolated copy: all nine mission/scene/fire tests passed. A graphics-enabled scene run also passed and a rendered camera preview confirmed player placement on the street. The full PlayMode suite passed 36/40. The same four failing tests were rerun against the original FireZone code and original JakeProtoLevel scene and failed identically: missing BasicScene reset, legacy expected hose colors, melee detach behavior, and revival visual status. These pre-existing failures are outside this feature. Automated checks establish local spawn/target clearance, not full human route usability or four-controller playtest quality.

### Initial civilian prototype validation on October 31, 2026 (superseded by escort)

Unity 6000.6.1f1, graphics enabled, isolated temporary project: all 14 focused `FireMissionTests`, `JakeMissionSceneTests`, and `FireZoneTests` passed. A separate temporary rendering test also passed; the rendered greybox preview was inspected for trapped, endangered, saved, and lost states. The authored-scene test now reads the configured objective count (currently five), verifies civilians exist only at selected sites, and verifies real hose extinguishing saves all 15 civilians. Casualty timings and full HUD readability during multiplayer combat still need a human playtest. The full unrelated PlayMode suite was not rerun.

### Escort validation on October 31, 2026

Unity 6000.6.1f1, graphics enabled, isolated temporary project: all **17** focused `FireMissionTests`, `JakeMissionSceneTests`, and `FireZoneTests` passed. Coverage includes sequential release, nearest living player switching, slower speed, fire and enemy damage with cooldowns, saved/dead accounting exactly once, queued/active civilian reset, and a survivor walking around a blocking wall and into evac before the team can finish. The authored scene test verifies one objective, civilian art from the player sprite sheet at smaller scale, and release through actual hose extinguishing. A separate rendered comparison confirmed the civilian is 70% of the player body size and uses the same upright sprite presentation. Full-route multiplayer pacing and combat balance still need playtesting; the unrelated full PlayMode suite was not rerun.
