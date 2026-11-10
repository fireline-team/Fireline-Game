using System.Collections.Generic;
using UnityEngine;

namespace Fireline.Missions
{
    /// <summary>A world-space label the mission HUD draws over something the team must deal with.</summary>
    public readonly struct ObjectiveMarker
    {
        public readonly Vector3 Position;
        public readonly string Label;
        public readonly Color Color;

        public ObjectiveMarker(Vector3 position, string label, Color color)
        {
            Position = position;
            Label = label;
            Color = color;
        }
    }

    /// <summary>
    /// Implemented by steps that want markers over their targets ("FIRE 2", "DEBRIS 1").
    /// The HUD asks for markers without knowing what kind of step it is talking to.
    /// </summary>
    public interface IObjectiveMarkerSource
    {
        /// <summary>Adds markers for the targets still left. Does not clear the list.</summary>
        void CollectMarkers(List<ObjectiveMarker> markers);
    }
}
