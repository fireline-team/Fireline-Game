using Fireline.Shared;
using UnityEngine;

/// <summary>
/// Shows one of 5 drawn poses and flips it to cover all 8 directions.
/// By default it reads facing from how the object actually moved this frame, so it works
/// with any movement controller (players, enemies, civilians) without coupling to it.
/// Call SetFacing() to override, e.g. to face the hose's aim direction instead.
///
/// Optional walk cycles: fill Walk Cycles (same 5-direction order as Poses) with the frames
/// sliced from each walk sheet. While the object moves, those frames loop; when it stops,
/// the idle pose is shown. Leave a direction empty to keep using its still pose.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class DirectionalSprite : MonoBehaviour
{
    [System.Serializable]
    public class FrameSet
    {
        public Sprite[] frames;
    }

    [Tooltip("Idle poses. In order: Front (S), SouthWest, Side (W), NorthWest, Back (N)")]
    [SerializeField] private Sprite[] poses = new Sprite[5];

    [Tooltip("Walk frames per direction, same order as Poses: Front, Front-side, Side, Back-side, Back")]
    [SerializeField] private FrameSet[] walkCycles = new FrameSet[5];

    [Tooltip("Walk animation speed in frames per second.")]
    [SerializeField] private float walkFps = 10f;

    [Tooltip("Uncheck if the artist drew the side/diagonal sprites facing east instead of west.")]
    [SerializeField] private bool artFacesWest = true;

    [Tooltip("Ignore tiny movements so the sprite doesn't jitter when nearly still.")]
    [SerializeField] private float minMoveDistance = 0.001f;

    [SerializeField] private Facing8 startFacing = Facing8.S;

    private SpriteRenderer spriteRenderer;
    private Vector3 lastPosition;
    private Facing8 current;
    private bool externallyControlled;
    private bool isMoving;
    private float walkTime;

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

        // Gameplay is on XY. When standing still, keep the last facing.
        isMoving = delta.sqrMagnitude > minMoveDistance * minMoveDistance;
        walkTime = isMoving ? walkTime + Time.deltaTime : 0f;

        if (!externallyControlled && isMoving)
            current = FacingDirection.FromVector(delta.x, delta.y);

        Apply(current);
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
        int i = (int)pose;

        Sprite sprite = poses[i];
        if (isMoving && walkCycles != null && i < walkCycles.Length)
        {
            Sprite[] frames = walkCycles[i]?.frames;
            if (frames != null && frames.Length > 0)
            {
                int frame = Mathf.FloorToInt(walkTime * walkFps) % frames.Length;
                if (frames[frame] != null) sprite = frames[frame];
            }
        }

        spriteRenderer.sprite = sprite;
        spriteRenderer.flipX = flip;
    }
}