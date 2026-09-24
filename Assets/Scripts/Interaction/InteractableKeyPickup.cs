using UnityEngine;
using System;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// A key the player can pick up.
    /// Adds the key to GameManager state and destroys the object.
    /// </summary>
    public class InteractableKeyPickup : InteractableObject
    {
        [Header("Key Settings")]
        [SerializeField] private string keyId = "key_01";
        [SerializeField] private string keyName = "Key";

        /// <summary>
        /// Fired when the key is picked up. UI can listen for pickup notifications.
        /// </summary>
        public event Action<string> OnKeyPickedUp;

        public override void Interact()
        {
            base.Interact();

            // Add key to game state
            if (HollowCreek.Story.GameManager.Instance != null)
            {
                HollowCreek.Story.GameManager.Instance.AddKey(keyId);
            }

            // Notify listeners
            OnKeyPickedUp?.Invoke(keyName);

            // Remove the object
            Destroy(gameObject);
        }

        public override string GetPrompt()
        {
            return $"Press E to pick up {keyName}";
        }

        public string GetKeyId()
        {
            return keyId;
        }

        public string GetKeyName()
        {
            return keyName;
        }
    }
}
