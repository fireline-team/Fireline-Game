using UnityEngine;

/// <summary>Presentation only: burning never adds another damage collider to a downed player.</summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDownedFire : MonoBehaviour
{
    [SerializeField] private GameObject fireVisualPrefab;
    [SerializeField] private Vector3 fireScale = new Vector3(1.3f, 2f, 1f);
    private PlayerHealth health;
    private GameObject fire;
    public bool IsBurningVisible => fire != null && fire.activeInHierarchy;

    private void Awake() => health = GetComponent<PlayerHealth>();
    private void OnEnable()
    {
        health.Died += Show;
        health.Revived += Hide;
        if (health.IsDowned) Show();
    }
    private void Show()
    {
        if (fire == null && fireVisualPrefab != null)
        {
            fire = Instantiate(fireVisualPrefab, transform);
            fire.name = "Downed player flames";
            
            fire.transform.localPosition = TiltedView.Up * 0.6f + Vector3.back * 0.12f;
            fire.transform.rotation = TiltedView.StandingRotation;
            
            // Set fire scale
            fire.transform.localScale = fireScale;
            
            foreach (var renderer in fire.GetComponentsInChildren<Renderer>()) renderer.sortingOrder = 1;
            foreach (var collider in fire.GetComponentsInChildren<Collider>()) collider.enabled = false;
            foreach (var collider in fire.GetComponentsInChildren<Collider2D>()) collider.enabled = false;
        }
        if (fire != null) { fire.SetActive(true); Update(); }
    }
    private void Update()
    {
        if (fire == null || !fire.activeSelf) return;
        // Existing fire material animates; shrinking/flickering makes extinguishing visible too.
        float strength = Mathf.Lerp(1f, 0.5f, health.ReviveProgress);
        float flicker = 1f; //1f + 0.08f * Mathf.Sin(Time.time * 13f);
        fire.transform.localScale = fireScale * (strength * flicker);
    }
    private void Hide() { if (fire != null) fire.SetActive(false); }
    private void OnDisable()
    {
        if (health != null) { health.Died -= Show; health.Revived -= Hide; }
        Hide();
    }
}
