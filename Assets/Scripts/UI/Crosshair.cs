using UnityEngine;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// Simple dot crosshair at screen center.
    /// Attach to a UI Image element to use as a crosshair.
    /// </summary>
    public class Crosshair : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private InteractionSystem interactionSystem;

        [Header("Colors")]
        [SerializeField] private Color defaultColor = new Color(1f, 1f, 1f, 0.6f);
        [SerializeField] private Color targetColor = new Color(1f, 0.9f, 0.3f, 1f);

        private UnityEngine.UI.Image crosshairImage;

        private void Awake()
        {
            crosshairImage = GetComponent<UnityEngine.UI.Image>();
            if (crosshairImage != null)
            {
                crosshairImage.color = defaultColor;
            }
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

        private void OnTargetChanged(IInteractable target)
        {
            if (crosshairImage != null)
            {
                crosshairImage.color = target != null ? targetColor : defaultColor;
            }
        }
    }
}
