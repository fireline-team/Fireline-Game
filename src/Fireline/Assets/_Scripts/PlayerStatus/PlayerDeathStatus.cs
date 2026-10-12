using UnityEngine;

/// <summary>Health adapter. Other systems can independently set/clear their own marker statuses.</summary>
[RequireComponent(typeof(PlayerStatusMarker), typeof(PlayerHealth))]
public class PlayerDeathStatus : MonoBehaviour
{
    [SerializeField] private PlayerStatusDefinition deadStatus;
    private PlayerHealth health;
    private PlayerStatusMarker marker;

    private void Awake()
    {
        health = GetComponent<PlayerHealth>();
        marker = GetComponent<PlayerStatusMarker>();
    }
    private void OnEnable()
    {
        health.Died += ShowDeath;
        if (health.IsDead) ShowDeath();
    }
    private void ShowDeath() => marker.SetStatus(this, deadStatus);
    private void OnDisable()
    {
        if (health != null) health.Died -= ShowDeath;
        if (marker != null) marker.ClearStatus(this);
    }
}
