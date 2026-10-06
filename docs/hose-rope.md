# Supply hose rope prototype

Open `Assets/_Scenes/Isometric HoseRope Jake.unity` and enter Play Mode. Join using the usual keyboard/gamepad controls. A hose connects a prototype hydrant to the held nozzle. Player colors are **P1 red, P2 yellow, P3 green, P4 blue**. Walk in a circle, double back, and rotate the nozzle to see slack dragging behind you. Additional players get independent hoses from the same supply. Nozzle switching keeps the connection.

## How it works

`HoseRope` represents the hose as a chain of points. Each neighboring pair has a maximum separation, much like links in a rope bridge. Verlet integration carries motion forward, gravity drops the free points, and several constraint passes keep the chain together. Ground friction resists sideways sliding. A LineRenderer draws the continuous hose through these points; there are no individual link GameObjects or Rigidbody joints.

The scene currently uses **XY ground with Rigidbody2D gameplay**, tilted cameras, and raised sprite children. The rope itself simulates in 3D and uses **negative Z as ground-up**, so it hangs from the raised nozzle down to the existing ground. The player's movement and combat physics are unchanged. For a future XZ-ground scene, set the source's Ground Normal to (0, 1, 0) and place its Ground Plane at ground height.

The supply pays out extra hose as the player moves farther away. Returning toward the source leaves that slack behind. This prototype does not limit movement or pull the player back. Paid Out Length is available on the rope in code. A teleport re-lays the rope instead of producing a violent whip.

## Scene setup

- **Water supply - Hose rope prototype**: scene-local `HoseRopeSource` with the hydrant placeholder at (-4, 1.17, 0).
- **Source point**: move this child to the outlet of your eventual truck or hydrant.
- **Hose ground plane (XY)**: defines the static flat collision plane; its local normal is configured on the source.
- **SupplyHose prefab**: simulation and rendering settings. Runtime instances appear under each player as `Supply hose`.
- **NozzleTransform** on `HoseWeapon`: exposes the existing muzzle reference, allowing each equipped nozzle to provide the hose endpoint. A missing muzzle falls back to the hose component's Transform.

`HoseWaterRules` initializes joining players once. `PlayerHoseConnection` owns each player's single connection and follows nozzle changes. `HoseRopeSource` is an `Interactable` outlet; duplicate a source to add more outlets. Sources can supply multiple players. Disabling/destroying a source disconnects its players.

## Attach, detach, and transfer

Walk within 1.25 units of a source and use **E / the melee-interact action (gamepad South)**. The nearby prompt describes the action:

- **Detach hose** at the player's current source.
- **Attach hose** when disconnected.
- **Transfer hose here** at a different source; this releases the old connection and connects to the new one in a single interaction.

The scene has outlets at (-4, 1.17, 0) and (4, -1, 0). Each player controls their own connection. Detaching removes that player's rope visual; reattaching lays out a new rope from the chosen outlet. Detached players do not automatically reconnect. Dead players cannot interact.

## Switch firing rules

Select **Hose gameplay settings** in the scene Hierarchy, then use `HoseWaterRules`:

- **Require Water Source ON**: disconnected players cannot emit water, damage enemies, or extinguish fires with any nozzle.
- **Require Water Source OFF**: legacy behavior; all nozzles can fire while detached. Source interactions and colored rope visuals still work.
- **Start Connected**: new players connect to their nearest available source once. Turn this off before joining to start disconnected.

The firing checkbox can change during Play Mode. Changes made during Play Mode are temporary, as usual in Unity; change it outside Play Mode to save the scene default. Scenes without `HoseWaterRules` keep always-available firing. Use one rules object per scene, regardless of the number of sources.

Colors use each `PlayerInput.playerIndex` and a per-renderer property block, so transferring or changing one player's color does not mutate the shared material. Indices above four repeat the palette.

## Tuning SupplyHose

| Setting | Default | Effect |
|---|---:|---|
| Segments | 64 | More points make smoother bends at higher CPU cost. |
| Minimum Length | 12 | Initial available slack, in world units. |
| Payout Slack | 2 | Spare length beyond the straight-line source/nozzle distance. |
| Radius | 0.06 | Hose thickness and clearance above the ground. |
| Gravity | 9.81 | Speed at which raised segments fall. |
| Air Drag | 1.5 | Damps swinging motion. |
| Ground Friction | 12 | How strongly the resting hose resists sliding. |
| Substeps | 3 | Smaller simulation steps improve stability. |
| Constraint Iterations | 24 | More passes reduce segment stretching. |
| Teleport Distance | 8 | Endpoint jump that triggers re-laying the chain. |

Select a runtime rope in Scene view to see its point gizmos. The two endpoint positions remain exact; interior segment distances are solved approximately.

## Current limits

Collision is against the configured flat plane. Props, walls, height-varying terrain, other hoses, and the hose itself do not collide with it yet. There is no tangling, snagging, water-flow restriction, or finite spool capacity. The simulation has a fixed point count, so very long paid-out hoses have less detailed bends. This is a ground-drag and attachment prototype, not a physical tether restricting the player.

## Verification

Run Unity PlayMode `HoseConnectionTests` and `HoseRopeTests` for grounded particles, endpoint attachment, segment-length stability, moving endpoints, payout, teleport recovery, missing endpoints, two-player connection, nozzle changes, and departure cleanup in the target scene.

Validation on 2026-10-06 with Unity 6000.6.1f1: all seven connection/rope tests passed in an isolated copy of the current project. Coverage includes the actual gamepad melee action while spraying, detach persistence, transfer distance, source shutdown, all three nozzle types under both firing rules, four independent player colors, and existing rope physics. A rendered four-player Play Mode preview confirmed the colors and hydrant visuals. The project Play Mode suite passed 22/23 tests (the additional temporary visual-capture check also passed). The existing `PlayerPrefabStopsAllActionsOnDeathAndControllerCanResetScene` test still fails because it references the absent `Assets/_Scenes/BasicScene.unity`.
