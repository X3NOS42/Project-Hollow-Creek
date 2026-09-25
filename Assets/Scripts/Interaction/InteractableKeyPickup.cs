using UnityEngine;
using System;
using HollowCreek.UI;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// A key the player can pick up.
    /// Adds the key to GameManager state and destroys the object.
    /// </summary>
    public class InteractableKeyPickup : InteractableObject
    {
        [Header("Key Settings")]
        [Tooltip("Generic name shown before examining, e.g. \"Key\".")]
        [SerializeField] private string genericName = "Key";
        [Tooltip("Name revealed after examining, e.g. \"Bedroom Key\".")]
        [SerializeField] private string keyName = "Key";
        [Tooltip("If true, the player must examine this key (E) before they can pick it up (F).")]
        [SerializeField] private bool mustExamineBeforePickup = false;
        [SerializeField] private string keyId = "key_01";
        [TextArea(2, 5)]
        [SerializeField] private string description = "A small brass key.";
        [Tooltip("How long the key description stays on screen after picking it up.")]
        [SerializeField] private float pickupMessageSeconds = 1.5f;

        /// <summary>
        /// Fired when the key is picked up. UI can listen for pickup notifications.
        /// </summary>
        public event Action<string> OnKeyPickedUp;

        private bool hasBeenExamined;

        public override string GetGenericName()
        {
            return string.IsNullOrEmpty(genericName) ? base.GetGenericName() : genericName;
        }

        public override void Interact()
        {
            base.Interact();

            // Inspect: show a small note describing the key
            TextInspectUIManager ui = TextInspectUIManager.instance;
            if (ui == null)
            {
                Debug.LogWarning("[InteractableKeyPickup] Insight UI Manager not in scene. Run 'Setup Interactions' or drag its prefab in.");
                return;
            }

            ui.ShowName(GetExaminedName(), true);
            ui.ShowObjectDetails(description, -1f);

            hasBeenExamined = true;
        }

        public override bool CanPickUp()
        {
            if (mustExamineBeforePickup && !hasBeenExamined)
            {
                return false;
            }
            return true;
        }

        public override string GetPrompt()
        {
            // Before examining, the player sees the generic name.
            // After examining, the prompt reveals the proper name.
            string name = hasBeenExamined ? keyName : GetGenericName();
            return $"Press E to inspect {name}";
        }

        public override string GetPickupPrompt()
        {
            string name = hasBeenExamined ? keyName : GetGenericName();
            return $"Press F to pick up {name}";
        }

        public override string GetExaminedName()
        {
            return string.IsNullOrEmpty(keyName) ? GetGenericName() : keyName;
        }

        public override void PickUp()
        {
            base.PickUp();

            // Keep the description briefly on screen after pickup
            TextInspectUIManager ui = TextInspectUIManager.instance;
            if (ui != null)
            {
                ui.ShowName(GetExaminedName(), true);
                ui.ShowObjectDetails(description, pickupMessageSeconds);
            }

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
