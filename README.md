# Fireline-Game
This repository will hold the project for our GAME 340-440 sequence capstone.

## Mission gameplay loop

Open `src/Fireline/Assets/_Scenes/MainMenu.unity` in Unity and press Play. MainMenu is also the first enabled build scene. Open the fire rescue mission, then Start to load JakeProtoLevel. Use the existing arena join controls to join players.

Complete the objectives to enable EVAC, escort the civilians, and return the team to the evacuation zone for extraction. Success results appear only after extraction; a full team wipe produces failure results. Results include objective progress, civilians saved/lost, and mission time. Exit to the main menu to launch a fresh mission.

Click Pause, press Escape, or press controller Start to pause/resume. Menu and briefing support Enter/controller A; controller B closes the briefing. Results support Enter/A after a short input guard. The pause menu also offers an exit to the main menu.

`GameManager` persists across menu/arena loads and owns session state and time. `FireRescueMission` continues to own objective and extraction rules. Opening prototype arenas directly retains their existing standalone reset controls; launching through MainMenu replaces those shortcuts with results and menu navigation.

Play Mode coverage: `GameManagerTests` checks pause during hit-stop, team wipe versus a revivable teammate, results/menu/replay, and objective completion versus successful extraction.

### Hose wrapping

Supply hoses route around solid `Obstacles` colliders on the XY ground plane. Building box footprints support rotation and scale; other collider shapes use conservative bounds for detours. Contact corners persist until the hose has a clear path past them. Wrapped spans become taut, while unobstructed hose retains its ground/slack simulation. Interior hose points stay locked one hose radius above the configured ground plane, including when corners release; only the end spans rise to the source and nozzle. Length limits and break strain use the remaining distance from the last contact corner, including hose already routed around buildings. Trigger colliders do not snag hoses.
