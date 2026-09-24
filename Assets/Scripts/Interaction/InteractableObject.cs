using UnityEngine;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// Base class for interactable objects. Provides default behavior for highlighting,
    /// interaction range, and prompt text. Subclass this for specific interactions.
    /// </summary>
    public class InteractableObject : MonoBehaviour, IInteractable
    {
        [Header("Interaction Settings")]
        [SerializeField] private string promptText = "Interact";
        [SerializeField] private float interactionRange = 3f;

        [Header("Highlight")]
        [SerializeField] private bool enableHighlight = true;
        [SerializeField] private Color highlightColor = new Color(1f, 0.9f, 0.5f);
        [SerializeField] private float highlightIntensity = 0.3f;

        private Color originalColor;
        private Renderer objectRenderer;
        private bool isHighlighted;

        protected virtual void Awake()
        {
            objectRenderer = GetComponent<Renderer>();
            if (objectRenderer != null && objectRenderer.material != null)
            {
                originalColor = objectRenderer.material.color;
            }
        }

        /// <summary>
        /// Called when the player interacts with this object.
        /// Override in subclasses for specific behavior.
        /// </summary>
        public virtual void Interact()
        {
            Debug.Log($"[Interactable] Interacted with: {gameObject.name}");
        }

        /// <summary>
        /// Returns the prompt text displayed to the player.
        /// </summary>
        public virtual string GetPrompt()
        {
            return promptText;
        }

        /// <summary>
        /// Returns the maximum interaction distance.
        /// </summary>
        public virtual float GetInteractionRange()
        {
            return interactionRange;
        }

        /// <summary>
        /// Highlights the object when the player looks at it.
        /// </summary>
        public void SetHighlighted(bool highlighted)
        {
            if (!enableHighlight || objectRenderer == null) return;

            isHighlighted = highlighted;

            if (highlighted)
            {
                objectRenderer.material.color = originalColor + highlightColor * highlightIntensity;
            }
            else
            {
                objectRenderer.material.color = originalColor;
            }
        }

        /// <summary>
        /// Returns whether this object is currently highlighted.
        /// </summary>
        public bool IsHighlighted()
        {
            return isHighlighted;
        }

        protected virtual void OnValidate()
        {
            if (interactionRange < 0.5f)
            {
                interactionRange = 0.5f;
            }
        }
    }
}
