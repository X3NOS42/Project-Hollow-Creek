using UnityEngine;

namespace HollowCreek.Interaction
{
    /// <summary>
    /// Base class for interactable objects the player can examine.
    /// Provides generic/examined naming (e.g. "Key" -> "Bedroom Key"):
    /// prompts show the generic name before examining and the revealed name after.
    /// Subclasses supply a revealed name and optionally a flavour prompt verb
    /// (defaults to "examine", e.g. "inspect" for keys, "read" for notes).
    /// </summary>
    public abstract class InteractableExaminable : InteractableObject
    {
        [Header("Examine Settings")]
        [Tooltip("Generic name shown before examining, e.g. \"Key\".")]
        [SerializeField] private string genericName = "Object";

        protected bool hasBeenExamined;

        public override string GetGenericName()
        {
            return string.IsNullOrEmpty(genericName) ? base.GetGenericName() : genericName;
        }

        protected virtual string RevealedName => GetGenericName();

        public override string GetExaminedName()
        {
            return RevealedName;
        }

        protected string GetDisplayName()
        {
            return hasBeenExamined ? RevealedName : GetGenericName();
        }

        protected virtual string ExamineVerb => "examine";

        public override string GetPrompt()
        {
            return $"Press E to {ExamineVerb} {GetDisplayName()}";
        }
    }
}