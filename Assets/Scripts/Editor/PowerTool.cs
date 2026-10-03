using System.Collections.Generic;
using HollowCreek.Power;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace HollowCreek.Editor
{
    /// <summary>
    /// One-button electricity setup: spawn the power box, place wall switches
    /// for every room light, and link/unlink devices. Gizmo toggle included.
    /// Access via menu: Tools > Hollow Creek > Power...
    /// </summary>
    public class PowerTool : EditorWindow
    {
        private const string PowerBoxMaterialPath = "Assets/Materials/Graybox_PowerBox.mat";
        private const string SwitchMaterialPath = "Assets/Materials/Graybox_Switch.mat";
        private const string SwitchLeverMaterialPath = "Assets/Materials/Graybox_SwitchLever.mat";
        private const string PowerBoxName = "Power Box";
        private const string SwitchPrefix = "Light Switch ";
        private const string LightContainerName = "Interior Lights";
        private const string EnvironmentRootName = "--- Environment ---";

        private struct RoomSwitchEntry
        {
            public string LightName;
            public string RoomName;
            public Vector3 Position;
            public float RotY;

            public RoomSwitchEntry(string lightName, string roomName, Vector3 position, float rotY)
            {
                LightName = lightName;
                RoomName = roomName;
                Position = position;
                RotY = rotY;
            }
        }

        // One switch per room light, beside the room's doorway at 2.2 m
        // world height (floor is y=1, so ~1.1 m above it).
        private static readonly RoomSwitchEntry[] RoomSwitches =
        {
            new RoomSwitchEntry("Light_LivingRoom", "Living Room",
                new Vector3(16.28f, 2.2f, -98.4f), -90f),
            new RoomSwitchEntry("Light_Hallway", "Hallway",
                new Vector3(19.49f, 2.2f, -93f), -90f),
            new RoomSwitchEntry("Light_Bedroom_Front", "Bedroom Front",
                new Vector3(19.73f, 2.2f, -95.3f), 90f),
            new RoomSwitchEntry("Light_Bedroom_Back", "Bedroom Back",
                new Vector3(19.73f, 2.2f, -99.7f), 90f)
        };

        private static readonly Vector3 DefaultBoxPosition = new Vector3(14f, 0f, -89.5f);
        private static readonly Vector3 DefaultSwitchPosition = new Vector3(16.52f, 2.2f, -97f);
        private static readonly Vector3 LegacyBoxPosition = new Vector3(15.2f, 0f, -91.9f);
        private static readonly Vector3 LegacySwitchPosition = new Vector3(15.2f, 1.2f, -91.9f);

        private Vector3 boxPosition = DefaultBoxPosition;
        private Vector3 switchPosition = DefaultSwitchPosition;
        private int sourceIndex;
        private int switchIndex;
        [SerializeField] private bool defaultsMigrated;

        private void OnEnable()
        {
            if (defaultsMigrated)
            {
                return;
            }
            // Older window layouts persisted the pre-foundation defaults
            // (box buried in the slab, switch at ankle height). Replace them.
            if ((boxPosition - LegacyBoxPosition).sqrMagnitude < 0.0001f)
            {
                boxPosition = DefaultBoxPosition;
            }
            if ((switchPosition - LegacySwitchPosition).sqrMagnitude < 0.0001f)
            {
                switchPosition = DefaultSwitchPosition;
            }
            defaultsMigrated = true;
        }

        [MenuItem("Tools/Hollow Creek/Power...")]
        private static void Open()
        {
            PowerTool window = GetWindow<PowerTool>("Power");
            window.minSize = new Vector2(430, 520);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Electricity: the Power Box supplies power, wall switches flip " +
                "individual rooms, and Power Devices receive both. Use E in play " +
                "mode on the box or a switch.",
                MessageType.Info);

            PowerSource[] sources = FindAll<PowerSource>();
            LightSwitch[] switches = FindAll<LightSwitch>();
            PowerDevice[] devices = FindAll<PowerDevice>();

            DrawPowerBoxSection();
            EditorGUILayout.Space(8);
            DrawSwitchSection(sources, switches);
            EditorGUILayout.Space(8);
            DrawDeviceSection(sources, switches, devices);
            EditorGUILayout.Space(8);
            DrawGizmoSection();
        }

        // ---------------------------------------------------------- power box

        private void DrawPowerBoxSection()
        {
            EditorGUILayout.LabelField("1. Power Box", EditorStyles.boldLabel);
            boxPosition = EditorGUILayout.Vector3Field("Position (base at ground)", boxPosition);

            if (GUILayout.Button("Add Power Box", GUILayout.Height(26)))
            {
                BuildPowerBox();
            }
        }

        private void BuildPowerBox()
        {
            Undo.SetCurrentGroupName("Add Power Box");
            int group = Undo.GetCurrentGroup();

            Material material = EnsureMaterial(PowerBoxMaterialPath,
                new Color(0.25f, 0.25f, 0.27f, 1f), 0.35f);
            if (material == null)
            {
                Debug.LogWarning("[PowerTool] Could not create power box material. Aborting.");
                return;
            }

            Transform old = FindDeepChildInScene(PowerBoxName);
            PowerSource oldSource = old != null ? old.GetComponent<PowerSource>() : null;
            List<PowerDevice> linkedDevices = null;
            if (oldSource != null)
            {
                linkedDevices = new List<PowerDevice>();
                foreach (PowerDevice device in FindAll<PowerDevice>())
                {
                    if (device.Source == oldSource)
                    {
                        linkedDevices.Add(device);
                    }
                }
            }

            if (old != null)
            {
                Undo.DestroyObjectImmediate(old.gameObject);
            }

            GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
            box.name = PowerBoxName;
            box.transform.SetParent(EnvironmentRoot(), false);
            box.transform.localPosition = boxPosition + Vector3.up * 0.9f;
            box.transform.localScale = new Vector3(0.5f, 0.7f, 0.25f);
            box.GetComponent<Renderer>().material = material;

            GameObject post = GameObject.CreatePrimitive(PrimitiveType.Cube);
            post.name = "Post";
            post.transform.SetParent(box.transform, false);
            post.transform.localPosition = new Vector3(0f, -0.892857f, 0f);
            post.transform.localScale = new Vector3(0.2f, 0.7857f, 0.4f);
            post.GetComponent<Renderer>().material = material;

            PowerSource source = box.AddComponent<PowerSource>();
            Undo.RegisterCreatedObjectUndo(box, "Add Power Box");

            if (linkedDevices != null)
            {
                foreach (PowerDevice device in linkedDevices)
                {
                    if (device == null)
                    {
                        continue;
                    }
                    device.SetSource(source);
                    EditorUtility.SetDirty(device);
                }
                Debug.Log($"[PowerTool] Rebound {linkedDevices.Count} device(s) " +
                    "to the new power box.");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = box;
            Debug.Log("[PowerTool] Power Box added at " + boxPosition + ".");
        }

        // ------------------------------------------------------- light switches

        private void DrawSwitchSection(PowerSource[] sources, LightSwitch[] switches)
        {
            EditorGUILayout.LabelField("2. Light Switches", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "One per room, placed beside the doorway (1.1 m above the " +
                "floor). Re-running replaces all switches; links to lights " +
                "are rebuilt.",
                EditorStyles.miniLabel);

            if (GUILayout.Button("Add Switches For All Rooms", GUILayout.Height(26)))
            {
                BuildAllRoomSwitches();
            }

            EditorGUILayout.Space(4);
            switchPosition = EditorGUILayout.Vector3Field("Position", switchPosition);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Light Switch", GUILayout.Height(26)))
            {
                BuildManualSwitch();
            }
            EditorGUI.BeginDisabledGroup(switches.Length == 0);
            switchIndex = EditorGUILayout.Popup(Mathf.Clamp(switchIndex, 0,
                switches.Length - 1), NameArray(switches));
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(switches.Length == 0 || Selection.gameObjects.Length == 0);
            if (GUILayout.Button("Link Selection to Switch"))
            {
                LinkSelectionToSwitch(switches);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUI.BeginDisabledGroup(switches.Length == 0);
            if (GUILayout.Button("Unlink Selection"))
            {
                UnlinkSelectionFromSwitch(switches);
            }
            if (GUILayout.Button("Clear Switch Links"))
            {
                Undo.SetCurrentGroupName("Clear Switch Links");
                int group = Undo.GetCurrentGroup();
                foreach (LightSwitch lightSwitch in switches)
                {
                    Undo.RecordObject(lightSwitch, "Clear Switch Links");
                    lightSwitch.ClearDevices();
                    EditorUtility.SetDirty(lightSwitch);
                }
                Undo.CollapseUndoOperations(group);
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();
        }

        private void BuildAllRoomSwitches()
        {
            Undo.SetCurrentGroupName("Add Room Light Switches");
            int group = Undo.GetCurrentGroup();

            Material material = EnsureMaterial(SwitchMaterialPath,
                new Color(0.85f, 0.85f, 0.85f, 1f), 0.4f);
            if (material == null)
            {
                Debug.LogWarning("[PowerTool] Could not create switch material. Aborting.");
                return;
            }

            foreach (Transform existing in FindAllDeepChildInScene(SwitchPrefix))
            {
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            PowerSource[] sources = FindAll<PowerSource>();
            PowerSource source = sources.Length > 0 ? sources[0] : null;

            int created = 0;
            foreach (RoomSwitchEntry entry in RoomSwitches)
            {
                LightSwitch lightSwitch = CreateSwitch(
                    entry.Position, entry.RotY, $"{SwitchPrefix}{entry.RoomName}", material);

                GameObject lightObject = GameObject.Find(entry.LightName);
                if (lightObject == null)
                {
                    Debug.LogWarning(
                        $"[PowerTool] Light '{entry.LightName}' not found - " +
                        $"switch '{entry.RoomName}' was created but not linked.");
                    created++;
                    continue;
                }

                PowerDevice device = EnsurePowerDevice(lightObject);
                if (device.Source == null && source != null)
                {
                    device.SetSource(source);
                    EditorUtility.SetDirty(device);
                }

                lightSwitch.AddDevice(device);
                EditorUtility.SetDirty(lightSwitch);
                created++;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[PowerTool] Created {created} room light switches" +
                (source != null ? " and linked room lights to the power box." : "."));
        }

        private void BuildManualSwitch()
        {
            Undo.SetCurrentGroupName("Add Light Switch");
            int group = Undo.GetCurrentGroup();

            Material material = EnsureMaterial(SwitchMaterialPath,
                new Color(0.85f, 0.85f, 0.85f, 1f), 0.4f);
            if (material == null)
            {
                return;
            }

            int number = FindAllDeepChildInScene(SwitchPrefix).Count + 1;
            LightSwitch lightSwitch = CreateSwitch(
                switchPosition, 90f, $"{SwitchPrefix}{number}", material);

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = lightSwitch.gameObject;
            Debug.Log($"[PowerTool] Light switch '{lightSwitch.name}' added. " +
                "Select the lights it should control and click 'Link Selection to Switch'.");
        }

        private LightSwitch CreateSwitch(Vector3 position, float rotY, string objectName,
            Material material)
        {
            GameObject plate = GameObject.CreatePrimitive(PrimitiveType.Cube);
            plate.name = objectName;
            plate.transform.SetParent(EnvironmentRoot(), false);
            plate.transform.localPosition = position;
            plate.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            plate.transform.localScale = new Vector3(0.12f, 0.18f, 0.04f);
            plate.GetComponent<Renderer>().material = material;

            GameObject lever = GameObject.CreatePrimitive(PrimitiveType.Cube);
            lever.name = "Lever";
            lever.transform.SetParent(plate.transform, false);
            lever.transform.localPosition = new Vector3(0f, 0.25f, 0.875f);
            lever.transform.localScale = new Vector3(0.25f, 0.28f, 0.75f);
            Material leverMaterial = EnsureLeverMaterial();
            lever.GetComponent<Renderer>().material =
                leverMaterial != null ? leverMaterial : material;
            lever.GetComponent<Collider>().isTrigger = true;

            LightSwitch lightSwitch = plate.AddComponent<LightSwitch>();
            SerializedObject serialized = new SerializedObject(lightSwitch);
            serialized.FindProperty("switchName").stringValue = objectName;
            serialized.FindProperty("lever").objectReferenceValue = lever.transform;
            serialized.ApplyModifiedProperties();
            lightSwitch.SetSwitch(lightSwitch.IsOn);
            Undo.RegisterCreatedObjectUndo(plate, "Add Light Switch");

            return lightSwitch;
        }

        private void LinkSelectionToSwitch(LightSwitch[] switches)
        {
            if (switches.Length == 0)
            {
                return;
            }

            LightSwitch target = switches[Mathf.Clamp(switchIndex, 0, switches.Length - 1)];

            Undo.SetCurrentGroupName("Link Selection to Switch");
            int group = Undo.GetCurrentGroup();

            int linked = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                PowerDevice device = EnsurePowerDevice(selected);
                target.AddDevice(device);
                EditorUtility.SetDirty(device);
                linked++;
            }

            EditorUtility.SetDirty(target);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[PowerTool] Linked {linked} object(s) to switch '{target.name}'.");
        }

        private void UnlinkSelectionFromSwitch(LightSwitch[] switches)
        {
            if (switches.Length == 0)
            {
                return;
            }

            LightSwitch target = switches[Mathf.Clamp(switchIndex, 0, switches.Length - 1)];

            Undo.SetCurrentGroupName("Unlink Selection from Switch");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject selected in Selection.gameObjects)
            {
                PowerDevice device = selected.GetComponent<PowerDevice>();
                if (device != null)
                {
                    target.RemoveDevice(device);
                    EditorUtility.SetDirty(device);
                }
            }

            EditorUtility.SetDirty(target);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------- devices

        private void DrawDeviceSection(PowerSource[] sources, LightSwitch[] switches,
            PowerDevice[] devices)
        {
            EditorGUILayout.LabelField("3. Power Devices", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Power Device to Selection", GUILayout.Height(26)))
            {
                AddPowerDeviceToSelection();
            }
            if (GUILayout.Button("Link All Room Lights", GUILayout.Height(26)))
            {
                LinkAllRoomLights();
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            int newSourceIndex = DrawPopup("Source", sources, sourceIndex);
            sourceIndex = newSourceIndex;
            EditorGUI.BeginDisabledGroup(sources.Length == 0 || Selection.gameObjects.Length == 0);
            if (GUILayout.Button("Link Selection", GUILayout.Width(110)))
            {
                LinkSelectionToSource(sources);
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            EditorGUI.BeginDisabledGroup(Selection.gameObjects.Length == 0);
            if (GUILayout.Button("Unlink Selection"))
            {
                UnlinkSelectionFromSource();
            }
            EditorGUI.EndDisabledGroup();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField(
                $"{devices.Length} device(s) in scene", EditorStyles.miniBoldLabel);

            foreach (PowerDevice device in devices)
            {
                if (device == null)
                {
                    continue;
                }

                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.LabelField(device.name, GUILayout.MinWidth(130f));
                string sourceLabel = device.Source != null ? device.Source.SourceName : "NONE";
                Color previous = GUI.color;
                if (device.Source == null)
                {
                    GUI.color = new Color(1f, 0.45f, 0.45f);
                }
                EditorGUILayout.LabelField("-> " + sourceLabel, GUILayout.MinWidth(90f));
                GUI.color = previous;

                string switchLabel = device.ControllingSwitch != null
                    ? device.ControllingSwitch.SwitchName
                    : "-";
                EditorGUILayout.LabelField(switchLabel, GUILayout.MinWidth(110f));

                if (GUILayout.Button("Unlink", GUILayout.Width(60f)))
                {
                    Undo.SetCurrentGroupName("Unlink Power Device");
                    int group = Undo.GetCurrentGroup();
                    device.SetSource(null);
                    EditorUtility.SetDirty(device);
                    Undo.CollapseUndoOperations(group);
                    EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
                }
                EditorGUILayout.EndHorizontal();
            }
        }

        private void AddPowerDeviceToSelection()
        {
            Undo.SetCurrentGroupName("Add Power Device");
            int group = Undo.GetCurrentGroup();

            int count = 0;
            foreach (GameObject selected in Selection.gameObjects)
            {
                EnsurePowerDevice(selected);
                count++;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[PowerTool] PowerDevice on {count} object(s). " +
                "With no source assigned it will log a warning and stay off.");
        }

        private void LinkAllRoomLights()
        {
            PowerSource[] sources = FindAll<PowerSource>();
            PowerSource source = sources.Length > 0 ? sources[sourceIndex = Mathf.Clamp(
                sourceIndex, 0, sources.Length - 1)] : null;
            if (source == null)
            {
                Debug.LogWarning("[PowerTool] No Power Box in the scene. " +
                    "Add one first so room lights have power.");
                return;
            }

            GameObject container = GameObject.Find(LightContainerName);
            if (container == null)
            {
                Debug.LogWarning($"[PowerTool] '{LightContainerName}' not found in the scene.");
                return;
            }

            Undo.SetCurrentGroupName("Link All Room Lights");
            int group = Undo.GetCurrentGroup();

            int linked = 0;
            foreach (Transform lightTransform in container.transform)
            {
                if (lightTransform.GetComponent<Light>() == null)
                {
                    continue;
                }

                PowerDevice device = EnsurePowerDevice(lightTransform.gameObject);
                device.SetSource(source);
                EditorUtility.SetDirty(device);
                linked++;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[PowerTool] Linked {linked} room light(s) to '{source.name}'.");
        }

        private void LinkSelectionToSource(PowerSource[] sources)
        {
            if (sources.Length == 0)
            {
                return;
            }

            PowerSource source = sources[Mathf.Clamp(sourceIndex, 0, sources.Length - 1)];

            Undo.SetCurrentGroupName("Link Selection to Source");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject selected in Selection.gameObjects)
            {
                PowerDevice device = EnsurePowerDevice(selected);
                device.SetSource(source);
                EditorUtility.SetDirty(device);
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[PowerTool] Linked selection to '{source.name}'.");
        }

        private void UnlinkSelectionFromSource()
        {
            Undo.SetCurrentGroupName("Unlink Selection from Source");
            int group = Undo.GetCurrentGroup();

            foreach (GameObject selected in Selection.gameObjects)
            {
                PowerDevice device = selected.GetComponent<PowerDevice>();
                if (device != null)
                {
                    device.SetSource(null);
                    EditorUtility.SetDirty(device);
                }
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }

        // ------------------------------------------------------------- gizmos

        private void DrawGizmoSection()
        {
            EditorGUILayout.LabelField("4. Grid Gizmos", EditorStyles.boldLabel);

            bool enabled = PowerGridGizmos.Enabled;
            string label = enabled
                ? "Gizmos: ON (click to hide connection lines)"
                : "Gizmos: OFF (click to show connection lines)";
            if (GUILayout.Button(label, GUILayout.Height(26)))
            {
                PowerGridGizmos.Enabled = !enabled;
                SceneView.RepaintAll();
            }

            EditorGUILayout.LabelField(
                "Green = powered, gray = power off, orange = switch on, " +
                "red = no source.",
                EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------ helpers

        private static T[] FindAll<T>() where T : Object
        {
            return Object.FindObjectsByType<T>(FindObjectsInactive.Include,
                FindObjectsSortMode.None);
        }

        private static PowerDevice EnsurePowerDevice(GameObject target)
        {
            PowerDevice device = target.GetComponent<PowerDevice>();
            if (device == null)
            {
                device = Undo.AddComponent<PowerDevice>(target);
            }
            return device;
        }

        private static string[] NameArray(Object[] objects)
        {
            string[] names = new string[objects.Length];
            for (int i = 0; i < objects.Length; i++)
            {
                names[i] = objects[i] != null ? objects[i].name : "(missing)";
            }
            return names;
        }

        private static int DrawPopup<T>(string label, T[] objects, int index)
            where T : Object
        {
            if (objects.Length == 0)
            {
                EditorGUILayout.LabelField(label, "(none in scene)");
                return 0;
            }

            return EditorGUILayout.Popup(label, Mathf.Clamp(index, 0, objects.Length - 1),
                NameArray(objects));
        }

        private static Transform EnvironmentRoot()
        {
            return GameObject.Find(EnvironmentRootName)?.transform;
        }

        private static Transform FindDeepChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name == name)
                {
                    return child;
                }

                Transform found = FindDeepChild(child, name);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private static Transform FindDeepChildInScene(string name)
        {
            foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeepChild(rootObject.transform, name);
                if (found != null)
                {
                    return found;
                }
                if (rootObject.name == name)
                {
                    return rootObject.transform;
                }
            }
            return null;
        }

        private static List<Transform> FindAllDeepChildInScene(string namePrefix)
        {
            List<Transform> results = new List<Transform>();
            foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                CollectPrefixed(rootObject.transform, namePrefix, results);
            }
            return results;
        }

        private static void CollectPrefixed(Transform parent, string namePrefix,
            List<Transform> results)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                Transform child = parent.GetChild(i);
                if (child.name.StartsWith(namePrefix))
                {
                    results.Add(child);
                }
                CollectPrefixed(child, namePrefix, results);
            }
        }

        private static Material EnsureLeverMaterial()
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(
                SwitchLeverMaterialPath);
            bool created = false;

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                if (shader == null)
                {
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, SwitchLeverMaterialPath);
                created = true;
            }

            // Faint amber glow so the lever stays findable in a blackout.
            Color baseColor = new Color(0.9f, 0.75f, 0.5f, 1f);
            Color emission = new Color(1f, 0.62f, 0.22f, 1f) * 1.4f;
            material.color = baseColor;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", baseColor);
            }
            material.EnableKeyword("_EMISSION");
            if (material.HasProperty("_EmissionColor"))
            {
                material.SetColor("_EmissionColor", emission);
            }
            material.globalIlluminationFlags =
                MaterialGlobalIlluminationFlags.RealtimeEmissive;

            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[PowerTool] Created Graybox_SwitchLever.mat " +
                    "(emissive).");
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
            return material;
        }

        private static Material EnsureMaterial(string path, Color color, float smoothness)
        {
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = false;

            if (material == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null)
                {
                    shader = Shader.Find("Standard");
                }
                if (shader == null)
                {
                    return null;
                }

                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
                created = true;
            }

            material.color = color;
            if (material.HasProperty("_BaseColor"))
            {
                material.SetColor("_BaseColor", color);
            }
            if (material.HasProperty("_Smoothness"))
            {
                material.SetFloat("_Smoothness", smoothness);
            }

            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[PowerTool] Created {System.IO.Path.GetFileName(path)}.");
            }
            else
            {
                EditorUtility.SetDirty(material);
            }
            return material;
        }
    }
}
