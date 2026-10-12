using UnityEngine;

/// <summary>
/// Draws the axe orbiting the player across the swing arc, with a trail behind it, and
/// triggers hit-freeze on lockers, water sources and parries. Purely visual: all the gameplay lives on AxeSwing,
/// and this just follows its blade, so the sprite and the hits always line up (and a
/// hit-freeze pauses the sprite mid-swing too).
///
/// Setup:
///   1. Make a child of the Player called "Axe" with a SpriteRenderer holding the axe sprite.
///      Draw/import the axe pointing RIGHT (head on the right), or use Sprite Angle Offset.
///   2. Add this component to the Player (next to AxeSwing) and drag the Axe child into Axe Sprite.
///   3. A TrailRenderer is added to the axe automatically if it doesn't have one. To style it,
///      add your own TrailRenderer to the Axe (or a child at the blade tip) and assign it.
/// </summary>
[DisallowMultipleComponent]
public class AxeSwingVisual : MonoBehaviour
{
    [Header("Axe")]
    [Tooltip("The axe sprite object (a child of the player).")]
    [SerializeField] private SpriteRenderer axeSprite;
    [Tooltip("How far from the player's feet the axe head travels, in world units. 0 = use the swing's Range.")]
    [SerializeField, Min(0f)] private float orbitRadius = 0f;
    [Tooltip("How high off the ground the orbit is, e.g. hand/waist height.")]
    [SerializeField, Min(0f)] private float orbitHeight = 0.6f;
    [Tooltip("Rotation applied to the sprite if the art doesn't point right. E.g. -90 if it points up.")]
    [SerializeField] private float spriteAngleOffset = 0f;
    [Tooltip("Mirror the sprite when swinging the other way, so the blade edge always leads.")]
    [SerializeField] private bool flipWithSwingDirection = true;
    [Tooltip("Hide the axe between swings.")]
    [SerializeField] private bool hideWhenIdle = true;

    [Header("Trail")]
    [Tooltip("Leave empty to auto-create one on the axe.")]
    [SerializeField] private TrailRenderer trail;
    [Tooltip("Used only for the auto-created trail.")]
    [SerializeField] private Color trailColor = new Color(1f, 1f, 1f, 0.8f);
    [Tooltip("Used only for the auto-created trail. Seconds the trail lingers.")]
    [SerializeField, Min(0.01f)] private float trailTime = 0.12f;
    [Tooltip("Used only for the auto-created trail. Width at the axe, in world units.")]
    [SerializeField, Min(0.01f)] private float trailWidth = 0.35f;

    [Header("Hit Freeze")]
    [Tooltip("Built-in hit-freeze. Turn off once Feel (or another feedback tool) handles it via AxeFeedbackEvents.")]
    [SerializeField] private bool useHitFreeze = true;
    [Tooltip("Freeze when the blade hits a locker or water source. 0 = off.")]
    [SerializeField, Min(0f)] private float interactableFreeze = 0.06f;
    [Tooltip("Freeze on a parry; a bit longer sells it. 0 = off.")]
    [SerializeField, Min(0f)] private float parryFreeze = 0.12f;
    [Tooltip("Freeze on regular enemy hits. Off by default: freezing while mowing through a crowd feels sluggish. " +
             "0 = off. If you turn it on, it only triggers once per swing.")]
    [SerializeField, Min(0f)] private float enemyFreeze = 0f;
    [Tooltip("0 = full stop. A small value like 0.05 gives slow-mo instead of a hard stop.")]
    [SerializeField, Range(0f, 1f)] private float freezeTimeScale = 0f;

    private AxeSwing _axe;
    private bool _frozeThisSwing;
    private bool _wasSwinging;

    private void Awake()
    {
        _axe = GetComponentInParent<AxeSwing>();
        if (_axe == null)
        {
            Debug.LogError($"{name}: AxeSwingVisual needs an AxeSwing on the player.", this);
            enabled = false;
            return;
        }
        if (axeSprite == null)
        {
            Debug.LogWarning($"{name}: AxeSwingVisual has no Axe Sprite assigned, so nothing will be drawn.", this);
            return;
        }
        if (trail == null) trail = CreateTrail();

        SetVisible(!hideWhenIdle);
        if (trail != null) trail.emitting = false;
    }

    private void OnEnable()
    {
        if (_axe == null) return;
        _axe.SwingStarted += OnSwingStarted;
        _axe.TargetHit += OnTargetHit;
        _axe.InteractableHit += OnInteractableHit;
    }

    private void OnDisable()
    {
        if (_axe == null) return;
        _axe.SwingStarted -= OnSwingStarted;
        _axe.TargetHit -= OnTargetHit;
        _axe.InteractableHit -= OnInteractableHit;
    }

    private void OnSwingStarted()
    {
        _frozeThisSwing = false;
        if (axeSprite == null) return;

        PlaceAxe();         // jump to the start of the arc first...
        if (trail != null)
        {
            trail.Clear();  // ...so the trail doesn't streak from wherever the axe was last
            trail.emitting = true;
        }
        SetVisible(true);
    }

    private void OnTargetHit(AxeHit hit, IAxeHittable target, AxeHitResult result)
    {
        if (!useHitFreeze) return;

        if (result == AxeHitResult.Parried)
        {
            HitStop.Freeze(parryFreeze, freezeTimeScale); // every parry counts
        }
        else if (enemyFreeze > 0f && !_frozeThisSwing)
        {
            _frozeThisSwing = true;
            HitStop.Freeze(enemyFreeze, freezeTimeScale);
        }
    }

    private void OnInteractableHit(Interactable target, Vector2 point)
    {
        if (useHitFreeze) HitStop.Freeze(interactableFreeze, freezeTimeScale);
    }

    private void LateUpdate()
    {
        if (axeSprite == null) return;

        bool swinging = _axe.IsSwinging;
        if (swinging) PlaceAxe();

        if (_wasSwinging && !swinging)
        {
            if (trail != null) trail.emitting = false; // let the existing trail fade out
            if (hideWhenIdle) SetVisible(false);
        }
        _wasSwinging = swinging;
    }

    private void PlaceAxe()
    {
        Vector2 dir = _axe.BladeDirection;
        float radius = orbitRadius > 0f ? orbitRadius : _axe.Range;

        // Along the ground from the feet, then lifted "up" in the tilted view.
        Vector3 feet = transform.position;
        Vector3 pos = feet + (Vector3)(dir * radius) + TiltedView.Up * orbitHeight;

        float angle = Mathf.Atan2(dir.y, dir.x) * Mathf.Rad2Deg + spriteAngleOffset;
        Transform t = axeSprite.transform;
        t.SetPositionAndRotation(pos, Quaternion.Euler(0f, 0f, angle));

        // Mirror across the swing's travel direction so the blade edge leads.
        if (flipWithSwingDirection) axeSprite.flipY = _axe.SwingSign < 0f;
    }

    private void SetVisible(bool visible)
    {
        if (axeSprite != null) axeSprite.enabled = visible;
    }

    private TrailRenderer CreateTrail()
    {
        TrailRenderer t = axeSprite.GetComponent<TrailRenderer>();
        if (t == null) t = axeSprite.gameObject.AddComponent<TrailRenderer>();

        t.time = trailTime;
        t.minVertexDistance = 0.02f;
        t.widthCurve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(1f, 0f));
        t.widthMultiplier = trailWidth;
        t.numCapVertices = 2;
        t.alignment = LineAlignment.View;

        var gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(trailColor, 0f), new GradientColorKey(trailColor, 1f) },
            new[] { new GradientAlphaKey(trailColor.a, 0f), new GradientAlphaKey(0f, 1f) });
        t.colorGradient = gradient;

        // Unlit sprite shader so it works in URP 2D and the built-in pipeline.
        Shader shader = Shader.Find("Universal Render Pipeline/2D/Sprite-Unlit-Default");
        if (shader == null) shader = Shader.Find("Sprites/Default");
        if (shader != null) t.sharedMaterial = new Material(shader);

        // Draw just behind the axe sprite.
        t.sortingLayerID = axeSprite.sortingLayerID;
        t.sortingOrder = axeSprite.sortingOrder - 1;
        return t;
    }
}