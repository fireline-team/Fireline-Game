using System;

namespace Fireline.Shared
{
    /// <summary>The 8 compass directions, counter-clockwise starting from East (matches Atan2).</summary>
    public enum Facing8
    {
        E = 0, NE = 1, N = 2, NW = 3, W = 4, SW = 5, S = 6, SE = 7
    }

    /// <summary>The 5 drawn poses from the art sheet. Left/right variants come from flipping.</summary>
    public enum SpritePose
    {
        Front = 0,      // S
        SouthWest = 1,  // SW (flip for SE)
        Side = 2,       // W  (flip for E)
        NorthWest = 3,  // NW (flip for NE)
        Back = 4        // N
    }

    /// <summary>
    /// Pure math, no UnityEngine dependency, so it can live in the shared library and be unit tested.
    /// Gameplay is on the XY plane: +X = east, +Y = north.
    /// </summary>
    public static class FacingDirection
    {
        /// <summary>Snap a direction vector to the nearest of 8 directions.</summary>
        public static Facing8 FromVector(float x, float y)
        {
            double degrees = Math.Atan2(y, x) * (180.0 / Math.PI); // -180..180, 0 = east
            int sector = (int)Math.Round(degrees / 45.0);           // -4..4
            sector = ((sector % 8) + 8) % 8;                        // wrap to 0..7
            return (Facing8)sector;
        }

        /// <summary>
        /// Which drawn pose to show and whether to mirror it.
        /// Assumes the side-ish art (SW, Side, NW) faces WEST. If yours faces east,
        /// pass artFacesWest = false and the flips invert.
        /// </summary>
        public static (SpritePose pose, bool flipX) ToPose(Facing8 facing, bool artFacesWest = true)
        {
            SpritePose pose;
            bool mirrored;

            switch (facing)
            {
                case Facing8.S:  pose = SpritePose.Front;     mirrored = false; break;
                case Facing8.SW: pose = SpritePose.SouthWest; mirrored = false; break;
                case Facing8.W:  pose = SpritePose.Side;      mirrored = false; break;
                case Facing8.NW: pose = SpritePose.NorthWest; mirrored = false; break;
                case Facing8.N:  pose = SpritePose.Back;      mirrored = false; break;
                case Facing8.NE: pose = SpritePose.NorthWest; mirrored = true;  break;
                case Facing8.E:  pose = SpritePose.Side;      mirrored = true;  break;
                case Facing8.SE: pose = SpritePose.SouthWest; mirrored = true;  break;
                default: throw new ArgumentOutOfRangeException(nameof(facing));
            }

            // Front/back are symmetric, never flip them.
            bool isSymmetric = pose == SpritePose.Front || pose == SpritePose.Back;
            bool flip = !isSymmetric && (mirrored == artFacesWest);
            return (pose, flip);
        }
    }
}
