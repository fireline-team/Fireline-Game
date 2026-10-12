using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// Exposes the axe's moments as Inspector events, so feedback tools can be wired up with
/// no code. Made with Feel in mind: drag an MMF Player into a slot and pick
/// MMF_Player > PlayFeedbacks (or PlayFeedbacks(Vector3) under "Dynamic Vector3" to spawn
/// the effect at the hit point).
///
/// Each event fires at the moment the blade reaches the target, not when the button is pressed.
/// Hit points are where the blade met the target, raised to orbit height so effects appear
/// at the axe rather than on the ground.
///
/// When Feel handles freeze frames, untick Use Hit Freeze on AxeSwingVisual so they don't double up.
/// </summary>
[DisallowMultipleComponent]
public class AxeFeedbackEvents : MonoBehaviour
{
    [System.Serializable] public class PointEvent : UnityEvent<Vector3> { }

    [Tooltip("How high above the ground to report hit points (match AxeSwingVisual's Orbit Height).")]
    [SerializeField, Min(0f)] private float pointHeight = 0.6f;

    [Header("Events")]
    [Tooltip("A swing starts: whoosh sound, swing animation.")]
    [SerializeField] private UnityEvent onSwing = new UnityEvent();
    [Tooltip("The swing ended without touching anything.")]
    [SerializeField] private UnityEvent onWhiff = new UnityEvent();
    [Tooltip("The blade hit an enemy, or anything else that takes a normal hit. Fires once per target, so keep it light: a thunk, small sparks.")]
    [SerializeField] private PointEvent onEnemyHit = new PointEvent();
    [Tooltip("Fires once per swing on the first enemy hit: good for camera shake that shouldn't stack.")]
    [SerializeField] private PointEvent onFirstEnemyHit = new PointEvent();
    [Tooltip("The blade hit a locker or water source.")]
    [SerializeField] private PointEvent onInteractableHit = new PointEvent();
    [Tooltip("A successful parry.")]
    [SerializeField] private PointEvent onParry = new PointEvent();

    private AxeSwing _axe;
    private bool _hitEnemyThisSwing;

    private void Awake()
    {
        _axe = GetComponentInParent<AxeSwing>();
        if (_axe == null)
        {
            Debug.LogError($"{name}: AxeFeedbackEvents needs an AxeSwing on the player.", this);
            enabled = false;
        }
    }

    private void OnEnable()
    {
        if (_axe == null) return;
        _axe.SwingStarted += HandleSwingStarted;
        _axe.TargetHit += HandleTargetHit;
        _axe.InteractableHit += HandleInteractableHit;
        _axe.Swung += HandleSwung;
    }

    private void OnDisable()
    {
        if (_axe == null) return;
        _axe.SwingStarted -= HandleSwingStarted;
        _axe.TargetHit -= HandleTargetHit;
        _axe.InteractableHit -= HandleInteractableHit;
        _axe.Swung -= HandleSwung;
    }

    private Vector3 Raise(Vector2 groundPoint) => (Vector3)groundPoint + TiltedView.Up * pointHeight;

    private void HandleSwingStarted()
    {
        _hitEnemyThisSwing = false;
        onSwing.Invoke();
    }

    private void HandleTargetHit(AxeHit hit, IAxeHittable target, AxeHitResult result)
    {
        Vector3 point = Raise(hit.Point);
        if (result == AxeHitResult.Parried)
        {
            onParry.Invoke(point);
            return;
        }

        onEnemyHit.Invoke(point);
        if (!_hitEnemyThisSwing)
        {
            _hitEnemyThisSwing = true;
            onFirstEnemyHit.Invoke(point);
        }
    }

    private void HandleInteractableHit(Interactable target, Vector2 point) => onInteractableHit.Invoke(Raise(point));

    private void HandleSwung(AxeSwingResult result)
    {
        if (result.Whiffed) onWhiff.Invoke();
    }
}
