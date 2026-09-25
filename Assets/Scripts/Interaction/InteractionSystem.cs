using System;
using UnityEngine;
using UnityEngine.InputSystem;
using HollowCreek.UI;

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

        /// <summary>
        /// Fired when the player successfully picks up an object with F.
        /// </summary>
        public event Action<IInteractable> OnPickedUp;

        private InputActionMap playerMap;
        private InputAction interactAction;
        private InputAction pickupAction;
        private IInteractable currentTarget;

        private void Awake()
        {
            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            interactAction = playerMap.FindAction("Interact", throwIfNotFound: true);
            pickupAction = playerMap.FindAction("PickUp");

            if (pickupAction == null)
            {
                Debug.LogWarning("[Hollow Creek] 'PickUp' action not found in InputSystem_Actions. " +
                    "Reimport the input asset (Assets > Reimport or focus the editor) and run 'Setup Interactions'.");
            }
        }

        private void OnEnable()
        {
            interactAction.performed += OnInteractPerformed;
            if (pickupAction != null)
            {
                pickupAction.performed += OnPickUpPerformed;
            }
        }

        private void OnDisable()
        {
            interactAction.performed -= OnInteractPerformed;
            if (pickupAction != null)
            {
                pickupAction.performed -= OnPickUpPerformed;
            }
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

        private void OnPickUpPerformed(InputAction.CallbackContext context)
        {
            if (currentTarget == null)
            {
                return;
            }

            if (!currentTarget.CanPickUp())
            {
                return;
            }

            // Don't pick up while a full note sheet is on screen (the player is reading).
            // The small inspect tooltip does NOT block pickup.
            TextInspectUIManager ui = TextInspectUIManager.instance;
            if (ui != null && ui.IsNoteSheetVisible)
            {
                return;
            }

            currentTarget.PickUp();
            OnPickedUp?.Invoke(currentTarget);
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
