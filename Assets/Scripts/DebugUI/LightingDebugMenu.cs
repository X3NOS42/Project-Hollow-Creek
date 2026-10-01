using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace HollowCreek.DebugUI
{
    /// <summary>
    /// Play-mode debug window toggled with F1. Lists every light in the scene
    /// with on/off buttons (plus All On / All Off). Auto-created at startup,
    /// so no scene object is needed.
    /// </summary>
    public class LightingDebugMenu : MonoBehaviour
    {
        private readonly List<Light> lights = new List<Light>();

        private bool visible;
        private Rect windowRect = new Rect(20f, 20f, 340f, 100f);

        /// <summary>Whether the menu is currently open. Gameplay input pauses while true.</summary>
        public static bool IsOpen { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Create()
        {
            IsOpen = false;
            GameObject host = new GameObject("Lighting Debug Menu");
            DontDestroyOnLoad(host);
            host.AddComponent<LightingDebugMenu>();
        }

        private void Update()
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
            {
                SetVisible(!visible);
            }
        }

        /// <summary>
        /// Opens or closes the menu. While open the mouse cursor is released so
        /// buttons can be clicked, and the Player action map is disabled so the
        /// camera does not spin and the player does not walk during clicks.
        /// </summary>
        private void SetVisible(bool open)
        {
            visible = open;
            IsOpen = open;

            if (open)
            {
                RefreshLights();
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                HollowCreek.Player.PlayerController.PlayerMap?.Disable();
            }
            else
            {
                Cursor.lockState = CursorLockMode.Locked;
                Cursor.visible = false;
                HollowCreek.Player.PlayerController.PlayerMap?.Enable();
            }
        }

        private void RefreshLights()
        {
            lights.Clear();
            lights.AddRange(FindObjectsByType<Light>(FindObjectsSortMode.None));
            lights.Sort((a, b) => string.CompareOrdinal(a.name, b.name));
        }

        private void OnGUI()
        {
            if (!visible)
            {
                return;
            }

            windowRect = GUILayout.Window(GetInstanceID(), windowRect, DrawWindow, "Lighting Debug (F1)");
        }

        private void DrawWindow(int id)
        {
            if (GUILayout.Button("Refresh"))
            {
                RefreshLights();
            }

            if (lights.Count == 0)
            {
                GUILayout.Label("(no lights found - click Refresh)");
            }

            foreach (Light light in lights)
            {
                if (light == null)
                {
                    continue;
                }

                // The flashlight beam is driven by intensity fades, so its real
                // on/off state lives on the Flashlight component, not light.enabled.
                HollowCreek.Player.Flashlight flashlight = light.GetComponent<HollowCreek.Player.Flashlight>();
                bool isOn = flashlight != null ? flashlight.IsOn : light.enabled;

                GUILayout.BeginHorizontal();
                GUILayout.Label(light.name, GUILayout.MinWidth(190f));
                if (GUILayout.Button(isOn ? "On" : "Off", GUILayout.Width(60f)))
                {
                    if (flashlight != null)
                    {
                        // Beam brightness is driven by the Flashlight fade, so only
                        // flip its state and keep the Light component enabled -
                        // disabling it would make pressing T look broken.
                        flashlight.SetOn(!isOn);
                        light.enabled = true;
                    }
                    else
                    {
                        light.enabled = !isOn;
                    }
                }
                GUILayout.EndHorizontal();

                if (flashlight != null)
                {
                    GUILayout.BeginHorizontal();
                    if (GUILayout.Button($"Flicker: {(flashlight.FlickerEnabled ? "On" : "Off")}"))
                    {
                        flashlight.FlickerEnabled = !flashlight.FlickerEnabled;
                    }
                    if (GUILayout.Button($"Thermal: {(flashlight.ThermalEnabled ? "On" : "Off")}"))
                    {
                        flashlight.ThermalEnabled = !flashlight.ThermalEnabled;
                    }
                    if (GUILayout.Button("Trigger Heat"))
                    {
                        // Enable so the heat holds instead of decaying; the
                        // engage message is logged by Flashlight itself.
                        flashlight.ThermalEnabled = true;
                        flashlight.TriggerThermal();
                    }
                    GUILayout.EndHorizontal();
                }

                // Intensity: the flashlight's value fades toward BaseIntensity,
                // candle flicker multiplies its own BaseIntensity, every other
                // light takes the slider value directly.
                HollowCreek.Environment.CandleFlicker candle =
                    light.GetComponent<HollowCreek.Environment.CandleFlicker>();

                GUILayout.BeginHorizontal();
                GUILayout.Label("Intensity", GUILayout.Width(66f));
                float currentIntensity = flashlight != null
                    ? flashlight.BaseIntensity
                    : candle != null
                        ? candle.BaseIntensity
                        : light.intensity;
                float newIntensity = GUILayout.HorizontalSlider(currentIntensity, 0f, 10f);
                if (!Mathf.Approximately(newIntensity, currentIntensity))
                {
                    if (flashlight != null)
                    {
                        flashlight.BaseIntensity = newIntensity;
                    }
                    else if (candle != null)
                    {
                        candle.BaseIntensity = newIntensity;
                    }
                    else
                    {
                        light.intensity = newIntensity;
                    }
                }
                GUILayout.Label(newIntensity.ToString("0.0"), GUILayout.Width(34f));
                GUILayout.EndHorizontal();

                // Color: per-channel sliders (no runtime color picker in IMGUI).
                GUILayout.BeginHorizontal();
                GUILayout.Label("R", GUILayout.Width(12f));
                float newR = GUILayout.HorizontalSlider(light.color.r, 0f, 1f);
                GUILayout.Label("G", GUILayout.Width(14f));
                float newG = GUILayout.HorizontalSlider(light.color.g, 0f, 1f);
                GUILayout.Label("B", GUILayout.Width(14f));
                float newB = GUILayout.HorizontalSlider(light.color.b, 0f, 1f);
                if (!Mathf.Approximately(newR, light.color.r) ||
                    !Mathf.Approximately(newG, light.color.g) ||
                    !Mathf.Approximately(newB, light.color.b))
                {
                    light.color = new Color(newR, newG, newB, light.color.a);
                }
                GUILayout.EndHorizontal();
            }

            GUILayout.BeginHorizontal();
            if (GUILayout.Button("All On"))
            {
                SetAll(true);
            }
            if (GUILayout.Button("All Off"))
            {
                SetAll(false);
            }
            GUILayout.EndHorizontal();

            GUI.DragWindow(new Rect(0f, 0f, 10000f, 20f));
        }

        private void SetAll(bool on)
        {
            foreach (Light light in lights)
            {
                if (light == null)
                {
                    continue;
                }

                HollowCreek.Player.Flashlight flashlight = light.GetComponent<HollowCreek.Player.Flashlight>();
                if (flashlight != null)
                {
                    flashlight.SetOn(on);
                    light.enabled = true;
                }
                else
                {
                    light.enabled = on;
                }
            }
        }
    }
}
