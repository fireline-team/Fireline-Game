using UnityEngine;

/// <summary>Standalone visual tuning scene, independent of hose supply and combat.</summary>
public sealed class HoseVfxPreview : MonoBehaviour
{
    [SerializeField] private HoseVfxLibrary library;
    private readonly HoseGraphVisual[] _effects = new HoseGraphVisual[3];
    private float _nextBurst;

    public void SetLibrary(HoseVfxLibrary value) => library = value;

    private void Start()
    {
        if (library == null || !library.IsComplete) { enabled = false; return; }
        string[] labels = { "STREAM + IMPACT + STEAM", "MIST CONE + STEAM", "SNIPER BURST + IMPACT + STEAM" };
        for (int i = 0; i < 3; i++)
        {
            var go = new GameObject(labels[i]);
            go.transform.SetParent(transform, false);
            _effects[i] = go.AddComponent<HoseGraphVisual>();
            _effects[i].Initialize(library, (NozzleType)i, "Default", 20);
            var label = new GameObject("Label");
            label.transform.SetParent(transform, false);
            label.transform.position = new Vector3(-5f, 4.5f - i * 3.5f, 0f);
            var text = label.AddComponent<TextMesh>();
            text.text = labels[i];
            text.characterSize = 0.15f;
            text.fontSize = 40;
            text.color = new Color(0.75f, 0.9f, 1f);
        }
    }

    private void Update()
    {
        float sweep = Mathf.Sin(Time.time * 0.7f) * 0.08f;
        Vector2 direction = new Vector2(1f, sweep).normalized;
        for (int i = 0; i < 3; i++)
        {
            Vector3 origin = new Vector3(-5f, 3.4f - i * 3.5f, 0f);
            float range = i == 1 ? 3f : 8f;
            Vector3 hit = origin + (Vector3)direction * range;
            if (i < 2)
            {
                _effects[i].SetSpraying(true, origin, direction, range, i == 1 ? 2.2f : 0.6f, 0.3f, 90f, 0.6f);
                _effects[i].ShowContinuousImpact(true, hit, -direction);
                _effects[i].WaterFire(_effects[i], hit, Time.deltaTime * 30f);
            }
            else if (Time.time >= _nextBurst)
            {
                _nextBurst = Time.time + 0.8f;
                _effects[i].Pulse(origin, direction, range, 0.25f, 0.09f, 0.45f, 18, true, hit, -direction);
                _effects[i].WaterFire(_effects[i], hit, 15f);
            }
        }
    }
}
