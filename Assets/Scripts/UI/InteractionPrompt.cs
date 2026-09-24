using UnityEngine;
using TMPro;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// Displays interaction prompts on screen.
    /// Listens to InteractionSystem events to show/hide the prompt text.
    /// Requires a TextMeshProUGUI component on the same GameObject.
    /// </summary>
    [RequireComponent(typeof(TextMeshProUGUI))]
    public class InteractionPrompt : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionSystem interactionSystem;

        [Header("Settings")]
        [SerializeField] private float fadeInSpeed = 8f;
        [SerializeField] private float fadeOutSpeed = 6f;

        private TextMeshProUGUI promptText;
        private float targetAlpha;
        private float currentAlpha;

        private void Awake()
        {
            promptText = GetComponent<TextMeshProUGUI>();
            promptText.alpha = 0f;
            currentAlpha = 0f;
            targetAlpha = 0f;
        }

        private void OnEnable()
        {
            if (interactionSystem != null)
            {
                interactionSystem.OnTargetChanged += OnTargetChanged;
            }
        }

        private void OnDisable()
        {
            if (interactionSystem != null)
            {
                interactionSystem.OnTargetChanged -= OnTargetChanged;
            }
        }

        private void Update()
        {
            // Smooth fade in/out
            float speed = targetAlpha > currentAlpha ? fadeInSpeed : fadeOutSpeed;
            currentAlpha = Mathf.Lerp(currentAlpha, targetAlpha, speed * Time.deltaTime);
            promptText.alpha = currentAlpha;

            // Hide text completely when fully faded out
            if (currentAlpha < 0.01f)
            {
                promptText.enabled = false;
            }
        }

        private void OnTargetChanged(IInteractable target)
        {
            if (target != null)
            {
                promptText.text = target.GetPrompt();
                promptText.enabled = true;
                targetAlpha = 1f;
            }
            else
            {
                targetAlpha = 0f;
            }
        }
    }
}
