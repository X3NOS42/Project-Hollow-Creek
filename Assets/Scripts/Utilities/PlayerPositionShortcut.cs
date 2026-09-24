using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowCreek.Utilities
{
    /// <summary>
    /// Dev helper placed on the Player: while playing, press F8 to stash the current
    /// position for the Player Position Tool to pick up (rename it there later).
    /// Reads input in the game loop, so it is reliable. No editor APIs used here.
    /// </summary>
    [DisallowMultipleComponent]
    public class PlayerPositionShortcut : MonoBehaviour
    {
        public const Key SaveKey = Key.F8;

        private void Update()
        {
            if (Keyboard.current == null)
            {
                return;
            }

            if (Keyboard.current[SaveKey].wasPressedThisFrame)
            {
                SaveCurrentPosition();
            }
        }

        private void SaveCurrentPosition()
        {
            Transform player = GameObject.Find("Player")?.transform;
            if (player == null)
            {
                player = transform;
            }

            var loc = new PlayerPositionStore.LocationData
            {
                name = "F8 " + System.DateTime.Now.ToString("HH:mm:ss"),
                position = player.position,
                playerRotation = player.eulerAngles,
                cameraLocalRotation = GetCameraLocalRotation(player)
            };

            PlayerPositionStore.PendingSave = loc;
            PlayerPositionStore.HasPending = true;

            Debug.Log($"[PlayerPositionTool] F8 captured position at {player.position}. The window will add it to the list.");
        }

        private static Vector3 GetCameraLocalRotation(Transform player)
        {
            Transform cam = player.Find("Camera");
            return cam != null ? cam.localEulerAngles : Vector3.zero;
        }
    }
}