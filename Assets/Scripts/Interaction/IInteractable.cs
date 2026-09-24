namespace HollowCreek.Interaction
{
    /// <summary>
    /// Interface for objects that the player can interact with.
    /// Any object implementing this can be detected and used by the InteractionSystem.
    /// </summary>
    public interface IInteractable
    {
        /// <summary>
        /// Called when the player presses the interact button on this object.
        /// </summary>
        void Interact();

        /// <summary>
        /// Returns the prompt text to display when looking at this object.
        /// Example: "Press E to examine"
        /// </summary>
        string GetPrompt();

        /// <summary>
        /// Returns the maximum distance the player can be to interact with this object.
        /// </summary>
        float GetInteractionRange();
    }
}
