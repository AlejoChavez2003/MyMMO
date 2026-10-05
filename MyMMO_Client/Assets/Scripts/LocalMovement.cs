using UnityEngine;

public class LocalMovement : MonoBehaviour
{
    [Header("Velocidades")]
    public float walkSpeed = 3.5f;
    public float runSpeed = 7.0f;
    public float jumpForce = 6.0f;
    public float doubleJumpForce = 5.5f;
    public float rotationSpeed = 12.0f;

    [Header("Físicas de Suelo")]
    public Transform groundCheck;
    public float groundDistance = 0.3f;
    public LayerMask groundLayer;

    [Header("Referencias")]
    public Animator animator;

    private bool isRunning = false;
    private Rigidbody rb;
    private int maxJumps = 2;
    private int jumpsRemaining;
    private bool isGrounded;

    void Start()
    {
        rb = GetComponent<Rigidbody>();
        if (animator == null) animator = GetComponentInChildren<Animator>();
        jumpsRemaining = maxJumps;
    }

    void Update()
    {
        rb.angularVelocity = Vector3.zero;

        // 1. Detección de suelo
        if (groundCheck != null)
        {
            isGrounded = Physics.CheckSphere(groundCheck.position, groundDistance, groundLayer);
        }

        if (isGrounded)
        {
            jumpsRemaining = maxJumps;
            if (animator != null)
            {
                animator.SetBool("isFalling", false);
            }
        }
        else
        {
            if (rb.linearVelocity.y < -0.1f && animator != null)
            {
                animator.SetBool("isFalling", true);
            }
        }

        // 2. Alternar Sprint con Shift
        if (Input.GetKeyDown(KeyCode.LeftShift) || Input.GetKeyDown(KeyCode.RightShift))
        {
            isRunning = !isRunning;
        }

        // 3. Movimiento relativo a la cámara
        float moveX = Input.GetAxis("Horizontal");
        float moveZ = Input.GetAxis("Vertical");

        Vector3 moveDirection = Vector3.zero;
        Camera cam = Camera.main;

        if (cam != null && cam.enabled)
        {
            Vector3 camForward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            Vector3 camRight = Vector3.ProjectOnPlane(cam.transform.right, Vector3.up).normalized;

            if (camForward.sqrMagnitude < 0.001f)
            {
                camForward = Vector3.ProjectOnPlane(transform.forward, Vector3.up).normalized;
            }

            moveDirection = (camForward * moveZ + camRight * moveX).normalized;
        }
        else
        {
            moveDirection = new Vector3(moveX, 0f, moveZ).normalized;
        }

        bool isMoving = moveDirection.sqrMagnitude > 0.01f;

        // 4. Aplicar físicas de movimiento y rotación
        float currentSpeed = isRunning ? runSpeed : walkSpeed;
        Vector3 targetVelocity = moveDirection * currentSpeed;
        
        // Conservamos la velocidad vertical (Y) actual para caídas y saltos
        rb.linearVelocity = new Vector3(targetVelocity.x, rb.linearVelocity.y, targetVelocity.z);

        if (isMoving)
        {
            Quaternion targetRotation = Quaternion.LookRotation(moveDirection);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * rotationSpeed);
        }

        // 5. Salto y Doble Salto con Espacio
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (isGrounded)
            {
                ExecuteJump(jumpForce, "Jump");
            }
            else if (jumpsRemaining > 0)
            {
                rb.linearVelocity = new Vector3(rb.linearVelocity.x, 0f, rb.linearVelocity.z);
                ExecuteJump(doubleJumpForce, "Fall");
            }
        }

        // 6. Parámetros de animación
        if (animator != null)
        {
            animator.SetBool("isMoving", isMoving);
            animator.SetBool("isRunning", isRunning && isMoving);
        }
    }

    private void ExecuteJump(float force, string triggerName)
    {
        rb.AddForce(Vector3.up * force, ForceMode.Impulse);
        jumpsRemaining--;
        if (animator != null)
        {
            animator.SetTrigger(triggerName);
        }
    }

    private void OnDrawGizmosSelected()
    {
        if (groundCheck != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(groundCheck.position, groundDistance);
        }
    }
}