using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Applies day, dusk, night, or blackout moods, places interior lights, and sets up the
    /// camera flashlight. Access via menu: Tools > Hollow Creek > Lighting &amp; Atmosphere...
    /// Mood buttons are one-click and reversible so each map can pick its own mood.
    /// Blackout = only house lights + flashlight illuminate the scene.
    /// </summary>
    public class LightingAtmosphereTool : EditorWindow
    {
        private enum Mood
        {
            Day,
            Dusk,
            Night,
            Blackout
        }

        private const string DuskSkyboxPath = "Assets/Materials/Sky_Dusk.mat";
        private const string NightSkyboxPath = "Assets/Materials/Sky_Night.mat";
        private const string ProfilePath = "Assets/Settings/SampleSceneProfile.asset";
        private const string InputActionsPath = "Assets/InputSystem_Actions.inputactions";

        private static readonly Color DuskSunColor = new Color(0.55f, 0.62f, 0.85f, 1f);
        private static readonly Color DuskFogColor = new Color(0.08f, 0.09f, 0.13f, 1f);
        private static readonly Color NightSunColor = new Color(0.4f, 0.5f, 0.85f, 1f);
        private static readonly Color NightFogColor = new Color(0.015f, 0.02f, 0.04f, 1f);
        private static readonly Color NightAmbientColor = new Color(0.012f, 0.015f, 0.028f, 1f);
        private static readonly Color WarmLightColor = new Color(1f, 0.88f, 0.7f, 1f);

        // Ceiling sits at y=3.46 inside the house; lights hang just below it.
        // Interior is x 10.5..25.5, z -90.6..-102.5: Wall_Indoor_01 (z=-97.33)
        // splits the right strip into front/back bedrooms, Wall_Indoor_02 (x=16.4)
        // leaves a hallway down the middle - so four rooms, one light each.
        private static readonly (string Name, Vector3 Position)[] RoomLights =
        {
            ("Light_LivingRoom", new Vector3(13.4f, 3.4f, -96.5f)),
            ("Light_Bedroom_Front", new Vector3(22.5f, 3.4f, -94f)),
            ("Light_Bedroom_Back", new Vector3(22.5f, 3.4f, -99.9f)),
            ("Light_Hallway", new Vector3(18f, 3.4f, -96.5f))
        };

        private static readonly string[] LegacyLightNames =
        {
            "Light_MainRoom", "Light_Bedroom", "Light_Custom"
        };

        [MenuItem("Tools/Hollow Creek/Lighting & Atmosphere...")]
        private static void Open()
        {
            LightingAtmosphereTool window = GetWindow<LightingAtmosphereTool>("Lighting");
            window.minSize = new Vector2(320, 300);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "One-click moods, interior lights, and flashlight setup. " +
                "Day restores the original daylight look exactly.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. Mood", EditorStyles.boldLabel);
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Day (Original)", GUILayout.Height(30)))
            {
                ApplyMood(Mood.Day);
            }
            if (GUILayout.Button("Dusk", GUILayout.Height(30)))
            {
                ApplyMood(Mood.Dusk);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Dark Night", GUILayout.Height(30)))
            {
                ApplyMood(Mood.Night);
            }
            if (GUILayout.Button("Blackout", GUILayout.Height(30)))
            {
                ApplyMood(Mood.Blackout);
            }
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.LabelField(
                "Night = forest-at-night dark: near-black sky, faint moonlight, flashlight needed.",
                EditorStyles.miniLabel);
            EditorGUILayout.LabelField(
                "Blackout = only house lights + flashlight; no moon, no ambient, no reflections.",
                EditorStyles.miniLabel);

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. Interior Lights", EditorStyles.boldLabel);
            if (GUILayout.Button("Setup Room Lights (4 Rooms)", GUILayout.Height(24)))
            {
                AddDefaultRoomLights();
            }
            if (GUILayout.Button("Add Light At Selection / Scene Pivot", GUILayout.Height(24)))
            {
                AddLightAtPivot();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("3. Flashlight", EditorStyles.boldLabel);
            if (GUILayout.Button("Setup Flashlight (Toggle: T)", GUILayout.Height(24)))
            {
                SetupFlashlight();
            }

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField(
                "In play mode press F1 for a menu that turns every light on/off.",
                EditorStyles.miniLabel);
        }

        // ------------------------------------------------------------- Mood

        private void ApplyMood(Mood mood)
        {
            Undo.SetCurrentGroupName($"Apply {mood} Mood");
            int group = Undo.GetCurrentGroup();

            ApplyDirectionalLight(mood);
            ApplySkybox(mood);
            ApplyFog(mood);
            ApplyVolume(mood);
            ApplyCameraPostProcessing();

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[Lighting] {mood} mood applied.");
        }

        private void ApplyDirectionalLight(Mood mood)
        {
            Light sun = FindDirectionalLight();
            if (sun == null)
            {
                Debug.LogWarning("[Lighting] No directional light found in the scene.");
                return;
            }

            Undo.RecordObject(sun, "Change Directional Light");
            Undo.RecordObject(sun.transform, "Change Directional Light");

            switch (mood)
            {
                case Mood.Blackout:
                    // No directional contribution at all: only house lights and
                    // the flashlight illuminate surfaces.
                    sun.intensity = 0f;
                    sun.color = NightSunColor;
                    sun.useColorTemperature = false;
                    sun.transform.rotation = Quaternion.Euler(35f, -140f, 0f);
                    break;

                case Mood.Night:
                    // Faint cool moonlight - just enough to read silhouettes outdoors.
                    sun.intensity = 0.05f;
                    sun.color = NightSunColor;
                    sun.useColorTemperature = false;
                    sun.transform.rotation = Quaternion.Euler(35f, -140f, 0f);
                    break;

                case Mood.Dusk:
                    sun.intensity = 0.5f;
                    sun.color = DuskSunColor;
                    sun.useColorTemperature = false;
                    sun.transform.rotation = Quaternion.Euler(15f, -30f, 0f);
                    break;

                default:
                    sun.intensity = 2f;
                    sun.color = Color.white;
                    sun.useColorTemperature = true;
                    sun.colorTemperature = 5000f;
                    sun.transform.rotation = Quaternion.Euler(50f, -30f, 0f);
                    break;
            }
        }

        private void ApplySkybox(Mood mood)
        {
            switch (mood)
            {
                case Mood.Blackout:
                    RenderSettings.skybox = EnsureNightSkybox();
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = Color.black;
                    RenderSettings.ambientIntensity = 1f;
                    // No skybox specular sheen either - lights in the scene only.
                    RenderSettings.reflectionIntensity = 0f;
                    break;

                case Mood.Night:
                    RenderSettings.skybox = EnsureNightSkybox();
                    // Flat ambient with explicit near-black colors so the result never
                    // depends on a stale ambient probe regenerated from an older sky.
                    RenderSettings.ambientMode = AmbientMode.Flat;
                    RenderSettings.ambientLight = NightAmbientColor;
                    RenderSettings.ambientIntensity = 1f;
                    RenderSettings.reflectionIntensity = 1f;
                    break;

                case Mood.Dusk:
                    RenderSettings.skybox = EnsureDuskSkybox();
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = 0.6f;
                    RenderSettings.reflectionIntensity = 1f;
                    break;

                default:
                    RenderSettings.skybox =
                        AssetDatabase.GetBuiltinExtraResource<Material>("Default-Skybox.mat");
                    RenderSettings.ambientMode = AmbientMode.Skybox;
                    RenderSettings.ambientIntensity = 1f;
                    RenderSettings.reflectionIntensity = 1f;
                    break;
            }
            DynamicGI.UpdateEnvironment();
        }

        private void ApplyFog(Mood mood)
        {
            switch (mood)
            {
                case Mood.Night:
                case Mood.Blackout:
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Exponential;
                    RenderSettings.fogDensity = 0.04f;
                    RenderSettings.fogColor = NightFogColor;
                    break;

                case Mood.Dusk:
                    RenderSettings.fog = true;
                    RenderSettings.fogMode = FogMode.Exponential;
                    RenderSettings.fogDensity = 0.01f;
                    RenderSettings.fogColor = DuskFogColor;
                    break;

                default:
                    RenderSettings.fog = false;
                    RenderSettings.fogColor = new Color(0.5f, 0.5f, 0.5f, 1f);
                    break;
            }
        }

        /// <summary>
        /// The camera must have post processing enabled or the volume profile
        /// (exposure, white balance, vignette, tonemapping) never renders.
        /// </summary>
        private void ApplyCameraPostProcessing()
        {
            Camera camera = FindPlayerCamera();
            if (camera == null)
            {
                Debug.LogWarning("[Lighting] No camera found; cannot enable post processing.");
                return;
            }

            UniversalAdditionalCameraData cameraData =
                camera.GetUniversalAdditionalCameraData();
            if (cameraData.renderPostProcessing)
            {
                return;
            }

            Undo.RecordObject(cameraData, "Enable Camera Post Processing");
            cameraData.renderPostProcessing = true;
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Lighting] Enabled post processing on camera '" + camera.name + "'.");
        }

        private void ApplyVolume(Mood mood)
        {
            VolumeProfile profile = LoadProfile();
            if (profile == null)
            {
                Debug.LogWarning("[Lighting] Volume profile not found at " + ProfilePath);
                return;
            }

            Undo.RecordObject(profile, "Change Volume Profile");

            float temperature;
            float saturation;
            float contrast;
            float postExposure;
            float vignetteIntensity;

            switch (mood)
            {
                case Mood.Night:
                case Mood.Blackout:
                    temperature = -40f;
                    saturation = -25f;
                    contrast = 15f;
                    postExposure = -1.5f; // 1.5 stops darker overall - pitch night
                    vignetteIntensity = 0.5f;
                    break;

                case Mood.Dusk:
                    temperature = -20f;
                    saturation = -5f;
                    contrast = 8f;
                    postExposure = 0f;
                    vignetteIntensity = 0.35f;
                    break;

                default:
                    temperature = 0f;
                    saturation = 0f;
                    contrast = 0f;
                    postExposure = 0f;
                    vignetteIntensity = 0.2f;
                    break;
            }

            if (mood != Mood.Day)
            {
                WhiteBalance whiteBalance = EnsureComponent<WhiteBalance>(profile);
                whiteBalance.temperature.value = temperature;
                whiteBalance.temperature.overrideState = true;
                whiteBalance.tint.value = 0f;
                whiteBalance.tint.overrideState = true;

                ColorAdjustments colorAdjustments = EnsureComponent<ColorAdjustments>(profile);
                colorAdjustments.saturation.value = saturation;
                colorAdjustments.saturation.overrideState = true;
                colorAdjustments.contrast.value = contrast;
                colorAdjustments.contrast.overrideState = true;
                colorAdjustments.postExposure.value = postExposure;
                colorAdjustments.postExposure.overrideState = true;
            }
            else
            {
                // Neutralize instead of removing so the profile stays simple.
                if (profile.TryGet<WhiteBalance>(out WhiteBalance whiteBalance))
                {
                    whiteBalance.temperature.value = 0f;
                    whiteBalance.tint.value = 0f;
                }
                if (profile.TryGet<ColorAdjustments>(out ColorAdjustments colorAdjustments))
                {
                    colorAdjustments.saturation.value = 0f;
                    colorAdjustments.contrast.value = 0f;
                    colorAdjustments.postExposure.value = 0f;
                }
            }

            if (profile.TryGet<Vignette>(out Vignette vignette))
            {
                vignette.intensity.value = vignetteIntensity;
                vignette.intensity.overrideState = true;
            }

            EditorUtility.SetDirty(profile);
            AssetDatabase.SaveAssets();
        }

        /// <summary>
        /// Gets the component from the profile, adding it (and registering it as a
        /// sub-asset so it persists) if it doesn't exist yet.
        /// </summary>
        private static T EnsureComponent<T>(VolumeProfile profile) where T : VolumeComponent
        {
            if (profile.TryGet<T>(out T existing))
            {
                return existing;
            }

            T component = profile.Add<T>(overrides: true);
            Undo.RegisterCreatedObjectUndo(component, "Add Volume Override");
            if (EditorUtility.IsPersistent(profile))
            {
                AssetDatabase.AddObjectToAsset(component, profile);
            }
            return component;
        }

        private static VolumeProfile LoadProfile()
        {
            Volume volume = FindFirstObjectByType<Volume>();
            if (volume != null && volume.sharedProfile != null)
            {
                return volume.sharedProfile;
            }
            return AssetDatabase.LoadAssetAtPath<VolumeProfile>(ProfilePath);
        }

        private Material EnsureDuskSkybox()
        {
            return EnsureSkybox(DuskSkyboxPath, mat =>
            {
                mat.SetFloat("_Exposure", 0.15f);
                mat.SetFloat("_AtmosphereThickness", 1.2f);
                mat.SetColor("_SkyTint", new Color(0.35f, 0.4f, 0.5f, 1f));
                mat.SetColor("_GroundColor", new Color(0.1f, 0.1f, 0.1f, 1f));
                mat.SetFloat("_SunSize", 0.05f);
            });
        }

        private Material EnsureNightSkybox()
        {
            return EnsureSkybox(NightSkyboxPath, mat =>
            {
                mat.SetFloat("_Exposure", 0.02f);
                mat.SetFloat("_AtmosphereThickness", 0.8f);
                mat.SetColor("_SkyTint", new Color(0.2f, 0.25f, 0.4f, 1f));
                mat.SetColor("_GroundColor", new Color(0.02f, 0.02f, 0.03f, 1f));
                mat.SetFloat("_SunSize", 0.03f);
            });
        }

        private static Material EnsureSkybox(string path, System.Action<Material> configure)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (mat != null)
            {
                return mat;
            }

            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null)
            {
                Debug.LogWarning("[Lighting] Skybox/Procedural shader not found; keeping current skybox.");
                return RenderSettings.skybox;
            }

            mat = new Material(shader);
            configure(mat);

            AssetDatabase.CreateAsset(mat, path);
            AssetDatabase.SaveAssets();
            Debug.Log($"[Lighting] Created {System.IO.Path.GetFileName(path)}.");
            return mat;
        }

        // ------------------------------------------------------- Interior lights

        private void AddDefaultRoomLights()
        {
            Undo.SetCurrentGroupName("Add Default Room Lights");
            int group = Undo.GetCurrentGroup();

            Transform parent = EnsureLightContainer();
            RemoveDuplicateLights(parent);

            int createdCount = 0;
            foreach ((string name, Vector3 position) in RoomLights)
            {
                if (CreatePointLight(name, position, parent))
                {
                    createdCount++;
                }
            }
            int removedCount = RemoveLegacyLights(parent);

            Undo.CollapseUndoOperations(group);
            if (createdCount > 0 || removedCount > 0)
            {
                EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            }
            Debug.Log($"[Lighting] Room lights: {RoomLights.Length} in layout " +
                $"({createdCount} new), {removedCount} legacy removed.");
        }

        private void AddLightAtPivot()
        {
            Vector3 position;
            if (Selection.activeTransform != null)
            {
                position = Selection.activeTransform.position;
            }
            else if (SceneView.lastActiveSceneView != null)
            {
                position = SceneView.lastActiveSceneView.pivot;
            }
            else
            {
                position = Vector3.zero;
            }

            Undo.SetCurrentGroupName("Add Light");
            int group = Undo.GetCurrentGroup();
            Transform parent = EnsureLightContainer();
            CreatePointLight(NextCustomLightName(parent), position, parent);
            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
        }
        private Transform EnsureLightContainer()
        {
            GameObject container = GameObject.Find("Interior Lights");
            if (container == null)
            {
                container = new GameObject("Interior Lights");
                Undo.RegisterCreatedObjectUndo(container, "Create Light Container");
            }
            return container.transform;
        }

        /// <summary>
        /// Deletes extra lights that share a name with an earlier sibling, which
        /// happens when the add button is clicked more than once.
        /// </summary>
        private static void RemoveDuplicateLights(Transform parent)
        {
            HashSet<string> seen = new HashSet<string>();
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.GetComponent<Light>() == null)
                {
                    continue;
                }
                if (!seen.Add(child.name))
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }
        }

        /// <summary>
        /// Deletes lights from the old two-light layout and the stray Light_Custom
        /// so the button fully replaces them with the per-room layout. Returns the
        /// number removed.
        /// </summary>
        private static int RemoveLegacyLights(Transform parent)
        {
            int removed = 0;
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                Transform child = parent.GetChild(i);
                if (child.GetComponent<Light>() == null)
                {
                    continue;
                }
                foreach (string legacyName in LegacyLightNames)
                {
                    if (child.name == legacyName)
                    {
                        Debug.Log($"[Lighting] Removed legacy light '{legacyName}'.");
                        Undo.DestroyObjectImmediate(child.gameObject);
                        removed++;
                        break;
                    }
                }
            }
            return removed;
        }

        /// <summary>
        /// "Light_Custom 1", "Light_Custom 2", ... so each click adds a light
        /// instead of moving the previous one. The bare name "Light_Custom" is
        /// reserved for the legacy stray, which AddDefaultRoomLights removes.
        /// </summary>
        private static string NextCustomLightName(Transform parent)
        {
            for (int i = 1; ; i++)
            {
                string candidate = $"Light_Custom {i}";
                if (parent.Find(candidate) == null)
                {
                    return candidate;
                }
            }
        }

        /// <summary>
        /// Creates a point light, or updates the existing one, so the button is a
        /// one-click "ensure correct state" action. Returns true if a light was created.
        /// </summary>
        private static bool CreatePointLight(string name, Vector3 position, Transform parent)
        {
            Transform existing = parent.Find(name);
            if (existing != null)
            {
                Light existingLight = existing.GetComponent<Light>();
                if (existingLight != null)
                {
                    Undo.RecordObject(existingLight, "Update Light");
                    Undo.RecordObject(existing.transform, "Update Light");
                    existing.position = position;
                    existingLight.color = WarmLightColor;
                    existingLight.intensity = 2f;
                    existingLight.range = 10f;
                    existingLight.shadows = LightShadows.Soft;
                    ApplyShadowResolutionTier(existingLight);
                    return false;
                }
            }

            GameObject lightObject = new GameObject(name);
            Undo.RegisterCreatedObjectUndo(lightObject, "Add Light");

            lightObject.transform.SetParent(parent, true);
            lightObject.transform.position = position;

            Light light = lightObject.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = WarmLightColor;
            light.intensity = 2f;
            light.range = 10f;
            light.shadows = LightShadows.Soft;
            ApplyShadowResolutionTier(light);
            return true;
        }

        /// <summary>
        /// Sets the shadow resolution tier. URP ignores Light.shadowResolution and reads
        /// this tier from UniversalAdditionalLightData instead; the default High tier (2)
        /// requests 1024px per face, but four point lights need 24 shadow slices and only
        /// 16 fit at 1024px in the 4096 atlas, so URP rescales everything to 512 anyway
        /// and prints "Reduced additional punctual light shadows resolution..." -
        /// requesting the real size directly gives the same image with no message.
        /// Tiers: 0 = Low (256px), 1 = Medium (512px), 2 = High (1024px).
        /// </summary>
        internal static void ApplyShadowResolutionTier(Light light, int tier = 1)
        {
            UniversalAdditionalLightData data =
                light.GetComponent<UniversalAdditionalLightData>();
            if (data == null)
            {
                data = Undo.AddComponent<UniversalAdditionalLightData>(light.gameObject);
            }

            SerializedObject serialized = new SerializedObject(data);
            SerializedProperty tierProperty = serialized.FindProperty("m_AdditionalLightsShadowResolutionTier");
            if (tierProperty != null && tierProperty.intValue != tier)
            {
                tierProperty.intValue = tier;
                serialized.ApplyModifiedProperties();
            }
        }

        // ----------------------------------------------------------- Flashlight

        private void SetupFlashlight()
        {
            Camera camera = FindPlayerCamera();
            if (camera == null)
            {
                Debug.LogWarning("[Lighting] No camera found in the scene.");
                return;
            }

            Transform cameraT = camera.transform;

            Undo.SetCurrentGroupName("Setup Flashlight");
            int group = Undo.GetCurrentGroup();

            // Replace any previous flashlight so re-running the button always
            // applies the current settings. Flicker/thermal toggles the user set
            // in the Inspector are carried over to the new component.
            bool hadOld = false;
            bool keepFlicker = true;
            bool keepThermal = true;
            float keepFadeSpeed = 8f;

            Transform existing = cameraT.Find("Flashlight");
            if (existing != null)
            {
                HollowCreek.Player.Flashlight oldFlashlight =
                    existing.GetComponent<HollowCreek.Player.Flashlight>();
                if (oldFlashlight != null)
                {
                    hadOld = true;
                    keepFlicker = oldFlashlight.FlickerEnabled;
                    keepThermal = oldFlashlight.ThermalEnabled;
                    keepFadeSpeed = oldFlashlight.FadeSpeed;
                }
                Undo.DestroyObjectImmediate(existing.gameObject);
            }

            GameObject flashlightObject = new GameObject("Flashlight");
            Undo.RegisterCreatedObjectUndo(flashlightObject, "Setup Flashlight");
            flashlightObject.transform.SetParent(cameraT, false);
            flashlightObject.transform.localPosition = new Vector3(0.18f, -0.18f, 0.1f);

            Light light = flashlightObject.AddComponent<Light>();
            light.type = LightType.Spot;
            light.color = new Color(1f, 0.95f, 0.85f, 1f);
            light.intensity = 10f;
            light.range = 25f;
            light.spotAngle = 45f;
            light.innerSpotAngle = 21.8f;
            light.shadows = LightShadows.Soft;
            ApplyShadowResolutionTier(light);

            HollowCreek.Player.Flashlight flashlight =
                flashlightObject.AddComponent<HollowCreek.Player.Flashlight>();
            if (hadOld)
            {
                flashlight.FlickerEnabled = keepFlicker;
                flashlight.ThermalEnabled = keepThermal;
                flashlight.FadeSpeed = keepFadeSpeed;
            }

            InputActionAsset inputAsset =
                AssetDatabase.LoadAssetAtPath<InputActionAsset>(InputActionsPath);
            if (inputAsset != null)
            {
                SerializedObject serialized = new SerializedObject(flashlight);
                serialized.FindProperty("inputActions").objectReferenceValue = inputAsset;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[Lighting] Input actions not found at " + InputActionsPath +
                    "; the flashlight will fall back to a code-created T binding.");
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[Lighting] Flashlight set up under '" + camera.name +
                "' (intensity 10, range 25). Press T in play mode.");
        }

        // -------------------------------------------------------------- Helpers

        private static Light FindDirectionalLight()
        {
            foreach (Light light in FindObjectsByType<Light>(FindObjectsSortMode.None))
            {
                if (light.type == LightType.Directional)
                {
                    return light;
                }
            }
            return null;
        }

        private static Camera FindPlayerCamera()
        {
            Camera[] cameras = FindObjectsByType<Camera>(FindObjectsSortMode.None);
            foreach (Camera camera in cameras)
            {
                Transform parent = camera.transform.parent;
                while (parent != null)
                {
                    if (parent.name == "Player")
                    {
                        return camera;
                    }
                    parent = parent.parent;
                }
            }
            return cameras.Length > 0 ? cameras[0] : null;
        }
    }
}
