using System.Collections.Generic;
using UnityEngine;

public abstract class Interactable : MonoBehaviour
{
    private static readonly List<Interactable> Active = new List<Interactable>();

    [Tooltip("How close a player has to be to use this.")]
    [SerializeField, Min(0.1f)] private float interactRadius = 1.25f;

    public float InteractRadius => interactRadius;

    protected virtual void OnEnable() => Active.Add(this);
    protected virtual void OnDisable() => Active.Remove(this);
    
    public abstract string GetPrompt(PlayerInteractor player);
    
    public virtual bool CanInteract(PlayerInteractor player) => true;

    public abstract void Interact(PlayerInteractor player);
    
    public static Interactable FindNearest(Vector2 position, PlayerInteractor player)
    {
        Interactable best = null;
        float bestSqr = float.MaxValue;

        for (int i = 0; i < Active.Count; i++)
        {
            Interactable candidate = Active[i];
            float sqr = ((Vector2)candidate.transform.position - position).sqrMagnitude;
            float reach = candidate.interactRadius;

            if (sqr <= reach * reach && sqr < bestSqr && candidate.CanInteract(player))
            {
                best = candidate;
                bestSqr = sqr;
            }
        }
        return best;
    }
    
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    private static void ClearRegistry() => Active.Clear();

#if UNITY_EDITOR
    protected virtual void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.8f);
        Gizmos.DrawWireSphere(transform.position, interactRadius);
    }
#endif
}
