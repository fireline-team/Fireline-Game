using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
[RequireComponent(typeof(PlayerInput))]
public class PlayerController : MonoBehaviour
{
    [Header("Movement")]
    [SerializeField] private float moveSpeed = 5f;

    [Header("Aiming")]
    [SerializeField] private Transform aimPivot;
    [SerializeField] private Camera aimCamera;
    [SerializeField] private float stickDeadzone = 0.2f;

    private Rigidbody2D rb;
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
        ReadMovement();
        ReadAim();
        ReadActions();
    }

    private void FixedUpdate()
    {
        Move();
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
        if (useWeaponAction.IsPressed())
        {
            UseWeapon();
        }

        if (interactAction.WasPressedThisFrame())
        {
            Interact();
        }
    }

    private void UseWeapon()
    {
        // Temporary test
        Debug.Log(
            $"Player {playerInput.playerIndex + 1} using weapon"
        );
    }

    private void Interact()
    {
        // Temporary test
        Debug.Log(
            $"Player {playerInput.playerIndex + 1} interacting"
        );
    }
}