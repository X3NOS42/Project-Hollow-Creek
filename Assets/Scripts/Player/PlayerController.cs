using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowCreek.Player
{
    /// <summary>
    /// Handles player movement: walking, sprinting, and crouching.
    /// Requires a CharacterController on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerController : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Movement Speeds")]
        [SerializeField] private float walkSpeed = 4f;
        [SerializeField] private float sprintSpeed = 7f;
        [SerializeField] private float crouchSpeed = 2f;

        [Header("Crouch Settings")]
        [SerializeField] private float standingHeight = 2f;
        [SerializeField] private float crouchHeight = 1.2f;
        [SerializeField] private float crouchTransitionSpeed = 8f;

        [Header("Gravity")]
        // Near real-world gravity (9.81). Lower = floatier, higher = snappier.
        // NOTE: the scene serializes this field, so tune it on the Player's
        // Inspector - changing the default here only affects newly added Players.
        [SerializeField] private float gravity = -10f;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;
        [SerializeField] private float terminalVelocity = -55f;

        private CharacterController controller;
        private InputActionMap playerMap;
        private InputAction moveAction;
        private InputAction sprintAction;
        private InputAction crouchAction;
        private Vector3 velocity;
        private bool isSprinting;
        private bool isCrouching;
        private float currentSpeed;
        private float targetHeight;

        /// <summary>
        /// The shared Player input map. UI code (the F1 lighting debug menu)
        /// disables it while open so movement, look, and interact pause while
        /// the mouse is being used to click buttons.
        /// </summary>
        public static InputActionMap PlayerMap { get; private set; }

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            targetHeight = standingHeight;

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            sprintAction = playerMap.FindAction("Sprint", throwIfNotFound: true);
            crouchAction = playerMap.FindAction("Crouch", throwIfNotFound: true);
            PlayerMap = playerMap;
        }

        private void OnEnable()
        {
            sprintAction.performed += OnSprintPerformed;
            sprintAction.canceled += OnSprintCanceled;
            crouchAction.performed += OnCrouchPerformed;
            playerMap.Enable();
        }

        private void OnDisable()
        {
            sprintAction.performed -= OnSprintPerformed;
            sprintAction.canceled -= OnSprintCanceled;
            crouchAction.performed -= OnCrouchPerformed;
            playerMap.Disable();
        }

        private void Update()
        {
            HandleMovement();
            HandleCrouch();
            ApplyGravity();
        }

        private void HandleMovement()
        {
            Vector2 moveInput = moveAction.ReadValue<Vector2>();

            if (moveInput.magnitude > 0.1f)
            {
                currentSpeed = GetCurrentSpeed();
            }
            else
            {
                currentSpeed = 0f;
            }

            Vector3 move = transform.right * moveInput.x + transform.forward * moveInput.y;
            controller.Move(move * currentSpeed * Time.deltaTime);
        }

        private float GetCurrentSpeed()
        {
            if (isCrouching) return crouchSpeed;
            if (isSprinting) return sprintSpeed;
            return walkSpeed;
        }

        private void HandleCrouch()
        {
            float currentHeight = controller.height;
            if (Mathf.Abs(currentHeight - targetHeight) > 0.01f)
            {
                float newHeight = Mathf.Lerp(currentHeight, targetHeight, crouchTransitionSpeed * Time.deltaTime);
                Vector3 center = controller.center;
                float heightDifference = newHeight - currentHeight;
                center.y += heightDifference * 0.5f;
                controller.height = newHeight;
                controller.center = center;
            }
        }

        private void ApplyGravity()
        {
            if (IsGrounded() && velocity.y < 0)
            {
                velocity.y = -2f;
            }

            velocity.y += gravity * Time.deltaTime;
            // Cap the fall speed so velocity can never run away (long falls,
            // stale frames) and teleport the controller on the next Move.
            velocity.y = Mathf.Max(velocity.y, terminalVelocity);
            controller.Move(velocity * Time.deltaTime);
        }

        /// <summary>
        /// Grounded check fired from the capsule's bottom sphere so it lines up
        /// with the feet - transform.position sits at the capsule center, a full
        /// height/2 above the ground. A groundMask of Nothing (the value the
        /// scene shipped with) would silently make the ray never hit and let
        /// gravity accumulate into a snap-teleport on the first step off a ledge,
        /// so a mask of 0 is treated as Everything.
        /// </summary>
        private bool IsGrounded()
        {
            Vector3 origin = transform.position + controller.center;
            origin.y -= controller.height * 0.5f - controller.radius;

            int maskBits = groundMask.value == 0 ? -1 : groundMask.value;
            return Physics.Raycast(origin, Vector3.down,
                controller.radius + groundCheckDistance, maskBits);
        }

        private void OnSprintPerformed(InputAction.CallbackContext context)
        {
            if (!isCrouching)
            {
                isSprinting = true;
            }
        }

        private void OnSprintCanceled(InputAction.CallbackContext context)
        {
            isSprinting = false;
        }

        private void OnCrouchPerformed(InputAction.CallbackContext context)
        {
            isCrouching = !isCrouching;
            targetHeight = isCrouching ? crouchHeight : standingHeight;

            if (isCrouching)
            {
                isSprinting = false;
            }
        }
    }
}
