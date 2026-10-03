using System.Collections.Generic;
using HollowCreek.Interaction;
using UnityEngine;

namespace HollowCreek.Power
{
    /// <summary>
    /// A wall light switch. Toggles every linked PowerDevice between on and off
    /// (only effective while its PowerSource is powered). Interactable: E flips.
    /// </summary>
    public class LightSwitch : InteractableObject
    {
        [Header("Switch")]
        [SerializeField] private string switchName = "Light Switch";
        [SerializeField] private bool isOn = true;

        [Header("Linked Devices")]
        [SerializeField] private List<PowerDevice> devices = new List<PowerDevice>();

        [Header("Lever")]
        [Tooltip("Optional child transform that flips up/down with the switch.")]
        [SerializeField] private Transform lever = null;

        [Tooltip("Lever offset from pivot when on vs off (local Y).")]
        [SerializeField] private float leverUpY = 0.25f;
        [SerializeField] private float leverDownY = -0.25f;

        /// <summary>Whether the switch is currently flipped on.</summary>
        public bool IsOn => isOn;

        /// <summary>Display name used by prompts and gizmos.</summary>
        public string SwitchName => switchName;

        /// <summary>Devices this switch controls.</summary>
        public IReadOnlyList<PowerDevice> Devices => devices;

        public override void Interact()
        {
            SetSwitch(!isOn);
        }

        /// <summary>Sets the switch state and updates all linked devices.</summary>
        public void SetSwitch(bool on)
        {
            isOn = on;
            ApplyLeverPosition();
            ApplyDevices();
        }

        /// <summary>Toggles the switch state.</summary>
        public void ToggleSwitch()
        {
            SetSwitch(!isOn);
        }

        /// <summary>Adds a device link (both directions).</summary>
        public void AddDevice(PowerDevice device)
        {
            if (device == null || devices.Contains(device))
            {
                return;
            }

            devices.Add(device);
            device.SetControllingSwitch(this);
            device.ApplyState();
        }

        /// <summary>Removes a device link (both directions).</summary>
        public void RemoveDevice(PowerDevice device)
        {
            if (device == null || !devices.Remove(device))
            {
                return;
            }

            if (device.ControllingSwitch == this)
            {
                device.SetControllingSwitch(null);
                device.ApplyState();
            }
        }

        /// <summary>Clears all device links.</summary>
        public void ClearDevices()
        {
            foreach (PowerDevice device in devices)
            {
                if (device != null && device.ControllingSwitch == this)
                {
                    device.SetControllingSwitch(null);
                    device.ApplyState();
                }
            }

            devices.Clear();
        }

        public override string GetPrompt()
        {
            return isOn ? "Turn off the light" : "Turn on the light";
        }

        public override string GetGenericName()
        {
            return switchName;
        }

        private void OnEnable()
        {
            ApplyLeverPosition();
        }

        protected override void OnValidate()
        {
            base.OnValidate();
            ApplyLeverPosition();
        }

        private void ApplyLeverPosition()
        {
            if (lever != null)
            {
                Vector3 local = lever.localPosition;
                local.y = isOn ? leverUpY : leverDownY;
                lever.localPosition = local;
            }
        }

        private void ApplyDevices()
        {
            for (int i = devices.Count - 1; i >= 0; i--)
            {
                PowerDevice device = devices[i];
                if (device == null)
                {
                    devices.RemoveAt(i);
                    continue;
                }

                device.ApplyStateInstant();
            }
        }
    }
}
