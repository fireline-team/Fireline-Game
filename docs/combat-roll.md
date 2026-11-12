# Combat roll

Press Space (keyboard) or L1 / left bumper (gamepad) while moving. The player dashes in that movement direction for 0.25 seconds at 3× normal speed. Direction is locked for the dash; diagonal and analog inputs produce the same dash speed. Pressing while stationary does nothing. Holding the button does not repeat rolls.

Tune Roll Duration (0.01–0.99 seconds), Roll Speed Multiplier, and Roll Cooldown on PlayerController. The default cooldown is 1.5 seconds after the dash ends. Timers use scaled game time. Movement continues through the existing Rigidbody2D and shared screen boundary constraints.

During the dash, all player sprites use the white CombatRollFlash material and PlayerHealth rejects damage, including contact damage. Original materials return afterward, and existing contact/revival immunity retains its own timer. Disabling the controller or blocking gameplay cancels the dash and starts its cooldown. No burning status or extinguishing behavior is implemented.

Future UI can subscribe to per-player PlayerController events:

- RollStarted and RollEnded mark the dash state.
- RollCooldownChanged(remainingSeconds, totalSeconds) fires when the cooldown begins, each active update, and at zero.
- RollReady fires once when cooldown finishes.

IsRolling, CanCombatRoll, and RollCooldownRemaining let late subscribers query current state. RollCooldownRemaining includes remaining dash time while rolling. Unsubscribe when the UI is disabled/destroyed. A disabled player reports readiness on its next active update.

CombatRollTests covers gamepad activation, the Space binding, direction locking, damage immunity, material restoration, cooldown events, and disable cleanup. Run it in Unity's PlayMode Test Runner. Gameplay and test assemblies were compiled with Unity's Roslyn compiler; PlayMode execution was blocked by the already-open Editor project lock.
