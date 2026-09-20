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
        [SerializeField] private float gravity = -20f;
        [SerializeField] private float groundCheckDistance = 0.3f;
        [SerializeField] private LayerMask groundMask;

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

        private void Awake()
        {
            controller = GetComponent<CharacterController>();
            targetHeight = standingHeight;

            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            moveAction = playerMap.FindAction("Move", throwIfNotFound: true);
            sprintAction = playerMap.FindAction("Sprint", throwIfNotFound: true);
            crouchAction = playerMap.FindAction("Crouch", throwIfNotFound: true);
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
            controller.Move(velocity * Time.deltaTime);
        }

        private bool IsGrounded()
        {
            return Physics.Raycast(transform.position, Vector3.down, groundCheckDistance + 0.1f, groundMask);
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
