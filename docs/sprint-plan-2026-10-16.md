# Fireline: status and proposed two-week sprints

Planning baseline: October 16, 2026, local checkout at `65a28f8`. Based on source, assets, and repository documentation; the game and tests were not run for this planning review. This is a proposal, not a confirmed class schedule or Jira status. Team availability and submission deadlines remain unconfirmed.

## Where we are

The project has passed much of the original Sprint 1 implementation scope and is approaching the first complete playable mission. It has several later-sprint systems, but a complete rescue-to-extraction loop was not found. Feature presence does not establish that the five-minute experience is fun or stable.

| Area | Evidence in the checkout | Remaining acceptance work |
| --- | --- | --- |
| Hose and nozzles | Separate HoseWeapon/HoseStreamVisual; Standard, Mist, Jet; knockback, slow, and nozzle stations | Validate distinct team roles. Decide whether Standard replaces the proposed Fountain. Values are serialized component settings; do not assume the proposed shared data-asset workflow is complete. |
| Tether | Verlet rope, source attach/detach/transfer, reach limit, strain and breakage | Record the design decision. There is no hose tangling or obstacle snagging; avoid treating these as committed features. |
| Horde | Steering/navigation, configurable enemies, pooled stress-test spawner, fast/tank configurations | Mission-controlled spawning and escalation; prove variants create different decisions. |
| Co-op | Player input integration, shared camera, player colors/status, hose revival | Four physical controllers, mixed-device behavior, full-mission join/reset verification. |
| Fire zones | Placed damaging/extinguishable zones and an Ignite entry point; recent shader changes | Scheduled ignition, mission integration, regression verification of new visuals. Steam-on-extinguish is currently commented out in FireZone. |
| Arena/art/VFX | ProtoLevel, building tools/assets, character animation, water/fire assets | One agreed playable scene, route readability, performance with four players. |
| Mission/rescue | No dedicated civilian, extraction, or mission-state implementation found in gameplay scripts | Release, follow, escort, extraction, final hold, win/lose/restart. |
| Delivery | Unity pinned to 6000.6.1f1; automated test sources and subsystem documentation | Checked-in EditorBuildSettings has no enabled scenes. HealthCombatTests still references missing BasicScene. Latest documented full run is 29/30 on October 12; this is not a current test result. |

Jira completion, outside playtest results, art estimates, and formal design decisions cannot be confirmed from this checkout.

## Alignment with the original pitch

Reviewed the September 8, 2026 pitch supplied at `/Users/sparkpilot/Downloads/FirelinePitchDocFINALGame340JakeMilam.docx.pdf`.

- The production envelope is **10 two-week sprints across fall and spring** (page 6). The four dated sprints below are a proposed near-term sequence, not the whole project schedule. The official sprint number and remaining sprint count are unknown; do not assume all ten remain.
- The pitch MVP is **1–2 players, one arena, one polished hose, one standard enemy, scheduled fire zones, one rescue objective, simple civilians, a stationary truck, revival, mission UI, win/lose conditions, and visual/audio/control polish** (pages 6–7). Four-player support and three enemy types are fuller targets, not prerequisites for the first MVP milestone.
- Nozzle variety, truck-only nozzle swapping, and physical tether/tangling are additions in the later epic proposal. Existing work can support the game, but these additions must not delay the pitch's rescue loop. Hydrants support water supply; they do not replace the truck's starting/extraction role.
- The full mission includes a final hold and boss (page 3); the cut list permits replacing the unique boss with a reskinned enemy (page 8). Use existing enemies for the early hold and reserve a distinct boss for later capacity.
- Civilian auto-extraction and timed player respawn are explicit fallbacks (page 8). Retain implemented revival if stable; simplify escort behavior before investing in complex navigation.
- Preserve simple knockback: it is already implemented and the later feedback specifically argues for retaining it, although the original cut list allows removing knockback physics.
- Original exclusions remain out of scope: online play, procedural maps, simulated fire spread, destructible buildings, truck driving, interiors, inventory/crafting, multiple maps/missions, and complex civilian AI (page 7).

The production priority is to finish the pitch MVP, then validate four-player depth, then use remaining spring sprints for selected content, polish, performance, and presentation. Existing extra systems do not establish MVP completion.

## Capacity and scope rules

- Use the roles in the supplied plan: Jake engineering, Jeffrey production, Lexi art, Will VFX. Proposed crossover tasks depend on each person's actual skills and availability.
- Commit only about 70% of each person's available hours; reserve the rest for integration, defects, and playtests. Do not assign story points without team estimates.
- Jake's primary feature track is the mission/rescue loop. Jeffrey owns acceptance, builds, scheduling, and scene/layout setup; Will owns VFX integration and bounded support work; Lexi owns the next milestone's required assets.
- Keep one arena and the existing three nozzles. Defer a boss, additional maps, new nozzle types, full rope tangling, and axe expansion until the mission is proven.
- Carry failed acceptance work forward before starting the next sprint's optional features.

## Proposed calendar

Dates below show Monday–Friday working windows for two calendar weeks. Sprint labels A–D avoid inventing the team's official sprint numbering.

| Sprint | Dates | Week 1 | Week 2 | Exit gate |
| --- | --- | --- | --- | --- |
| A: first complete rescue | Oct 19–30 | Select canonical scene; repair build/test references; lock nozzle/tether decisions; implement objective release and one civilian follower | Connect escort and extraction, basic success/failure/restart, objective text; integrate and playtest | Two players complete a rescue and restart from a standalone build without editor intervention. Four-controller smoke test is recorded. |
| B: five-minute co-op mission | Nov 2–13 | Connect timed enemy spawning, scheduled fire ignition, and mission phases; add a simple final hold using existing enemies | Tune to 5–7 minutes, four-player camera/revival, civilian failure rules, HUD and nozzle/audio readability | Four outside players attempt a complete mission; completion blockers and confusing decisions are recorded and addressed. |
| C: clarity and stability | Nov 16–27 | Tune existing enemy roles, routes, VFX/audio, and worst-case performance on intended hardware | Fix playtest findings, check backup hardware, stabilize the build | Repeatable full missions at the agreed frame-time target, with no known progression blockers. Nov 23–27 is reduced-capacity/contingency until holiday availability is confirmed. |
| D: demo readiness | Nov 30–Dec 11 | Add safe mission-phase starts for demonstrations; finish required UI/audio and show-critical assets | Freeze features, rehearse, capture, fix blockers, package build and instructions | Rehearsed five-minute presentation and recoverable demo build on primary and backup machines. |

Sprint D is a proposed fall demonstration/readiness checkpoint, not the final project or IEEE deadline. Spring continues within the original ten-sprint envelope. Allocate remaining official sprints only after confirming how many have already elapsed and the academic calendar. If a required fall deadline is sooner, bring phase-start support forward and cut optional content; preserve integration and rehearsal time.

For remaining spring capacity, prioritize: (1) improve the complete mission using outside playtests; (2) prove four-player support and distinct existing enemy roles; (3) add a boss or its permitted reskinned fallback only if the mission is stable; (4) reserve the final two available sprints for performance, defects, final assets/audio, and presentation rehearsal. Combine or cut feature work if fewer sprints remain.

## Sprint A commitment

Goal: convert existing systems into the smallest complete rescue experience.

| Work item | Proposed owner | Acceptance |
| --- | --- | --- |
| Canonical playable scene and repeatable build | Jeffrey + Jake | Another teammate follows written instructions to build and launch; no debug spawning is required to exercise the rescue. |
| Baseline regression cleanup | Jake; Jeffrey verifies | Replace obsolete scene references, run relevant Unity tests, record results, and resolve mission-blocking failures. Check shader fire zones and extinguish feedback. |
| Design decisions | Jeffrey with all four | By Oct 21, record Standard/Mist/Jet roles, allowed swap locations, water-source rules, and tether scope. Do not add tangling this sprint. |
| Rescue objective and follower | Jake | Extinguishing one objective releases one civilian exactly once; the civilian follows through the intended route and can reach extraction. |
| Mission completion and retry | Jake | Extraction produces success; team wipe produces a failure/retry route; reset clears old mission state. Define the simplest civilian-loss rule. |
| Mission route and minimum art | Lexi + Jeffrey | Truck/extraction, objective, civilian, and safe route are recognizable at gameplay scale; placeholders are acceptable. |
| Interaction feedback | Will | Water contact, extinguish/release, downed/revive, and extraction feedback remain readable with multiple streams. |
| Playtest and build record | Jeffrey; whole team attends | Oct 23 internal checkpoint, Oct 28 four-person outside session, Oct 30 review build. These are proposed appointments, not bookings. |

Suggested daily sequence:

- Oct 19: planning, capacity estimates, canonical scene and baseline build/test check.
- Oct 20–21: decision lock, objective event and follower prototype, required asset/feedback placeholders.
- Oct 22–23: objective-to-follower integration; first internal route test.
- Oct 26–27: extraction, success/failure/retry, objective prompts, standalone build.
- Oct 28: outside playtest with four controllers; prioritize blockers over extra features.
- Oct 29: fixes and regression checks.
- Oct 30: acceptance review, tagged/shared build according to team workflow, retrospective and next commitment.

If the follower cannot reliably reach extraction by Oct 23, shorten the route and reduce obstacle complexity. Do not spend the second week adding enemy types or rope features.

## Repeatable sprint rhythm

Day 1: commit a single playable outcome and owners. Day 5: integrated build checkpoint. Day 8: outside playtest. Day 9: fixes. Day 10: review the standalone build and adjust the next sprint.

Every completed story needs observable acceptance in the agreed scene/build. Record build version, testers, controller setup, completion time, blockers, and the next three changes. Ask whether players understood their role, knew the next objective, and wanted another run.

## Decisions needed to finalize dates

1. Next required demo/submission date and any intermediate class deliverables.
2. Weekly hours per teammate and availability during November 23–27.
3. Whether a complete mission or formal playtest results exist outside this checkout.

## Source pointers

- `Assets/_Scripts/HoseTypes/`: current nozzle implementations and station interaction.
- `Assets/_Scripts/HordeCode/HordeStressTest.cs`: pooled debug spawning, not a mission director.
- `Assets/_Scripts/HordeCode/FireZone.cs`: current environmental-fire behavior.
- `Assets/_Scripts/PlayerCode/`, `Assets/_Scripts/HoseRope/`: player and hose systems.
- `docs/hose-rope.md`, `docs/player-revival.md`, `docs/shared-camera.md`: behavior, limits, historical verification.
- `src/Fireline/ProjectSettings/EditorBuildSettings.asset`, `Assets/Tests/PlayMode/HealthCombatTests.cs`: delivery cleanup evidence. Asset paths above are relative to `src/Fireline/`.
