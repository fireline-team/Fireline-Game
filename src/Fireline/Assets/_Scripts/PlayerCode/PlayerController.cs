using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
[RequireComponent(typeof(PlayerHealth))] // plus at least one hose: StandardHose, MistHose, or JetHose
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Aiming")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float stickDeadzone = 0.2f;

    private Rigidbody2D rb;
    private PlayerHealth health;
    private HoseLoadout loadout;
    private PlayerInteractor interactor;
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

    private void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        health = GetComponent<PlayerHealth>();
        loadout = GetComponent<HoseLoadout>();
        interactor = GetComponent<PlayerInteractor>();
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
            if (Time.unscaledTime - diedAt > 0.3f && interactAction.WasPressedThisFrame())
                health.ResetScene();
            return;
        }

        ReadMovement();
        ReadAim();
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
        rb.linearVelocity = moveInput * moveSpeed;
    }

    private void ReadAim()
    {
        Vector2 aimInput = aimAction.ReadValue<Vector2>();

        if (playerInput.currentControlScheme == "KeyboardMouse")
        {
            // Mouse gives us a SCREEN POSITION
            Vector3 mouseWorldPosition =
                aimCamera.ScreenToWorldPoint(aimInput);

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
    
    private void Interact()
    {
        if (interactor != null && interactor.TryInteract())
            return;

        UseOffhand();
    }

    private void UseOffhand()
    {
        // TODO: off-hand axe/weapon
        Debug.Log($"Player {playerInput.playerIndex + 1} swings axe (not implemented yet)");
    }
}