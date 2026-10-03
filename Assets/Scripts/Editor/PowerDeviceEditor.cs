using HollowCreek.Power;
using UnityEditor;
using UnityEngine;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Adds a visible warning to the PowerDevice Inspector when no power
    /// source is assigned (the device stays off and logs on Play).
    /// </summary>
    [CustomEditor(typeof(PowerDevice))]
    public class PowerDeviceEditor : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();

            PowerDevice device = (PowerDevice)target;

            if (device.Source == null)
            {
                EditorGUILayout.HelpBox(
                    "No Power Source connected! This device will stay OFF and " +
                    "log a warning on Play. Assign a source (or use Power tool " +
                    "> Link).",
                    MessageType.Warning);
            }
            else if (device.ControllingSwitch == null)
            {
                EditorGUILayout.HelpBox(
                    "No Light Switch assigned - this device follows the power " +
                    "source only (always on while the source is powered).",
                    MessageType.Info);
            }

            if (Application.isPlaying && device.Source != null)
            {
                EditorGUILayout.LabelField(
                    "Current state",
                    device.IsEffectivelyPowered ? "POWERED" : "OFF");
            }
        }
    }
}
