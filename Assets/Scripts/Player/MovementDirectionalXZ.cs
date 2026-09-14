using UnityEngine;
using NaughtyAttributes;
using UnityEngine.InputSystem;
using UC;

[RequireComponent(typeof(Rigidbody))]
public class MovementDirectionalXZ : MonoBehaviour
{
    public enum MovementControl { Velocity = 0, Acceleration = 1 };
    public enum TurnBehaviour { None = 0, InputRotatesObject = 1, VelocityRotatesObject = 2 };

    [SerializeField]
    private float speed = 5.0f;
    [SerializeField]
    private MovementControl movementControl = MovementControl.Velocity;
    [SerializeField]
    private float acceleration = 50.0f;
    [SerializeField]
    private float deceleration = 50.0f;
    [SerializeField, HideIf("needNewInputSystem")]
    private PlayerInput playerInput;
    [SerializeField, InputPlayer(nameof(playerInput))]
    private UC.InputControl moveInput;
    [SerializeField]
    private Hypertag cameraTag;
    [SerializeField]
    private TurnBehaviour turnBehaviour = TurnBehaviour.None;
    [SerializeField]
    private float turnSpeed = 720.0f;
    [SerializeField]
    private bool useAnimator = false;
    [SerializeField]
    private Animator animator;
    [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Float)]
    private string speedParameter;
    [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Float)]
    private string normalizedSpeedParameter;
    [SerializeField, AnimatorParam("animator", AnimatorControllerParameterType.Bool)]
    private string isMovingParameter;

    public bool isMoving { get; private set; }
    public Vector3 moveDirection { get; private set; }

    private Transform cameraTransform;
    private Vector3 planarVelocity;

    const float inputEpsilonZero = 0.1f;
    const float velocityEpsilonZero = 0.05f;

    public float GetSpeed() => speed;
    public void SetSpeed(float speed) { this.speed = speed; }

    public void SetTurnSpeed(float v) { turnSpeed = v; }
    public float GetTurnSpeed() => turnSpeed;

    public bool needNewInputSystem => (moveInput.type == UC.InputControl.InputType.NewInput);

    protected Rigidbody rb;

    protected void Start()
    {
        rb = GetComponent<Rigidbody>();

        if (animator == null)
        {
            animator = GetComponentInChildren<Animator>();
        }

        moveInput.playerInput = playerInput;

        FetchCamera();
    }

    void FixedUpdate()
    {
        Vector2 input = moveInput.GetAxis2();
        // Keep diagonals from being faster than straight movement (keyboard input)
        if (input.sqrMagnitude > 1.0f) input.Normalize();
        if (input.magnitude < inputEpsilonZero) input = Vector2.zero;

        (Vector3 forward, Vector3 right) = GetCameraAxis();

        moveDirection = right * input.x + forward * input.y;

        Vector3 targetVelocity = moveDirection * speed;
        Vector3 currentVelocity = rb.linearVelocity;
        planarVelocity = new Vector3(currentVelocity.x, 0.0f, currentVelocity.z);

        if (movementControl == MovementControl.Acceleration)
        {
            float accel = (moveDirection.sqrMagnitude > 0.0f) ? acceleration : deceleration;
            planarVelocity = Vector3.MoveTowards(planarVelocity, targetVelocity, accel * Time.fixedDeltaTime);
        }
        else
        {
            planarVelocity = targetVelocity;
        }

        // Vertical velocity is left alone, so gravity and collisions still work
        rb.linearVelocity = new Vector3(planarVelocity.x, currentVelocity.y, planarVelocity.z);

        isMoving = planarVelocity.magnitude > velocityEpsilonZero;

        Vector3 facing = Vector3.zero;
        switch (turnBehaviour)
        {
            case TurnBehaviour.None:
                break;
            case TurnBehaviour.InputRotatesObject:
                if (moveDirection.sqrMagnitude > 0.0f) facing = moveDirection;
                break;
            case TurnBehaviour.VelocityRotatesObject:
                if (isMoving) facing = planarVelocity;
                break;
            default:
                break;
        }

        if (facing.sqrMagnitude > 0.0f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(facing, Vector3.up);
            if (turnSpeed > 0.0f)
            {
                targetRotation = Quaternion.RotateTowards(rb.rotation, targetRotation, turnSpeed * Time.fixedDeltaTime);
            }
            rb.MoveRotation(targetRotation);
        }
    }

    void Update()
    {
        if ((useAnimator) && (animator))
        {
            float currentSpeed = planarVelocity.magnitude;

            if (speedParameter != "") animator.SetFloat(speedParameter, currentSpeed);
            if (normalizedSpeedParameter != "") animator.SetFloat(normalizedSpeedParameter, (speed > 0.0f) ? (currentSpeed / speed) : 0.0f);
            if (isMovingParameter != "") animator.SetBool(isMovingParameter, isMoving);
        }
    }

    void FetchCamera()
    {
        if (cameraTag != null)
        {
            var cam = Hypertag.FindFirstObjectWithHypertag<Camera>(cameraTag);
            if (cam) cameraTransform = cam.transform;
        }
        if ((cameraTransform == null) && (Camera.main != null))
        {
            cameraTransform = Camera.main.transform;
        }
    }

    (Vector3, Vector3) GetCameraAxis()
    {
        if (cameraTransform == null)
        {
            FetchCamera();
            // No camera, just use world axis
            if (cameraTransform == null) return (Vector3.forward, Vector3.right);
        }

        Vector3 forward = cameraTransform.forward;
        forward.y = 0.0f;
        // Camera is looking straight down, so use the top of the screen as forward
        if (forward.sqrMagnitude < 0.0001f)
        {
            forward = cameraTransform.up;
            forward.y = 0.0f;
        }
        forward.Normalize();

        Vector3 right = Vector3.Cross(Vector3.up, forward);

        return (forward, right);
    }

    public void SetActive(bool active)
    {
        enabled = active;
        if (!active)
        {
            rb.linearVelocity = new Vector3(0.0f, rb.linearVelocity.y, 0.0f);
            planarVelocity = Vector3.zero;
            isMoving = false;
        }
    }
}
