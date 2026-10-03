using System;
using HollowCreek.Interaction;
using UnityEngine;

namespace HollowCreek.Power
{
    /// <summary>
    /// A mains power source (power box / generator). Devices link to this via
    /// their PowerDevice component. Interactable: E toggles the power on/off.
    /// </summary>
    public class PowerSource : InteractableObject
    {
        [Header("Power")]
        [SerializeField] private string sourceName = "Power Box";
        [SerializeField] private bool initiallyPowered = true;

        /// <summary>Whether this source is currently delivering power.</summary>
        public bool IsPowered { get; private set; } = true;

        /// <summary>Raised when the power state changes with the new state.</summary>
        public event Action<bool> PowerStateChanged;

        /// <summary>Display name used by prompts and gizmos.</summary>
        public string SourceName => sourceName;

        protected override void Awake()
        {
            base.Awake();
            IsPowered = initiallyPowered;
        }

        /// <summary>Sets the power state. No-op if unchanged.</summary>
        public void SetPowered(bool powered)
        {
            if (IsPowered == powered)
            {
                return;
            }

            IsPowered = powered;
            PowerStateChanged?.Invoke(powered);
        }

        /// <summary>Toggles the power state.</summary>
        public void TogglePower()
        {
            SetPowered(!IsPowered);
        }

        public override void Interact()
        {
            TogglePower();
        }

        public override string GetPrompt()
        {
            return IsPowered ? "Turn off power" : "Turn on power";
        }

        public override string GetGenericName()
        {
            return sourceName;
        }
    }
}
