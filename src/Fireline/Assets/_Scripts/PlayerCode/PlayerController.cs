using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerHealth))] // plus at least one hose: StandardHose, MistHose, or JetHose
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Shared camera boundary")]
    [Tooltip("The character body used to keep head, feet and sides inside the screen.")]
    [SerializeField] private SpriteRenderer boundarySprite;
    [Tooltip("Extra screen pixels between the character body and EVERY screen edge. Zero = no extra buffer.")]
    [SerializeField, Min(0f)] private float screenEdgePaddingPixels = 0f;

    [Header("Aiming")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float stickDeadzone = 0.2f;

    [Header("Animation")] 
    [SerializeField] private Animator _animator;
    private static readonly int IsMovingParam = Animator.StringToHash("IsMoving");

    private Rigidbody2D rb;
    private PlayerHealth health;
    private HoseLoadout loadout;
    private AxeSwing axe;
    private HoseWeapon fixedHose;
    private HoseWeapon hose => loadout != null && loadout.Current != null ? loadout.Current : fixedHose;
    private bool spraying;
    private float diedAt;
    private PlayerInput playerInput;

    private InputAction moveAction;
    private InputAction aimAction;
    private InputAction useWeaponAction;
    private InputAction interactAction;

    private Vector2 moveInput;
    private Vector2 aimDirection = Vector2.right;

    public Vector2 AimDirection => aimDirection;
    public float ScreenEdgePaddingPixels => screenEdgePaddingPixels;

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        if (boundarySprite == null && _animator != null)
            boundarySprite = _animator.GetComponent<SpriteRenderer>();
        health = GetComponent<PlayerHealth>();
        loadout = GetComponent<HoseLoadout>();
        axe = GetComponent<AxeSwing>();
        if (axe == null)
        {
            Debug.LogWarning($"{name} has no AxeSwing; adding one with default settings. Add it to the Player prefab to tune it.", this);
            axe = gameObject.AddComponent<AxeSwing>();
        }
        fixedHose = GetComponent<HoseWeapon>();
        if (hose == null)
            Debug.LogError($"{name} needs a hose component (StandardHose, MistHose, or JetHose).", this);
        health.Died += OnDied;
        playerInput = GetComponent<PlayerInput>();

        moveAction = playerInput.actions["Move"];
        aimAction = playerInput.actions["Aim"];
        useWeaponAction = playerInput.actions["UseWeapon"];
        interactAction = playerInput.actions["Interact"];

        if (aimCamera == null)
        {
            aimCamera = Camera.main;
        }
    }

    private void Update()
    {
        if (health.IsDead)
        {
            UpdateAnimation();
            if (health.CanResetScene && Time.unscaledTime - diedAt > 0.3f && interactAction.WasPressedThisFrame())
                health.ResetScene();
            return;
        }

        ReadMovement();
        ReadAim();
        UpdateAnimation();
        ReadActions();
    }

    private void FixedUpdate()
    {
        if (health.IsDead) return;
        Move();
        if (hose == null) return;
        if (spraying) hose.Spray(aimDirection, Time.fixedDeltaTime);
        else hose.StopSpraying();
    }

    private void ReadMovement()
    {
        moveInput = moveAction.ReadValue<Vector2>();

        // Prevent diagonal movement from being faster
        if (moveInput.sqrMagnitude > 1f)
        {
            moveInput.Normalize();
        }
    }

    private void Move()
    {
        Vector2 desired = rb.position + moveInput * moveSpeed * Time.fixedDeltaTime;
        Vector2 constrained = ConstrainToScreen(desired);
        rb.linearVelocity = (constrained - rb.position) / Time.fixedDeltaTime;
    }

    private readonly Vector3[] boundaryCorners = new Vector3[8];
    private readonly Plane[] screenPlanes = new Plane[6];
    private readonly float[] planeOffsets = new float[6];

    public Vector2 ConstrainToScreen(Vector2 position)
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (aimCamera == null) return position;
        // Cropping the projection keeps padding equal in pixels for either projection.
        float padding = Mathf.Clamp(screenEdgePaddingPixels, 0f, Mathf.Min(aimCamera.pixelWidth, aimCamera.pixelHeight) * 0.49f);
        Matrix4x4 projection = aimCamera.projectionMatrix;
        projection.SetRow(0, projection.GetRow(0) / (1f - 2f * padding / Mathf.Max(1, aimCamera.pixelWidth)));
        projection.SetRow(1, projection.GetRow(1) / (1f - 2f * padding / Mathf.Max(1, aimCamera.pixelHeight)));
        GeometryUtility.CalculateFrustumPlanes(projection * aimCamera.worldToCameraMatrix, screenPlanes);
        GetBoundaryCorners(boundaryCorners);
        for (int i = 0; i < screenPlanes.Length; i++)
        {
            planeOffsets[i] = float.PositiveInfinity;
            foreach (var corner in boundaryCorners)
                planeOffsets[i] = Mathf.Min(planeOffsets[i], screenPlanes[i].GetDistanceToPoint(corner));
        }
        Vector2 desired = position - (Vector2)transform.position;
        if (FitsScreen(desired)) return position;
        Vector2 best = Vector2.zero;
        float bestDistance = float.PositiveInfinity;
        // Intersect the view volume with the XY movement plane. The nearest point
        // on this convex polygon preserves sliding along its sloping perspective edges.
        for (int i = 0; i < screenPlanes.Length; i++)
        {
            Vector2 n = screenPlanes[i].normal;
            if (n.sqrMagnitude < 0.000001f) continue;
            Consider(desired - n * ((Vector2.Dot(n, desired) + planeOffsets[i]) / n.sqrMagnitude));
            for (int j = i + 1; j < screenPlanes.Length; j++)
            {
                Vector2 m = screenPlanes[j].normal;
                float det = n.x * m.y - n.y * m.x;
                if (Mathf.Abs(det) < 0.000001f) continue;
                Consider(new Vector2((-planeOffsets[i] * m.y + n.y * planeOffsets[j]) / det,
                    (-n.x * planeOffsets[j] + planeOffsets[i] * m.x) / det));
            }
        }
        return (Vector2)transform.position + best;

        void Consider(Vector2 candidate)
        {
            float distance = (candidate - desired).sqrMagnitude;
            if (distance < bestDistance && FitsScreen(candidate)) { best = candidate; bestDistance = distance; }
        }
    }

    private bool FitsScreen(Vector2 translation)
    {
        for (int i = 0; i < screenPlanes.Length; i++)
            if (Vector2.Dot((Vector2)screenPlanes[i].normal, translation) + planeOffsets[i] < -0.00001f) return false;
        return true;
    }

    public void GetBoundaryCorners(Vector3[] corners)
    {
        if (boundarySprite == null || boundarySprite.sprite == null)
        {
            for (int i = 0; i < 8; i++) corners[i] = transform.position;
            return;
        }
        Bounds local = boundarySprite.localBounds;
        // Sprite bounds include artificial thickness; the billboard itself is a flat surface.
        for (int i = 0; i < 8; i++)
            corners[i] = boundarySprite.transform.TransformPoint(new Vector3(
                (i & 1) == 0 ? local.min.x : local.max.x,
                (i & 2) == 0 ? local.min.y : local.max.y,
                local.center.z));
    }

    /// <summary>Project the actual body rectangle, not a world-axis bounding box or the feet pivot.</summary>
    public Rect GetCameraPlaneBounds(Vector3 cameraPosition, Quaternion cameraRotation)
    {
        Quaternion inverse = Quaternion.Inverse(cameraRotation);
        Vector2 root = inverse * (transform.position - cameraPosition);
        if (boundarySprite == null || boundarySprite.sprite == null)
            return new Rect(root, Vector2.zero);
        Bounds local = boundarySprite.localBounds;
        Vector2 min = new Vector2(float.PositiveInfinity, float.PositiveInfinity);
        Vector2 max = new Vector2(float.NegativeInfinity, float.NegativeInfinity);
        for (int i = 0; i < 8; i++)
        {
            Vector3 corner = new Vector3((i & 1) == 0 ? local.min.x : local.max.x,
                (i & 2) == 0 ? local.min.y : local.max.y, local.center.z);
            Vector2 point = inverse * (boundarySprite.transform.TransformPoint(corner) - cameraPosition);
            min = Vector2.Min(min, point);
            max = Vector2.Max(max, point);
        }
        return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
    }

    public static float ContainmentShift(float min, float max, float halfExtent)
    {
        float lower = max - halfExtent;
        float upper = min + halfExtent;
        return lower <= upper ? Mathf.Clamp(0f, lower, upper) : (min + max) * 0.5f;
    }

    private void ReadAim()
    {
        Vector2 aimInput = aimAction.ReadValue<Vector2>();

        if (playerInput.currentControlScheme == "KeyboardMouse")
        {
            // Mouse gives us a SCREEN POSITION
            Vector2 mouseWorldPosition = ScreenToGround(aimInput);

            Vector2 direction =
                (Vector2)mouseWorldPosition - rb.position;

            if (direction.sqrMagnitude > 0.001f)
            {
                aimDirection = direction.normalized;
            }
        }
        else
        {
            // Right stick already gives us a DIRECTION
            if (aimInput.sqrMagnitude >
                stickDeadzone * stickDeadzone)
            {
                aimDirection = aimInput.normalized;
            }
        }

        RotateAimPivot();
    }

    private void RotateAimPivot()
    {
        if (aimPivot == null)
            return;

        float angle =
            Mathf.Atan2(aimDirection.y, aimDirection.x)
            * Mathf.Rad2Deg;

        aimPivot.rotation =
            Quaternion.Euler(0f, 0f, angle);
    }

    private void ReadActions()
    {
        spraying = useWeaponAction.IsPressed();
        if (!spraying && hose != null) hose.StopSpraying();

        if (interactAction.WasPressedThisFrame())
        {
            Interact();
        }
    }

    private void OnDied()
    {
        diedAt = Time.unscaledTime;
        moveInput = Vector2.zero;
        spraying = false;
        if (hose != null) hose.StopSpraying();
    }

    private void OnDisable()
    {
        spraying = false;
        if (rb != null) rb.linearVelocity = Vector2.zero;
        if (hose != null) hose.StopSpraying();
    }

    private void OnDestroy()
    {
        if (health != null) health.Died -= OnDied;
    }
    
    // Interact = swing the off-hand axe. Whatever it hits (locker, water source) handles the rest.
    private void Interact() => axe.TrySwing();
    
    
    // animation stuff

    private void UpdateAnimation()
    {
        if (_animator == null) return;
        _animator.SetBool(IsMovingParam, moveInput.sqrMagnitude > 0.01f);
    }
    
    private Vector2 ScreenToGround(Vector2 screenPos)
    {
        Ray ray = aimCamera.ScreenPointToRay(screenPos);
        Plane ground = new Plane(Vector3.forward, new Vector3(0f, 0f, transform.position.z));
        return ground.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : rb.position;
    }
}