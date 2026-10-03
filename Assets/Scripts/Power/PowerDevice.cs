using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace HollowCreek.Power
{
    /// <summary>
    /// Placed on anything that uses mains power (lights, future electric props).
    /// Links a device to a PowerSource, optionally gated by a LightSwitch.
    /// Effective state: source powered AND (no switch OR switch on).
    /// Devices with no source log a warning and stay off.
    /// </summary>
    public class PowerDevice : MonoBehaviour
    {
        [Header("Power")]
        [SerializeField] private PowerSource source;
        [SerializeField] private LightSwitch controllingSwitch;

        [Header("Device")]
        [Tooltip("Auto-found from this object or children if left empty.")]
        [SerializeField] private Light deviceLight;
        [SerializeField] private bool affectLight = true;

        [Header("Events")]
        public UnityEvent onPowerOn;
        public UnityEvent onPowerOff;

        private Coroutine flickerRoutine;
        private bool hasApplied;
        private bool currentState;

        /// <summary>The assigned power source (may be null).</summary>
        public PowerSource Source => source;

        /// <summary>The assigned light switch (may be null = no local switch).</summary>
        public LightSwitch ControllingSwitch => controllingSwitch;

        /// <summary>The light this device drives (may be null for non-light devices).</summary>
        public Light DeviceLight => deviceLight;

        /// <summary>
        /// Whether the device is receiving effective power right now:
        /// source powered AND (no switch OR switch on). False when unlinked.
        /// </summary>
        public bool IsEffectivelyPowered =>
            source != null && source.IsPowered &&
            (controllingSwitch == null || controllingSwitch.IsOn);

        private void Awake()
        {
            if (source == null)
            {
                Debug.LogWarning(
                    $"[PowerDevice] '{name}' has no power source connected. " +
                    "It will stay off.", this);
            }

            if (deviceLight == null)
            {
                deviceLight = GetComponent<Light>();
                if (deviceLight == null)
                {
                    deviceLight = GetComponentInChildren<Light>(true);
                }
            }
        }

        private void OnEnable()
        {
            if (source != null)
            {
                source.PowerStateChanged += HandlePowerStateChanged;
            }
        }

        private void Start()
        {
            ApplyState(instant: true);
            hasApplied = true;
        }

        private void OnDisable()
        {
            if (source != null)
            {
                source.PowerStateChanged -= HandlePowerStateChanged;
            }

            StopFlicker();
        }

        /// <summary>Assigns or clears the power source at runtime.</summary>
        public void SetSource(PowerSource newSource)
        {
            if (source == newSource)
            {
                return;
            }

            if (source != null && isActiveAndEnabled)
            {
                source.PowerStateChanged -= HandlePowerStateChanged;
            }

            source = newSource;

            if (source != null && isActiveAndEnabled)
            {
                source.PowerStateChanged += HandlePowerStateChanged;
            }

            ApplyState();
        }

        /// <summary>Assigns or clears the controlling light switch.</summary>
        public void SetControllingSwitch(LightSwitch newSwitch)
        {
            if (controllingSwitch == newSwitch)
            {
                return;
            }

            controllingSwitch = newSwitch;
            ApplyState();
        }

        /// <summary>
        /// Re-evaluates the effective state. Called by the source event, the
        /// switch, and the tool after linking. Power loss flickers.
        /// </summary>
        public void ApplyState()
        {
            ApplyState(instant: false);
        }

        /// <summary>
        /// Re-evaluates the effective state with no flicker - used by light
        /// switches so flipping one kills/starts the light immediately.
        /// </summary>
        public void ApplyStateInstant()
        {
            ApplyState(instant: true);
        }

        private void ApplyState(bool instant)
        {
            bool powered = IsEffectivelyPowered;

            if (!hasApplied)
            {
                // First application: snap to the correct state, no flicker,
                // no events (nothing changed yet from the player's view).
                StopFlicker();
                SetLightImmediate(powered);
                currentState = powered;
                return;
            }

            if (powered == currentState)
            {
                return;
            }

            if (powered)
            {
                StopFlicker();
                SetLightImmediate(true);
                currentState = true;
                FireEvents(on: true, invoked: true);
            }
            else if (instant)
            {
                StopFlicker();
                SetLightImmediate(false);
                currentState = false;
                FireEvents(on: false, invoked: true);
            }
            else
            {
                // Only flicker if the device was actually on; a switch flip
                // while the power is out is remembered silently.
                if (currentState)
                {
                    currentState = false;
                    flickerRoutine = StartCoroutine(FlickerOut());
                    FireEvents(on: false, invoked: true);
                }
            }
        }

        private void HandlePowerStateChanged(bool powered)
        {
            ApplyState();
        }

        private void SetLightImmediate(bool on)
        {
            if (affectLight && deviceLight != null)
            {
                deviceLight.enabled = on;
            }
        }

        private void FireEvents(bool on, bool invoked)
        {
            if (!invoked)
            {
                return;
            }

            if (on)
            {
                onPowerOn?.Invoke();
            }
            else
            {
                onPowerOff?.Invoke();
            }
        }

        private IEnumerator FlickerOut()
        {
            float duration = Random.Range(0.5f, 1f);
            float elapsed = 0f;
            bool lastState = false;

            while (elapsed < duration)
            {
                lastState = !lastState;
                if (affectLight && deviceLight != null)
                {
                    deviceLight.enabled = lastState;
                }

                float step = Random.Range(0.03f, 0.12f);
                elapsed += step;
                yield return new WaitForSeconds(step);
            }

            if (affectLight && deviceLight != null)
            {
                deviceLight.enabled = false;
            }

            flickerRoutine = null;
        }

        private void StopFlicker()
        {
            if (flickerRoutine != null)
            {
                StopCoroutine(flickerRoutine);
                flickerRoutine = null;
            }
        }
    }
}
