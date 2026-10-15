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

    public Vector2 ConstrainToScreen(Vector2 position)
    {
        if (aimCamera == null) aimCamera = Camera.main;
        if (aimCamera == null || !aimCamera.orthographic) return position;
        Vector3 world = new Vector3(position.x, position.y, transform.position.z);
        Vector3 viewport = aimCamera.WorldToViewportPoint(world);
        Rect bounds = GetCameraPlaneBounds(aimCamera.transform.position, aimCamera.transform.rotation);
        Vector3 delta = Quaternion.Inverse(aimCamera.transform.rotation) * (world - transform.position);
        bounds.position += new Vector2(delta.x, delta.y);
        float halfHeight = aimCamera.orthographicSize;
        float halfWidth = halfHeight * aimCamera.aspect;
        float padding = Mathf.Min(screenEdgePaddingPixels, Mathf.Min(aimCamera.pixelWidth, aimCamera.pixelHeight) * 0.49f);
        float horizontalPadding = padding * (2f * halfWidth / Mathf.Max(1, aimCamera.pixelWidth));
        float verticalPadding = padding * (2f * halfHeight / Mathf.Max(1, aimCamera.pixelHeight));
        float x = viewport.x - ContainmentShift(bounds.xMin, bounds.xMax, halfWidth - horizontalPadding) / (2f * halfWidth);
        float y = viewport.y - ContainmentShift(bounds.yMin, bounds.yMax, halfHeight - verticalPadding) / (2f * halfHeight);
        if (x == viewport.x && y == viewport.y) return position;
        // Ray/ground intersection preserves the XY physics plane with a tilted camera.
        Ray ray = aimCamera.ViewportPointToRay(new Vector3(x, y, 0));
        Plane ground = new Plane(Vector3.forward, world);
        return ground.Raycast(ray, out float distance) ? (Vector2)ray.GetPoint(distance) : position;
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
                (i & 2) == 0 ? local.min.y : local.max.y, (i & 4) == 0 ? local.min.z : local.max.z);
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