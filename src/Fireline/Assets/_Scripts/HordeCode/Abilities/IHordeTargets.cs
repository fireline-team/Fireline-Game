using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// What enemy abilities need to know about the players. HordeManager provides it, but
    /// abilities only see this interface, so they can be tested or reused without the manager.
    /// </summary>
    public interface IHordeTargets
    {
        /// <summary>The closest living player to <paramref name="from"/>, if any.</summary>
        bool TryGetNearestPlayer(Vector2 from, out Vector2 position);
    }
}
