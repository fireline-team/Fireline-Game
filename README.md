# Fireline-Game
This repository will hold the project for our GAME 340-440 sequence capstone.

## Mission gameplay loop

Open `src/Fireline/Assets/_Scenes/MainMenu.unity` in Unity and press Play. MainMenu is also the first enabled build scene. Open the fire rescue mission, then Start to load JakeProtoLevel. Use the existing arena join controls to join players.

Complete the objectives to enable EVAC, escort the civilians, and return the team to the evacuation zone for extraction. Success results appear only after extraction; a full team wipe produces failure results. Results include objective progress, civilians saved/lost, and mission time. Exit to the main menu to launch a fresh mission.

Click Pause, press Escape, or press controller Start to pause/resume. Menu and briefing support Enter/controller A; controller B closes the briefing. Results support Enter/A after a short input guard. The pause menu also offers an exit to the main menu.

`GameManager` persists across menu/arena loads and owns session state and time. `FireRescueMission` continues to own objective and extraction rules. Opening prototype arenas directly retains their existing standalone reset controls; launching through MainMenu replaces those shortcuts with results and menu navigation.

Play Mode coverage: `GameManagerTests` checks pause during hit-stop, team wipe versus a revivable teammate, results/menu/replay, and objective completion versus successful extraction.

### Hose wrapping

Supply hoses route around solid `Obstacles` colliders on the XY ground plane. Building box footprints support rotation and scale; other collider shapes use conservative bounds for detours. Corner collisions repair only the intersecting section of hose, preserving slack and momentum elsewhere. Each repaired section keeps its share of the hose length, so player tension pulls slack toward corners through the simulation instead of straightening the entire hose. Interior hose points stay locked to a small configurable ground clearance (0.01 units by default), using the hydrant base rather than its raised outlet as the ground plane; only the end spans rise to the source and nozzle. Length limits and break strain use the remaining distance from the last contact corner, including hose already routed around buildings. Trigger colliders do not snag hoses. If aiming pushes the nozzle into or through a wall, the rope end retracts along the player-to-nozzle line to a clear point on the player’s side. The weapon transform stays unchanged, the hose stays visible, and the end returns to the nozzle when clear. When sliding close to a wall, the clear attachment point follows the current player position. Movement limits still measure the real held nozzle, so visual retraction cannot grant extra travel or cause a delayed snap back.
