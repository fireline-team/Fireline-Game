# JakeProtoLevel fire mission

This is the first mission feature: randomized starting/return point, two randomized building objectives, required fires, then team evacuation. It is a scene-local component, not a Game Manager. It does not implement civilians, debris, a final stand, enemy waves, or generated streets.

## Play

Open `Assets/_Scenes/JakeProtoLevel.unity`, enter Play Mode, and join normally. Players spawn around the selected return point. Follow the numbered yellow OBJECTIVE markers to the two chosen buildings, in either order. Orange FIRE markers identify the three required fires; ambient fires do not count. A nearby hydrant supports the existing source/tether rules: detach to travel and reconnect at the objective as needed.

Extinguishing every required fire at both buildings changes the instruction to return. Extraction arrows and labels stay hidden until both objectives are complete. Follow the cyan EVAC marker back to the original spawn. All active players in the scene must be standing and inside the cyan three-unit radius for two continuous seconds. Leaving the circle or having a downed teammate resets the countdown. No players cannot complete a mission. Disconnected/disabled players do not block evacuation. Completion displays a success message and raises an Inspector event.

Re-enter Play Mode for a fresh mission. This feature does not add a retry/menu flow or reset player health; that belongs to later session management. Existing team-wipe/reset behavior is unchanged. Late joiners appear at the selected spawn.

## Scene setup

The **Fire rescue mission** root contains:

- **FireRescueMission**: lists of spawn points and building objective sites, evacuation radius/time, optional deterministic seed, and evacuation-ready/completion events.
- **MissionGuidance**: objective instructions, required-fire markers, off-screen direction hints, and the active evacuation ring.
- **Spawn and evacuation candidates**: three editable spawn transforms with MissionPoint components.
- **Building objective candidates**: eleven authored building-front sites, each with three dedicated FireZone prefab instances and a nearby water supply. Buildings without a clear front approach were excluded during authoring.

Only the two selected sites' required fires activate. Existing scenery and ambient hazards remain. The point lists are explicit; adding a GameObject alone does not register it. Assign new points/sites in the mission Inspector.

Select a **MissionPoint** to see its exclusion circle in the XY gameplay plane. A pair is valid when the distance between centers is at least the sum of both exclusion radii (currently 9 + 9 = 18 units). Visual elevation/Z is ignored. Selection chooses a spawn with enough eligible sites, then chooses distinct sites without replacement; there is no retry loop and no too-close fallback. If no valid pair exists, the mission shows an unavailable message and logs an actionable error. Random selection can repeat a previous pair. Set a nonzero seed to reproduce a selection.

The city block is handcrafted. Only mission locations change. Distance does not prove a walkable route, so route playtesting remains necessary when moving points or editing streets.

## Modular objectives

The **Objective Count** setting on FireRescueMission is set to **2** in JakeProtoLevel. Both sites run concurrently; the HUD shows completed objectives. If a spawn has fewer than the requested number of eligible sites, it cannot be selected.

A **MissionObjectiveSite** has an ordered **Steps** array. The mission starts step zero, waits for its completion event, then starts the next step. Evacuation becomes available only after the last step at every selected site completes.

**FireObjectiveStep** owns an explicit list of required FireZones. It activates/reignites those zones when begun and waits for all unique assigned fires to be extinguished. Unassigned fires cannot advance it. Missing fire references or empty step lists invalidate a site.

To add the future axe-debris objective, implement **MissionObjectiveStep** and append it after the fire step. Its `OnBegin` enables the debris interaction and `CompleteStep` signals success. No debris implementation or random selection of an unimplemented objective is included. Current sites are fire-only; future sites can supply different ordered combinations.

FireZone colliders must remain reachable by the ground-space hose. A fire can use a building-mounted visual while its water hitbox sits at the reachable facade; putting the collider behind the building's blocking collider will prevent spraying it. The authored candidates use ground fires in front of buildings. Their dedicated visuals do not change building artwork.

The FireZone update retains shader flame-height behavior, adds a safe fallback for non-shader visuals, and restores steam after extinguishing. Steam is reparented outside the flame child so hiding the flame cannot hide its completion effect.

## Verification

Run Unity PlayMode `FireMissionTests`, `JakeMissionSceneTests`, and `FireZoneTests`. Coverage includes exclusion boundaries, ground-plane distance, irrelevant/duplicate fires, ordered follow-up steps, repeat initialization, downed teammates, empty teams, invalid configuration, authored approach clearance, inactive unselected fires, real-player spawn integration, and actual hose overlap extinguishing.

The explicit editor command **Fireline > Missions > Set up JakeProtoLevel fire mission** is a one-time authoring helper. It does nothing if a mission already exists. It opens JakeProtoLevel after offering to save modified scenes; it does not execute automatically on import or Play. Edit the already-authored scene directly for normal iteration.

### Validation on October 19, 2026

Unity 6000.6.1f1 in an isolated copy: all nine mission/scene/fire tests passed. A graphics-enabled scene run also passed and a rendered camera preview confirmed player placement on the street. The full PlayMode suite passed 36/40. The same four failing tests were rerun against the original FireZone code and original JakeProtoLevel scene and failed identically: missing BasicScene reset, legacy expected hose colors, melee detach behavior, and revival visual status. These pre-existing failures are outside this feature. Automated checks establish local spawn/target clearance, not full human route usability or four-controller playtest quality.
