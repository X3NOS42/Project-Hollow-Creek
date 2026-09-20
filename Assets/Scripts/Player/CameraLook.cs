using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowCreek.Player
{
    /// <summary>
    /// Handles first-person mouse look with pitch clamping.
    /// Attach to the camera GameObject (child of the player).
    /// </summary>
    public class CameraLook : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;

        [Header("Sensitivity")]
        [SerializeField] private float mouseSensitivity = 0.2f;

        [Header("Pitch Limits")]
        [SerializeField] private float minPitch = -80f;
        [SerializeField] private float maxPitch = 80f;

        private InputActionMap playerMap;
        private InputAction lookAction;
        private float xRotation;

        private void Awake()
        {
            playerMap = inputActions.FindActionMap("Player", throwIfNotFound: true);
            lookAction = playerMap.FindAction("Look", throwIfNotFound: true);
        }

        private void OnEnable()
        {
            playerMap.Enable();
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
        }

        private void OnDisable()
        {
            playerMap.Disable();
        }

        private void Update()
        {
            HandleLook();
        }

        private void HandleLook()
        {
            Vector2 lookInput = lookAction.ReadValue<Vector2>();

            float mouseX = lookInput.x * mouseSensitivity;
            float mouseY = lookInput.y * mouseSensitivity;

            xRotation -= mouseY;
            xRotation = Mathf.Clamp(xRotation, minPitch, maxPitch);

            // Yaw: rotate the player (parent) on Y
            transform.parent.Rotate(Vector3.up * mouseX);

            // Pitch: rotate the camera on X only
            transform.localRotation = Quaternion.Euler(xRotation, 0f, 0f);
        }
    }
}
