using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// Handles detection and interaction with IInteractable objects.
    /// Attach to the Camera. Raycasts from camera center to detect interactables.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Raycast Settings")]
        [SerializeField] private float maxRaycastDistance = 5f;
        [SerializeField] private LayerMask interactableLayer = ~0;

        /// <summary>
        /// Fired when the player targets a new interactable (or null when looking away).
        /// </summary>
        public event Action<IInteractable> OnTargetChanged;

        /// <summary>
        /// Fired when the player successfully interacts with an object.
        /// </summary>
        public event Action<IInteractable> OnInteracted;

        private InputActionMap playerMap;
        private InputAction interactAction;
        private IInteractable currentTarget;

        private void Awake()
        {
            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            interactAction.performed += OnInteractPerformed;
        }

        private void OnDisable()
        {
            interactAction.performed -= OnInteractPerformed;
        }

        private void Update()
        {
            CheckForInteractable();
        }

        private void CheckForInteractable()
        {
            Ray ray = new Ray(transform.position, transform.forward);
            IInteractable detected = null;

            if (Physics.Raycast(ray, out RaycastHit hit, maxRaycastDistance, interactableLayer))
            {
                detected = hit.collider.GetComponent<IInteractable>();

                // Also check parent objects if the collider is on a child
                if (detected == null)
                {
                    detected = hit.collider.GetComponentInParent<IInteractable>();
                }
            }

            // Notify if target changed
            if (detected != currentTarget)
            {
                // Unhighlight old target
                if (currentTarget is InteractableObject oldInteractable)
                {
                    oldInteractable.SetHighlighted(false);
                }

                currentTarget = detected;

                // Highlight new target
                if (currentTarget is InteractableObject newInteractable)
                {
                    newInteractable.SetHighlighted(true);
                }

                OnTargetChanged?.Invoke(currentTarget);
            }
        }

        private void OnInteractPerformed(InputAction.CallbackContext context)
        {
            if (currentTarget != null)
            {
                currentTarget.Interact();
                OnInteracted?.Invoke(currentTarget);
            }
        }

        /// <summary>
        /// Returns the currently targeted interactable, or null.
        /// </summary>
        public IInteractable GetCurrentTarget()
        {
            return currentTarget;
        }
    }
}
