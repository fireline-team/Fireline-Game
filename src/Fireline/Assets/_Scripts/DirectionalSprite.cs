using Fireline.Shared;
using UnityEngine;

/// <summary>
/// Shows one of 5 drawn poses and flips it to cover all 8 directions.
/// By default it reads facing from how the object actually moved this frame, so it works
/// with any movement controller (players, enemies, civilians) without coupling to it.
/// Call SetFacing() to override, e.g. to face the hose's aim direction instead.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DirectionalSprite : MonoBehaviour
{
    [Tooltip("In order: Front (S), SouthWest, Side (W), NorthWest, Back (N)")]
    [SerializeField] private Sprite[] poses = new Sprite[5];

    [Tooltip("Uncheck if the artist drew the side/diagonal sprites facing east instead of west.")]
    [SerializeField] private bool artFacesWest = true;

    [Tooltip("Ignore tiny movements so the sprite doesn't jitter when nearly still.")]
    [SerializeField] private float minMoveDistance = 0.001f;

    [SerializeField] private Facing8 startFacing = Facing8.S;

    private SpriteRenderer spriteRenderer;
    private Vector3 lastPosition;
    private Facing8 current;
    private bool externallyControlled;

    public Facing8 Current => current;

    private void Awake()
    {
        spriteRenderer = GetComponent<SpriteRenderer>();
        lastPosition = transform.position;
        Apply(startFacing);
    }

    private void LateUpdate()
    {
        Vector3 delta = transform.position - lastPosition;
        lastPosition = transform.position;

        if (externallyControlled) return;

        // Gameplay is on XY. When standing still, keep the last facing.
        if (delta.sqrMagnitude > minMoveDistance * minMoveDistance)
            Apply(FacingDirection.FromVector(delta.x, delta.y));
    }

    /// <summary>Face a direction explicitly (e.g. aim). Pass Vector2.zero to go back to movement-based facing.</summary>
    public void SetFacing(Vector2 direction)
    {
        externallyControlled = direction.sqrMagnitude > 0.0001f;
        if (externallyControlled)
            Apply(FacingDirection.FromVector(direction.x, direction.y));
    }

    private void Apply(Facing8 facing)
    {
        current = facing;
        var (pose, flip) = FacingDirection.ToPose(facing, artFacesWest);
        spriteRenderer.sprite = poses[(int)pose];
        spriteRenderer.flipX = flip;
    }
}
