using System.Collections.Generic;
using UnityEngine;

namespace Game.Runtime
{
    /// <summary>Warm realtime light for the fire sprite, with a shared budget for large hordes.</summary>
    [DisallowMultipleComponent]
    public sealed class EnemyFireLight : MonoBehaviour
    {
        public const int MaxRealtimeLights = 24;

        [SerializeField] private SpriteRenderer spriteRenderer;
        [Tooltip("Realtime Point Light owned by this enemy. Tune its color, intensity and range on the child.")]
        [SerializeField] private Light pointLight;
        [Tooltip("World-space offset from the sprite center. Negative Z lifts the light toward the camera on the XY ground plane.")]
        [SerializeField] private Vector3 lightOffset = new Vector3(0f, 0f, -0.65f);

        private static readonly List<EnemyFireLight> Active = new List<EnemyFireLight>(128);
        private static readonly EnemyFireLight[] Selected = new EnemyFireLight[MaxRealtimeLights];
        private static readonly float[] Scores = new float[MaxRealtimeLights];
        private static readonly Plane[] Frustum = new Plane[6];
        private static int _lastFrame = -1;
        private static float _nextSelection;
        private static float _nextCameraRefresh;
        private static Camera _camera;

        private float _baseIntensity;
        private float _baseRange;
        private Color _baseColor;
        private Material _fireMaterial;
        private bool _selected;

        private static readonly int FlickerAmount = Shader.PropertyToID("_FlickerAmount");
        private static readonly int FlickerSpeed = Shader.PropertyToID("_FlickerSpeed");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Active.Clear();
            System.Array.Clear(Selected, 0, Selected.Length);
            _lastFrame = -1;
            _nextSelection = _nextCameraRefresh = 0f;
            _camera = null;
        }

        private void Awake()
        {
            if (spriteRenderer == null) spriteRenderer = GetComponentInChildren<SpriteRenderer>(true);
            if (pointLight == null) pointLight = GetComponentInChildren<Light>(true);
            if (pointLight == null) return;
            _baseIntensity = pointLight.intensity;
            _baseRange = pointLight.range;
            _baseColor = pointLight.color;
            _fireMaterial = spriteRenderer != null ? spriteRenderer.sharedMaterial : null;
            pointLight.enabled = false;
        }

        private void OnEnable()
        {
            if (pointLight == null || spriteRenderer == null) return;
            Active.Add(this);
            _nextSelection = 0f;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            _selected = false;
            if (pointLight != null) pointLight.enabled = false;
            _nextSelection = 0f;
        }

        private void LateUpdate()
        {
            // One pass for the whole crowd, regardless of how many enemies have this component.
            if (_lastFrame == Time.frameCount) return;
            _lastFrame = Time.frameCount;
            if (Time.unscaledTime >= _nextSelection)
            {
                SelectNearbyLights();
                _nextSelection = Time.unscaledTime + 0.15f;
            }
            for (int i = 0; i < Selected.Length; i++)
            {
                EnemyFireLight fire = Selected[i];
                if (fire != null && fire.isActiveAndEnabled) fire.TickLight();
            }
        }

        private static void SelectNearbyLights()
        {
            if (_camera == null || !_camera.isActiveAndEnabled || Time.unscaledTime >= _nextCameraRefresh)
            {
                _camera = Camera.main;
                _nextCameraRefresh = Time.unscaledTime + 0.5f;
            }
            bool hasCamera = _camera != null;
            Vector3 origin = hasCamera ? _camera.transform.position : Vector3.zero;
            if (hasCamera) GeometryUtility.CalculateFrustumPlanes(_camera, Frustum);
            System.Array.Clear(Selected, 0, Selected.Length);
            for (int i = 0; i < Scores.Length; i++) Scores[i] = float.PositiveInfinity;

            for (int i = 0; i < Active.Count; i++)
            {
                EnemyFireLight fire = Active[i];
                if (fire.spriteRenderer == null || fire.pointLight == null) continue;
                Bounds influence = fire.spriteRenderer.bounds;
                float range = fire.ScaledRange;
                // Include nearby offscreen fires when their light can reach the visible world.
                influence.Expand(range * 2f);
                if (!fire.spriteRenderer.enabled || !fire.spriteRenderer.gameObject.activeInHierarchy || fire.spriteRenderer.color.a <= 0f
                    || (hasCamera && !GeometryUtility.TestPlanesAABB(Frustum, influence))) continue;
                float score = (fire.spriteRenderer.bounds.center - origin).sqrMagnitude;
                if (fire._selected) score *= 0.85f; // Hysteresis prevents lights swapping at equal distances.
                for (int slot = 0; slot < Selected.Length; slot++)
                {
                    if (score >= Scores[slot]) continue;
                    for (int move = Selected.Length - 1; move > slot; move--)
                    {
                        Selected[move] = Selected[move - 1];
                        Scores[move] = Scores[move - 1];
                    }
                    Selected[slot] = fire;
                    Scores[slot] = score;
                    break;
                }
            }
            for (int i = 0; i < Active.Count; i++) Active[i]._selected = false;
            for (int i = 0; i < Selected.Length; i++)
                if (Selected[i] != null) Selected[i]._selected = true;
            for (int i = 0; i < Active.Count; i++)
                if (!Active[i]._selected && Active[i].pointLight != null) Active[i].pointLight.enabled = false;
        }

        private float ScaledRange => _baseRange * Mathf.Sqrt(Mathf.Max(0.01f, Mathf.Abs(transform.lossyScale.x)));

        private void TickLight()
        {
            if (pointLight == null || spriteRenderer == null) return;
            float amount = 0.12f;
            float speed = 8f;
            if (_fireMaterial != null && _fireMaterial.HasProperty(FlickerAmount))
            {
                amount = _fireMaterial.GetFloat(FlickerAmount);
                speed = _fireMaterial.GetFloat(FlickerSpeed);
            }
            Vector3 spritePosition = spriteRenderer.transform.position;
            float phase = Vector3.Dot(spritePosition, new Vector3(1.37f, 2.11f, 0.83f));
            float time = Time.time * speed;
            float flicker = 1f + amount *
                (0.65f * Mathf.Sin(time + phase) + 0.35f * Mathf.Sin(time * 2.31f + phase * 1.73f));
            Color tint = _baseColor * spriteRenderer.color;
            float peak = Mathf.Max(0.001f, Mathf.Max(tint.r, Mathf.Max(tint.g, tint.b)));
            pointLight.color = new Color(tint.r / peak, tint.g / peak, tint.b / peak);
            pointLight.intensity = _baseIntensity * flicker * spriteRenderer.color.a;
            pointLight.range = ScaledRange;
            pointLight.transform.position = spriteRenderer.bounds.center + lightOffset;
            pointLight.enabled = spriteRenderer.enabled && spriteRenderer.gameObject.activeInHierarchy && spriteRenderer.color.a > 0f;
        }
    }
}
