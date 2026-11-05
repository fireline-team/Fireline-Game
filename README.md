# Fireline-Game
This repository will hold the project for our GAME 340-440 sequence capstone.

## Mission gameplay loop

Open `src/Fireline/Assets/_Scenes/MainMenu.unity` in Unity and press Play. MainMenu is also the first enabled build scene. Open the fire rescue mission, then Start to load JakeProtoLevel. Use the existing arena join controls to join players.

Complete the objectives to enable EVAC, escort the civilians, and return the team to the evacuation zone for extraction. Success results appear only after extraction; a full team wipe produces failure results. Results include objective progress, civilians saved/lost, and mission time. Exit to the main menu to launch a fresh mission.

Click Pause, press Escape, or press controller Start to pause/resume. Menu and briefing support Enter/controller A; controller B closes the briefing. Results support Enter/A after a short input guard. The pause menu also offers an exit to the main menu.

`GameManager` persists across menu/arena loads and owns session state and time. `FireRescueMission` continues to own objective and extraction rules. Opening prototype arenas directly retains their existing standalone reset controls; launching through MainMenu replaces those shortcuts with results and menu navigation.

Play Mode coverage: `GameManagerTests` checks pause during hit-stop, team wipe versus a revivable teammate, results/menu/replay, and objective completion versus successful extraction.
