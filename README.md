# Fireline-Game
This repository will hold the project for our GAME 340-440 sequence capstone.

### Hose wrapping

Supply hoses route around solid `Obstacles` colliders on the XY ground plane. Building box footprints support rotation and scale; other collider shapes use conservative bounds for detours. Contact corners persist until the hose has a clear path past them. Wrapped spans become taut, while unobstructed hose retains its ground/slack simulation. Length limits and break strain use the remaining distance from the last contact corner, including hose already routed around buildings. Trigger colliders do not snag hoses.
