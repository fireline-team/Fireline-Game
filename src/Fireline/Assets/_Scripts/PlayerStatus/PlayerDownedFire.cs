using UnityEngine;

/// <summary>Visual-only shader flames and steam for burning and downed players.</summary>
[RequireComponent(typeof(PlayerHealth))]
public class PlayerDownedFire : MonoBehaviour
{
    [SerializeField] private GameObject fireVisualPrefab;
    [SerializeField] private Vector3 fireScale = new Vector3(1.3f, 2f, 1f);
    private PlayerHealth health;
    private GameObject fire;
    private ExtinguishSteam steam;
    public bool IsBurningVisible => fire != null && fire.activeInHierarchy;
    public bool IsExtinguishSteamPlaying => steam != null && steam.IsPlaying;

    private void Awake() => health = GetComponent<PlayerHealth>();
    private void OnEnable()
    {
        health.Died += Show;
        health.Revived += OnExtinguished;
        health.Ignited += Refresh;
        health.Extinguished += OnExtinguished;
        Refresh();
    }

    private void Refresh()
    {
        if (health.IsDowned || health.IsBurning) Show();
        else Hide();
    }

    private void Show()
    {
        if (fire == null && fireVisualPrefab != null)
        {
            fire = Instantiate(fireVisualPrefab, transform);
            fire.name = "Player flames";
            fire.transform.localScale = fireScale;
            fire.transform.localPosition = TiltedView.Up * (fireScale.y * 0.5f) + Vector3.back * 0.12f;
            fire.transform.rotation = TiltedView.StandingRotation;

            var bodySprite = GetComponentInChildren<SpriteRenderer>();
            foreach (var renderer in fire.GetComponentsInChildren<Renderer>(true))
            {
                renderer.sortingLayerID = bodySprite != null ? bodySprite.sortingLayerID : 0;
                renderer.sortingOrder = bodySprite != null ? bodySprite.sortingOrder + 1 : 1;
            }
            foreach (var collider in fire.GetComponentsInChildren<Collider>(true)) collider.enabled = false;
            foreach (var collider in fire.GetComponentsInChildren<Collider2D>(true)) collider.enabled = false;

            // Retire the legacy prefab particles; hiding/showing flames must never restart them.
            foreach (var legacy in fire.GetComponentsInChildren<ParticleSystem>(true))
            {
                legacy.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
                var main = legacy.main;
                main.playOnAwake = false;
                legacy.gameObject.SetActive(false);
            }
            steam = ExtinguishSteam.Create(transform, fire.transform.position, bodySprite);
        }
        if (steam != null) steam.Clear();
        if (fire != null) { fire.SetActive(true); Update(); }
    }

    private void Update()
    {
        if (fire == null || !fire.activeSelf) return;
        float progress = health.IsDowned ? health.ReviveProgress : health.ExtinguishProgress;
        float strength = Mathf.Lerp(1f, 0.5f, progress);
        fire.transform.localScale = fireScale * strength;
        // Keep the bottom of the quad at the player's feet as the flame shrinks.
        fire.transform.localPosition = TiltedView.Up * (fireScale.y * strength * 0.5f) + Vector3.back * 0.12f;
    }

    private void OnExtinguished()
    {
        // Death clears the living burn status but must retain the downed fire effect.
        if (health.IsDowned || health.IsBurning) { Refresh(); return; }
        bool wasVisible = IsBurningVisible;
        Hide();
        if (wasVisible && steam != null)
        {
            steam.Play();
        }
    }

    private void Hide() { if (fire != null) fire.SetActive(false); }

    private void OnDisable()
    {
        if (health != null)
        {
            health.Died -= Show;
            health.Revived -= OnExtinguished;
            health.Ignited -= Refresh;
            health.Extinguished -= OnExtinguished;
        }
        Hide();
        if (steam != null) steam.Clear();
    }

    private void OnDestroy()
    {
        if (fire != null) Destroy(fire);
        if (steam != null) Destroy(steam.gameObject);
    }
}
