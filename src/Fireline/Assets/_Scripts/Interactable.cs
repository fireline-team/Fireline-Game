using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    private static readonly List<Interactable> Active = new List<Interactable>();

    [Tooltip("Size of this object for axe hits when it has no Collider2D. With a collider, the collider's shape is used instead.")]
    [SerializeField, Min(0f)] private float hitRadius = 0.3f;

    private Collider2D _collider;

    public float HitRadius => hitRadius;

    protected virtual void OnEnable()
    {
        _collider = GetComponent<Collider2D>();
        Active.Add(this);
    }

    protected virtual void OnDisable() => Active.Remove(this);

    public abstract string GetPrompt(PlayerInteractor player);

    public virtual bool CanInteract(PlayerInteractor player) => true;

    public abstract void Interact(PlayerInteractor player);

    private bool HasCollider => _collider != null && _collider.enabled;

    /// <summary>Point on this object nearest to <paramref name="from"/>, on the ground.</summary>
    public Vector2 ClosestPoint(Vector2 from) => HasCollider ? _collider.ClosestPoint(from) : (Vector2)transform.position;
    
    public bool IsInArc(Vector2 origin, Vector2 facing, float range, float arcDegrees, out float distance)
    {
        float padding = HasCollider ? 0f : hitRadius;
        return AxeSwing.IsInArc(origin, facing, range, arcDegrees, ClosestPoint(origin), padding, out distance);
    }
    
    public static Interactable FindInArc(Vector2 origin, Vector2 facing, float range, float arcDegrees, PlayerInteractor player)
    {
        Interactable best = null;
        float bestDistance = float.MaxValue;

        for (int i = 0; i < Active.Count; i++)
        {
            Interactable candidate = Active[i];
            if (candidate.IsInArc(origin, facing, range, arcDegrees, out float distance)
                && distance < bestDistance && candidate.CanInteract(player))
            {
                best = candidate;
                bestDistance = distance;
            }
        }
        return best;
    }

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() => Active.Clear();

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        if (GetComponent<Collider2D>() != null) return; // the collider outline already shows the hit area
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, hitRadius);
    }
#endif
}