# Hose teammate revival

The shared Player prefab includes health/revival settings, the overhead skull and meter, and `PlayerDownedFire` using the existing animated `FireSmallBase` visual.

At zero health, the player becomes downed: movement, hose attacks, melee and interactions are blocked. Their character remains visible with animated fire and a skull marker. Spraying them reduces the flames and fills the cyan **REVIVE** meter. Water from multiple teammates adds to the same meter. Progress persists when spraying stops. Filling the meter extinguishes the flames, restores health, re-enables movement and attacks, and restores the normal player/status marker.

## Inspector tuning

Select `Assets/_Prefabs/Player.prefab` → **Player Health → Revival**:

- **Water To Revive**: default 90 water units. With a 30-water/second nozzle this takes 3 seconds for one teammate, or 1.5 seconds for two.
- **Revived Health Percent**: default 50; adjustable from 1–100% of maximum health.
- **Revive Invulnerability Seconds**: default 2 seconds of protection against contact damage after revival.

Each nozzle's existing **Extinguish Per Second** controls revival contribution as well as firefighting. Standard and Mist contribute continuously; Jet contributes when its pulse actually fires, respecting cooldown. The normal hose range, width, obstacle blocking and water-source rules apply. A small 0.3-unit target radius makes spraying the downed body forgiving.

Under **Player Downed Fire**, change the visual prefab or scale. The fire object is presentation only: its colliders are disabled and it does not hurt teammates or create an environmental fire zone. Existing characters stay visible throughout. Flames shrink as progress rises and disappear on revival; a later downing starts fresh at zero progress.

## Reset and architecture

A downed player's interact button no longer resets the level while a living teammate can rescue them. When everyone in the scene is down, the existing reset prompt/button becomes available. Disconnected or disabled player objects do not prevent reset.

`PlayerHealth.IsDowned` describes the new state. `IsDead` and `Died` remain compatible aliases/signals for existing ability guards. `Revived` clears the death status through `PlayerDeathStatus`. `ApplyRevivalWater` validates the contributor and revives once when the threshold is reached. Downed bodies remain out of physics simulation so teammates cannot push them; hoses find them through the active-player registry and test ground-space geometry, avoiding reliance on disabled colliders.

`PlayerStatusMarker.SetProgress(owner, value)` adds an optional 0–1 meter to that producer's status. Only the winning status's meter is shown. `PlayerDeadStatus.asset` supplies the REVIVE label. Other status producers can use the same meter API without changing the renderer.

All players in the same scene are currently teammates. Water does not heal standing players, self-revive, revive across scenes, pass through blockers, or count twice because a player has multiple colliders.

## Verification

Unity 6000.6.1f1, 2026-10-12: the three revival tests passed, covering additive teammate water, retained progress, configured health, repeat downing, all nozzle types, wall/source restrictions, self/downed-contributor rejection, restored movement and attacks, and visual/status cleanup. The full project suite passed 29/30 tests; the pre-existing health-reset test still fails because `BasicScene.unity` is absent. The final targeted run also checks the overhead status tests and a rendered 50%-revived burning player preview.
