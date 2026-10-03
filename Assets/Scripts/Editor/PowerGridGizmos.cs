using HollowCreek.Power;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Scene-view overlay drawing the power grid: source -> device and
    /// switch -> device lines with state colors. Drawn on top of everything
    /// (through walls) like a debug overlay. Toggle via
    /// Tools > Hollow Creek > Power Grid Gizmos (persisted in EditorPrefs).
    /// </summary>
    public static class PowerGridGizmos
    {
        private const string PrefsKey = "HollowCreek.PowerGridGizmos";

        private static readonly Color PoweredLine = new Color(0.25f, 1f, 0.35f, 0.9f);
        private static readonly Color UnpoweredLine = new Color(0.55f, 0.55f, 0.55f, 0.8f);
        private static readonly Color SwitchOnLine = new Color(1f, 0.65f, 0.15f, 0.9f);
        private static readonly Color SwitchOffLine = new Color(0.45f, 0.32f, 0.12f, 0.8f);
        private static readonly Color NoSourceColor = new Color(1f, 0.25f, 0.25f, 0.95f);

        /// <summary>Whether the power grid overlay is visible in the Scene view.</summary>
        public static bool Enabled
        {
            get => EditorPrefs.GetBool(PrefsKey, false);
            set => EditorPrefs.SetBool(PrefsKey, value);
        }

        [InitializeOnLoadMethod]
        private static void Initialize()
        {
            SceneView.duringSceneGui -= OnSceneGUI;
            SceneView.duringSceneGui += OnSceneGUI;
        }

        [MenuItem("Tools/Hollow Creek/Power Grid Gizmos")]
        private static void ToggleMenu()
        {
            Enabled = !Enabled;
            SceneView.RepaintAll();
        }

        [MenuItem("Tools/Hollow Creek/Power Grid Gizmos", true)]
        private static bool ToggleMenuValidate()
        {
            Menu.SetChecked("Tools/Hollow Creek/Power Grid Gizmos", Enabled);
            return true;
        }

        private static void OnSceneGUI(SceneView sceneView)
        {
            if (!Enabled)
            {
                return;
            }

            PowerSource[] sources = Object.FindObjectsByType<PowerSource>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            PowerDevice[] devices = Object.FindObjectsByType<PowerDevice>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);
            LightSwitch[] switches = Object.FindObjectsByType<LightSwitch>(
                FindObjectsInactive.Include, FindObjectsSortMode.None);

            CompareFunction previousZTest = Handles.zTest;
            try
            {
                // Draw on top of geometry so wiring is visible from anywhere.
                Handles.zTest = CompareFunction.Always;

                foreach (PowerSource source in sources)
                {
                    DrawSource(source);
                }

                foreach (PowerDevice device in devices)
                {
                    DrawDevice(device);
                }

                foreach (LightSwitch lightSwitch in switches)
                {
                    DrawSwitch(lightSwitch);
                }

                DrawStatus(sources.Length, switches.Length, devices.Length);
            }
            finally
            {
                Handles.zTest = previousZTest;
            }
        }

        private static void DrawStatus(int sourceCount, int switchCount, int deviceCount)
        {
            Handles.BeginGUI();
            GUILayout.BeginArea(new Rect(10f, 30f, 320f, 44f));
            EditorGUILayout.HelpBox(
                $"Power Grid: {sourceCount} source(s), {switchCount} switch(es), " +
                $"{deviceCount} device(s)",
                MessageType.None);
            GUILayout.EndArea();
            Handles.EndGUI();
        }

        private static void DrawSource(PowerSource source)
        {
            Vector3 pos = source.transform.position;

            Handles.color = source.IsPowered ? PoweredLine : UnpoweredLine;
            Handles.DrawWireCube(pos, Vector3.one * 0.5f);
            Handles.Label(pos + Vector3.up * 0.55f,
                $"{source.SourceName} - {(source.IsPowered ? "ON" : "OFF")}");
        }

        private static void DrawDevice(PowerDevice device)
        {
            Vector3 pos = device.transform.position;

            if (device.Source == null)
            {
                Handles.color = NoSourceColor;
                Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.35f,
                    EventType.Repaint);
                Handles.Label(pos + Vector3.up * 0.3f, $"! {device.name}: no source");
                return;
            }

            Color line = device.IsEffectivelyPowered ? PoweredLine : UnpoweredLine;
            Vector3 sourcePos = device.Source.transform.position;
            Handles.color = line;
            Handles.DrawAAPolyLine(3f, sourcePos, pos);
            Handles.SphereHandleCap(0, pos, Quaternion.identity, 0.2f,
                EventType.Repaint);
        }

        private static void DrawSwitch(LightSwitch lightSwitch)
        {
            Vector3 pos = lightSwitch.transform.position;

            Handles.color = lightSwitch.IsOn ? SwitchOnLine : SwitchOffLine;
            Handles.DrawWireCube(pos, new Vector3(0.18f, 0.24f, 0.18f));

            foreach (PowerDevice device in lightSwitch.Devices)
            {
                if (device == null)
                {
                    continue;
                }

                Handles.DrawAAPolyLine(2f, pos, device.transform.position);
            }
        }
    }
}
