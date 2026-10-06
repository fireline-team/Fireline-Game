# Supply hose rope prototype

Open `Assets/_Scenes/Isometric HoseRope Jake.unity` and enter Play Mode. Join using the usual keyboard/gamepad controls. A yellow hose connects the red prototype hydrant to the held nozzle. Walk in a circle, double back, and rotate the nozzle to see slack dragging behind you. Additional players get independent hoses from the same supply. Nozzle switching keeps the connection.

## How it works

`HoseRope` represents the hose as a chain of points. Each neighboring pair has a maximum separation, much like links in a rope bridge. Verlet integration carries motion forward, gravity drops the free points, and several constraint passes keep the chain together. Ground friction resists sideways sliding. A LineRenderer draws the continuous hose through these points; there are no individual link GameObjects or Rigidbody joints.

The scene currently uses **XY ground with Rigidbody2D gameplay**, tilted cameras, and raised sprite children. The rope itself simulates in 3D and uses **negative Z as ground-up**, so it hangs from the raised nozzle down to the existing ground. The player's movement and combat physics are unchanged. For a future XZ-ground scene, set the source's Ground Normal to (0, 1, 0) and place its Ground Plane at ground height.

The supply pays out extra hose as the player moves farther away. Returning toward the source leaves that slack behind. This prototype does not limit movement or pull the player back. Paid Out Length is available on the rope in code. A teleport re-lays the rope instead of producing a violent whip.

## Scene setup

- **Water supply - Hose rope prototype**: scene-local `HoseRopeSource` with the hydrant placeholder at (-4, -1, 0).
- **Source point**: move this child to the outlet of your eventual truck or hydrant.
- **Hose ground plane (XY)**: defines the static flat collision plane; its local normal is configured on the source.
- **SupplyHose prefab**: simulation and rendering settings. Runtime instances appear under each player as `Supply hose`.
- **NozzleTransform** on `HoseWeapon`: exposes the existing muzzle reference, allowing each equipped nozzle to provide the hose endpoint. A missing muzzle falls back to the hose component's Transform.

`HoseRopeSource` checks players in its own scene, connects them automatically, follows nozzle changes, and cleans up hoses on departure or when the source is disabled. Use one supply manager per scene in this prototype.

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

Run Unity PlayMode `HoseRopeTests` for grounded particles, endpoint attachment, segment-length stability, moving endpoints, payout, teleport recovery, missing endpoints, two-player connection, nozzle changes, and departure cleanup in the target scene.

Validation on 2026-10-06 with Unity 6000.6.1f1: all four hose tests passed in an isolated project copy, and a rendered Play Mode preview confirmed the hose is visible and connected. The full Play Mode suite passed 19/20 tests. The existing `PlayerPrefabStopsAllActionsOnDeathAndControllerCanResetScene` test fails because it references `Assets/_Scenes/BasicScene.unity`, which is absent from the current project.
