using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Rigidbody2D))]
public class PlayerMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 3f;

    private Rigidbody2D rb;
    private PlayerInputActions input;
    private Vector2 moveInput;
    private Animator anim;   // <-- NEW

    void Awake()
    {
        rb = GetComponent<Rigidbody2D>();
        anim = GetComponent<Animator>();   // <-- NEW
        input = new PlayerInputActions();
    }

    private void OnEnable()
    {
        input.Player.Enable();
        input.Player.Move.performed += OnMove;
        input.Player.Move.canceled += OnMoveCancelled;
    }

    private void OnDisable()
    {
        input.Player.Disable();
    }

    void OnMove(InputAction.CallbackContext ctx)
    {
        moveInput = ctx.ReadValue<Vector2>();
        UpdateAnimation();

    }

    void OnMoveCancelled(InputAction.CallbackContext ctx)
    {
        moveInput = Vector2.zero;
        UpdateAnimation();
    }

    void FixedUpdate()
    {
        Vector2 movement = moveInput * moveSpeed * Time.fixedDeltaTime;
        rb.MovePosition(rb.position + movement);
    }

    // -------------------------------
    // Update Animations
    // -------------------------------
    void UpdateAnimation()
    {
        // If not moving → idle
        if (moveInput == Vector2.zero)
        {
            anim.Play("Idle_Minotaur");
            return;
        }

        // Determine direction
        if (Mathf.Abs(moveInput.x) > Mathf.Abs(moveInput.y))
        {
            if (moveInput.x > 0)
                anim.Play("Right_Minotaur");
            else
                anim.Play("Left_Minotaur");
        }
        else
        {
            if (moveInput.y > 0)
                anim.Play("Back_Minotaur");
            else
                anim.Play("Front_Minotaur");
        }
    }
}
