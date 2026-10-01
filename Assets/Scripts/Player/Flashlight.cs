using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowCreek.Player
{
    /// <summary>
    /// Camera-mounted flashlight with a smooth on/off fade, optional loose-contact
    /// flicker, and optional thermal step-down (slow dimming during long use).
    /// Toggle with the "Flashlight" input action (T key).
    /// Attach to the same GameObject as the spotlight Light.
    /// </summary>
    public class Flashlight : MonoBehaviour
    {
        [Header("Input")]
        [SerializeField] private InputActionAsset inputActions;
        [SerializeField] private string actionName = "Flashlight";

        [Header("Fade")]
        [SerializeField] private float fadeSpeed = 8f;

        [Header("Flicker (loose contact)")]
        [SerializeField] private bool flickerEnabled = true;
        [SerializeField] private Vector2 flickerIntervalRange = new Vector2(5f, 18f);
        [SerializeField, Range(0.05f, 1f)] private float flickerDip = 0.4f;
        [SerializeField] private float flickerDuration = 0.1f;
        [SerializeField, Range(0f, 1f)] private float flickerDoubleChance = 0.35f;

        [Header("Thermal Step-Down")]
        [SerializeField] private bool thermalStepDownEnabled = true;
        [SerializeField] private float heatSeconds = 180f;
        [SerializeField, Range(0f, 0.9f)] private float thermalMaxDim = 0.15f;
        [SerializeField] private float cooldownSeconds = 60f;

        private Light lightSource;
        private InputAction toggleAction;
        private InputAction fallbackAction;
        private bool isOn;
        private float targetIntensity;
        private float referenceIntensity;
        private float baseIntensity;

        private float heat;
        private bool heatFullLogged;
        private float nextFlickerIn;
        private float flickerPhaseTimeLeft;
        private int flickerPhase;
        private bool flickerDouble;
        private float flickerMultiplier = 1f;

        /// <summary>Whether the flashlight beam is currently on.</summary>
        public bool IsOn => isOn;

        /// <summary>Loose-contact flicker on/off. Also togglable in the F1 menu.</summary>
        public bool FlickerEnabled { get => flickerEnabled; set => flickerEnabled = value; }

        /// <summary>Thermal step-down dimming on/off. Also togglable in the F1 menu.</summary>
        public bool ThermalEnabled { get => thermalStepDownEnabled; set => thermalStepDownEnabled = value; }

        /// <summary>Speed of the on/off fade transition.</summary>
        public float FadeSpeed { get => fadeSpeed; set => fadeSpeed = value; }

        /// <summary>
        /// Steady intensity the beam fades toward (the F1 menu's intensity slider).
        /// Kept separate from referenceIntensity so setting this to 0 still allows
        /// the fade to run.
        /// </summary>
        public float BaseIntensity
        {
            get => targetIntensity;
            set => targetIntensity = Mathf.Max(0f, value);
        }

        /// <summary>
        /// Turns the beam on or off. Used by the F1 debug menu so its button
        /// shows the real beam state instead of only the Light component flag.
        /// </summary>
        public void SetOn(bool on)
        {
            isOn = on;
        }

        /// <summary>
        /// Debug helper (F1 menu): instantly drives heat to 100% so the thermal
        /// step-down can be seen without waiting for heatSeconds. Requires the
        /// thermal effect to be enabled, otherwise heat decays immediately.
        /// </summary>
        public void TriggerThermal()
        {
            heat = 1f;
        }

        private void Awake()
        {
            lightSource = GetComponent<Light>();
            if (lightSource == null)
            {
                Debug.LogWarning("[Flashlight] No Light component found on this GameObject. Add a spotlight.");
            }
            else
            {
                targetIntensity = lightSource.intensity;
                referenceIntensity = targetIntensity;
                lightSource.intensity = 0f;
            }

            nextFlickerIn = Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);

            // Prefer the action from the shared asset; fall back to a code-created
            // <Keyboard>/t binding so the flashlight still works if the asset is missing it.
            toggleAction = inputActions != null
                ? inputActions.FindAction(actionName, throwIfNotFound: false)
                : null;

            if (toggleAction == null)
            {
                fallbackAction = new InputAction("Flashlight (fallback)", InputActionType.Button, "<Keyboard>/t");
                toggleAction = fallbackAction;
            }

            toggleAction.performed += OnTogglePerformed;
        }

        private void OnEnable()
        {
            toggleAction?.Enable();
        }

        private void OnDisable()
        {
            toggleAction?.Disable();
        }

        private void OnDestroy()
        {
            if (toggleAction != null)
            {
                toggleAction.performed -= OnTogglePerformed;
            }
            fallbackAction?.Dispose();
        }

        private void OnTogglePerformed(InputAction.CallbackContext context)
        {
            isOn = !isOn;
        }

        private void Update()
        {
            if (lightSource == null)
            {
                return;
            }

            UpdateThermal();
            UpdateFlicker();

            float target = isOn ? targetIntensity : 0f;
            baseIntensity = Mathf.MoveTowards(
                baseIntensity, target, fadeSpeed * referenceIntensity * Time.deltaTime);

            // Flicker and thermal apply instantly to the faded value so a dip never
            // fights the smooth on/off fade.
            float thermalFactor = 1f - heat * thermalMaxDim;
            lightSource.intensity = baseIntensity * thermalFactor * flickerMultiplier;
        }

        /// <summary>
        /// Builds heat while the beam is on (dimming output) and sheds it while off
        /// or when the effect is disabled.
        /// </summary>
        private void UpdateThermal()
        {
            bool heating = isOn && thermalStepDownEnabled;
            float target = heating ? 1f : 0f;
            float seconds = Mathf.Max(0.01f, heating ? heatSeconds : cooldownSeconds);
            heat = Mathf.MoveTowards(heat, target, Time.deltaTime / seconds);

            // One log per state change, whether heat got there by waiting or
            // by the debug menu's Trigger Heat button.
            if (!heatFullLogged && heat >= 1f)
            {
                heatFullLogged = true;
                int outputPercent = Mathf.RoundToInt((1f - thermalMaxDim) * 100f);
                Debug.Log($"[Flashlight] Thermal step-down engaged: beam output reduced to {outputPercent}%.");
            }
            else if (heatFullLogged && heat <= 0f)
            {
                heatFullLogged = false;
                Debug.Log("[Flashlight] Thermal recovered: beam output back to 100%.");
            }
        }

        /// <summary>
        /// Schedules rare brief dips while the beam is on. Phases: 1 = first dip,
        /// 2 = steady gap, 3 = optional second dip. Resets when off/disabled so a
        /// flicker never fires right after turning the light on.
        /// </summary>
        private void UpdateFlicker()
        {
            if (!isOn || !flickerEnabled)
            {
                flickerMultiplier = 1f;
                flickerPhase = 0;
                nextFlickerIn = Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);
                return;
            }

            if (flickerPhase == 0)
            {
                nextFlickerIn -= Time.deltaTime;
                if (nextFlickerIn > 0f)
                {
                    return;
                }
                flickerDouble = Random.value < flickerDoubleChance;
                flickerPhase = 1;
                flickerPhaseTimeLeft = flickerDuration;
                flickerMultiplier = flickerDip;
                return;
            }

            flickerPhaseTimeLeft -= Time.deltaTime;
            if (flickerPhaseTimeLeft > 0f)
            {
                return;
            }

            switch (flickerPhase)
            {
                case 1: // first dip finished
                    if (flickerDouble)
                    {
                        flickerPhase = 2;
                        flickerPhaseTimeLeft = 0.04f;
                        flickerMultiplier = 1f;
                    }
                    else
                    {
                        EndFlicker();
                    }
                    break;

                case 2: // steady gap finished
                    flickerPhase = 3;
                    flickerPhaseTimeLeft = flickerDuration;
                    flickerMultiplier = flickerDip;
                    break;

                default: // second dip finished
                    EndFlicker();
                    break;
            }
        }

        private void EndFlicker()
        {
            flickerPhase = 0;
            flickerMultiplier = 1f;
            nextFlickerIn = Random.Range(flickerIntervalRange.x, flickerIntervalRange.y);
        }
    }
}
