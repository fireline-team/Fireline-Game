# Player burning

PlayerHealth owns enemy-hit fire buildup and burning. Under its Burning Inspector header:

- Damage To Ignite defaults to 30 accumulated enemy damage.
- Water To Extinguish defaults to 45 water units.
- Burning Damage Per Second defaults to 5; set to zero for a status without damage.

Only accepted enemy damage builds fire. Contact and roll immunity block both the enemy damage and its buildup. Environmental fire contact, generic TakeDamage calls, and burning damage do not build the meter. Future enemy attack implementations should call TakeEnemyDamage, which applies the shared enemy contact cooldown. Buildup does not decay. Once ignited, it stays full until extinguished or downed.

A successful combat roll extinguishes immediately. Stationary/rejected rolls do not. Teammates can combine water toward the threshold, using each nozzle's existing water rate or pulse amount. Partial water progress persists; self-water, dead/inactive teammates, invalid amounts, and cross-scene sources are rejected. Hose range, width, wall occlusion, hit layers, and supply checks still apply. Water on a downed player goes exclusively to revival. Water and rolling do not reduce partial buildup before ignition.

Burning drains health using scaled time while gameplay is active. Ordinary contact immunity does not suppress the burn. Death clears buildup and burning; existing downed-player flames and revival continue separately. The existing PlayerDownedFire presentation also shows flames on living burning players and shrinks them as extinguishing water accumulates. No new bar is created.

Future UI can subscribe per player to FireBuildupChanged(currentDamage, ignitionThreshold), ExtinguishProgressChanged(currentWater, waterThreshold), Ignited, and Extinguished. Progress events fire when values change, including resets. Completion water is reported before resetting to zero. IsBurning, FireBuildup, FireBuildupProgress, and ExtinguishProgress supply current state for late subscribers. Unsubscribe when UI is disabled/destroyed.

Validation: gameplay and PlayMode test assemblies compile with Unity's compiler. PlayerBurningTests covers buildup, immunity, water thresholds, burn damage, extinguishing and death cleanup. CombatRollTests covers successful versus rejected roll extinguishing. PlayerRevivalTests checks extinguishing with all three nozzles alongside revival. PlayMode execution is blocked while another Unity Editor instance holds the project open.
