using System;

namespace Fireline.Shared.Infrastructure
{
    /// <summary>Health rules shared by players and pooled enemies; independent of Unity.</summary>
    public sealed class HealthPool
    {
        public float Maximum { get; private set; }
        public float Current { get; private set; }
        public bool IsDead => Current <= 0f;

        public HealthPool(float maximum) => Reset(maximum);

        public void Reset(float maximum)
        {
            if (float.IsNaN(maximum) || float.IsInfinity(maximum) || maximum <= 0f)
                throw new ArgumentOutOfRangeException(nameof(maximum));
            Maximum = maximum;
            Current = maximum;
        }

        /// <returns>True only for the hit that transitions from alive to dead.</returns>
        public bool TakeDamage(float amount)
        {
            if (IsDead || amount <= 0f || float.IsNaN(amount) || float.IsInfinity(amount))
                return false;
            Current = Math.Max(0f, Current - amount);
            return IsDead;
        }
    }
}
