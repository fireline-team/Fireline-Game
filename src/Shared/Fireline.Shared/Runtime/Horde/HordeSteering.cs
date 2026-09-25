using System;
using System.Collections.Generic;
using System.Numerics;

namespace Fireline.Shared.Horde
{
    public static class HordeSteering
    {
        private const float Epsilon = 0.000001f;

        public static int FindNearest(Vector2 from, IReadOnlyList<Vector2> targets)
        {
            if (targets == null) throw new ArgumentNullException(nameof(targets));

            int best = -1;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < targets.Count; i++)
            {
                float sqr = Vector2.DistanceSquared(from, targets[i]);
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = i;
                }
            }
            return best;
        }

        public static Vector2 Seek(Vector2 from, Vector2 target, float stopDistance)
        {
            Vector2 toTarget = target - from;
            float distSqr = toTarget.LengthSquared();

            if (distSqr <= stopDistance * stopDistance || distSqr < Epsilon)
                return Vector2.Zero;

            return toTarget / MathF.Sqrt(distSqr);
        }

        public static Vector2 Separation(
            int selfIndex,
            Vector2[] positions,
            List<int> candidateIds,
            float radius,
            int maxNeighbors)
        {
            if (positions == null) throw new ArgumentNullException(nameof(positions));
            if (candidateIds == null) throw new ArgumentNullException(nameof(candidateIds));

            Vector2 self = positions[selfIndex];
            float radiusSqr = radius * radius;
            Vector2 push = Vector2.Zero;
            int reacted = 0;

            for (int n = 0; n < candidateIds.Count && reacted < maxNeighbors; n++)
            {
                int j = candidateIds[n];
                if (j == selfIndex) continue;

                Vector2 away = self - positions[j];
                float dSqr = away.LengthSquared();
                if (dSqr >= radiusSqr) continue;

                if (dSqr < Epsilon)
                {
                    push += selfIndex < j ? Vector2.UnitX : -Vector2.UnitX;
                }
                else
                {
                    float d = MathF.Sqrt(dSqr);
                    push += away / d * (1f - d / radius);
                }
                reacted++;
            }

            return push;
        }
    }
}
