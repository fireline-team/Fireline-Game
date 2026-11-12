using UnityEngine;

namespace Game.Runtime
{
    /// <summary>
    /// Makes a freshly spawned enemy climb out of the ground: the sprite grows upward from its
    /// feet with a little wobble. While rising the enemy doesn't move, attack or hurt on contact.
    /// Placeholder until there's proper VFX; swap the visual here without touching anything else.
    /// </summary>
    internal sealed class SpawnRise
    {
        private readonly Transform _visual;
        private readonly Vector3 _restPosition;
        private readonly Vector3 _restScale;

        private float _duration;
        private float _elapsed;
        private float _pivotFraction;
        private float _height;

        public bool IsRising => _elapsed < _duration;

        /// <param name="visual">The sprite's own transform. Null (or the enemy root) disables the visual part.</param>
        public SpawnRise(Transform visual)
        {
            _visual = visual;
            if (_visual == null) return;
            _restPosition = _visual.localPosition;
            _restScale = _visual.localScale;
        }

        public void Begin(SpriteRenderer sprite, float duration)
        {
            _duration = Mathf.Max(0f, duration);
            _elapsed = 0f;
            _pivotFraction = 0.5f;
            _height = 1f;
            if (sprite != null && sprite.sprite != null)
            {
                Sprite art = sprite.sprite;
                _pivotFraction = art.rect.height > 0f ? art.pivot.y / art.rect.height : 0.5f;
                _height = art.bounds.size.y;
            }
            Apply(IsRising ? 0f : 1f);
        }

        public void Tick(float deltaTime)
        {
            if (!IsRising) return;
            _elapsed += deltaTime;
            Apply(Mathf.Clamp01(_elapsed / _duration));
        }

        /// <summary>Snap to fully risen (e.g. when the enemy is recycled mid-rise).</summary>
        public void Finish()
        {
            _elapsed = _duration;
            Apply(1f);
        }

        private void Apply(float t)
        {
            if (_visual == null) return;
            float grown = 1f - (1f - t) * (1f - t) * (1f - t); // ease out: fast start, soft landing
            float s = Mathf.Max(0.02f, grown);

            // Grow from the feet: keep the sprite's bottom edge where it is while it scales.
            float sink = -_pivotFraction * _height * _restScale.y * (1f - s);
            float wobble = t < 1f ? Mathf.Sin(t * 40f) * 0.04f * (1f - t) : 0f;

            _visual.localScale = new Vector3(_restScale.x, _restScale.y * s, _restScale.z);
            _visual.localPosition = _restPosition + _visual.localRotation * new Vector3(wobble, sink, 0f);
        }
    }
}
