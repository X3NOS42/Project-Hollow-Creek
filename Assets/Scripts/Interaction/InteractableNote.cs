using UnityEngine;
using HollowCreek.UI;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// A note or document the player can examine.
    /// Shows the note through the asset's Insight UI Manager overlay:
    /// either the name tooltip + details popup (displayAsFullSheet = false)
    /// or a full scrollable sheet (displayAsFullSheet = true).
    /// The game keeps running while the note is shown.
    /// </summary>
    public class InteractableNote : InteractableObject
    {
        [Header("Note Settings")]
        [Tooltip("Generic name shown before examining, e.g. \"Piece of paper\".")]
        [SerializeField] private string genericName = "Piece of paper";
        [SerializeField] private string noteTitle = "Note";
        [SerializeField] [TextArea(5, 20)] private string noteText = "This is a note.";
        [Tooltip("True = show as a full-screen scrollable sheet; False = show as the tooltip/details popup.")]
        [SerializeField] private bool displayAsFullSheet = false;
        [Tooltip("How long this note stays on screen (seconds). Set to 0 or less to use the manager's default.")]
        [SerializeField] private float displaySeconds = 20f;

        private static bool isDisplaying;
        private bool openedThisFrame;
        private bool hasBeenExamined;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatic()
        {
            isDisplaying = false;
        }

        public override void Interact()
        {
            base.Interact();

            if (isDisplaying)
            {
                return;
            }

            TextInspectUIManager ui = TextInspectUIManager.instance;
            if (ui == null)
            {
                Debug.LogWarning("[InteractableNote] Insight UI Manager not in scene. Run 'Setup Interactions' or drag its prefab in.");
                return;
            }

            ui.ShowName(GetExaminedName(), true);
            if (displayAsFullSheet)
            {
                ui.ShowNoteSheet(noteTitle, noteText, displaySeconds);
            }
            else
            {
                ui.ShowObjectDetails(noteText, displaySeconds);
            }

            hasBeenExamined = true;
            isDisplaying = true;
            openedThisFrame = true;
        }

        private void Update()
        {
            if (!isDisplaying)
            {
                return;
            }

            if (openedThisFrame)
            {
                openedThisFrame = false;
                return;
            }

            if (TextInspectUIManager.instance != null && !TextInspectUIManager.instance.IsObjectDetailsVisible)
            {
                isDisplaying = false;
            }
        }

        public override string GetPrompt()
        {
            // Before examining, the player sees the generic name.
            // After examining, the prompt reveals the note's title.
            string name = hasBeenExamined ? noteTitle : GetGenericName();
            return $"Press E to read {name}";
        }

        public override string GetGenericName()
        {
            return string.IsNullOrEmpty(genericName) ? base.GetGenericName() : genericName;
        }

        public override string GetExaminedName()
        {
            return string.IsNullOrEmpty(noteTitle) ? GetGenericName() : noteTitle;
        }
    }
}