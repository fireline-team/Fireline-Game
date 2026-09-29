# Health and hose combat

Open `src/Fireline` with Unity 6000.6.1f1, then open `Assets/_Scenes/BasicScene.unity` and press Play.

1. Join using the keyboard or a gamepad. Move with WASD/arrows or the left stick; aim with the mouse or right stick.
2. Click +10 in the horde panel to spawn enemies.
3. Hold left mouse/right trigger to spray. The blue stream shows the rectangular damage area. Enemies inside it lose health, once per enemy per physics tick, even if they have several colliders.
4. Touch enemies to lose health. Continuing contact deals another hit after 0.75 seconds. All enemies share that cooldown for each player; a crowd cannot apply dozens of hits in one frame.
5. At zero health, movement, aiming, spraying, and normal interaction stop immediately. The player HUD offers Reset level; E or the controller's south face button also resets after a short input guard. This reloads the whole scene for all players. Players must join again.
6. Test with two players: one can die while the survivor continues. Hordes stop targeting dead players.

## Tuning

- Player prefab / PlayerHealth: maximum health (100), contact damage cooldown (0.75 seconds).
- Player prefab / HoseWeapon: damage per second (20), range (5 world units), width (0.6 world units), muzzle, hit layers, and stream material.
- HordeSwarmer and TankEnemy assets: maximum health (15) and contact damage (10).
- Enemy prefabs: trigger CircleCollider2D defines the damageable/touching body and scales with the enemy.

The hose query is independent of rendering and particles. The simple line preview can be replaced with water VFX later. It currently damages every enemy in its rectangle; wall occlusion, nozzles, knockback, and extinguishing VFX are outside this story.

Pooled enemies disappear and return to the pool at zero health; unpooled enemies are destroyed. Health resets on pool reuse. `HealthPool` contains the shared health rules; `HordeEnemy` and `PlayerHealth` own their respective death behavior. Dead players remain visible but their physics body is disabled.

BasicScene and Jake-Player are included in Build Settings so their reset flow works in builds. The existing first build scene is unchanged. Other saved prototype scenes can also reset directly in the Editor.

## Verification

- Run `dotnet test src/Shared/Fireline.sln` for health rules and the existing shared-code suite.
- In Unity's Test Runner, run PlayMode / HealthCombatTests for trigger contact, cooldown, hose geometry, duplicate colliders, player death, pooling, and controller-driven scene reset using the actual player prefab.
- Manually check the stream and HUD in Game view, including two controllers; headless tests cannot validate presentation or physical devices.
