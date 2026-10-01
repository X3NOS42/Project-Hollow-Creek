using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HollowCreek.Environment;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Builds a small graybox picket fence near the house.
    /// Access via menu: Tools > Hollow Creek > Add Fence...
    /// Set position/size, click "Add Fence". Replaces the previous fence so
    /// tweaks (length, rotation, spot) are one click.
    /// </summary>
    public class AddFenceTool : EditorWindow
    {
        private const string FenceMaterialPath = "Assets/Materials/Graybox_Fence.mat";
        private const string WaxMaterialPath = "Assets/Materials/Graybox_Candle_Wax.mat";
        private const string FlameMaterialPath = "Assets/Materials/Graybox_Candle_Flame.mat";
        private const string EnvironmentRootName = "--- Environment ---";
        private const string FenceRootName = "Fence";

        // Fixed design proportions (fractions of height / absolute meters).
        private const float PicketWidth = 0.12f;
        private const float PicketDepth = 0.04f;
        private const float PostSize = 0.12f;
        private const float PostExtraHeight = 0.08f;
        private const float RailThickness = 0.07f;
        private const float RailDepth = 0.05f;
        private const float PicketTargetSpacing = 0.3f;

        private Vector3 fencePosition = new Vector3(12f, 0f, -87f);
        private float rotationY;
        private float length = 5f;
        private float height = 1f;
        private bool candleEmitsLight = true;

        [MenuItem("Tools/Hollow Creek/Add Fence...")]
        private static void Open()
        {
            AddFenceTool window = GetWindow<AddFenceTool>("Add Fence");
            window.minSize = new Vector2(320, 240);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Builds a small graybox picket fence with a candle on each post. " +
                "\"Add Fence\" replaces the previous fence; \"Add Candles\" works on "
                + "existing/duplicated fences too.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Placement", EditorStyles.boldLabel);
            fencePosition = EditorGUILayout.Vector3Field("Position (base at ground)", fencePosition);
            rotationY = EditorGUILayout.FloatField("Rotation (Y)", rotationY);

            EditorGUILayout.Space(6);
            EditorGUILayout.LabelField("Size", EditorStyles.boldLabel);
            length = EditorGUILayout.FloatField("Length (m)", length);
            height = EditorGUILayout.FloatField("Height (m)", height);

            EditorGUILayout.Space(4);
            candleEmitsLight = EditorGUILayout.Toggle(
                "Candle emits light", candleEmitsLight);

            EditorGUILayout.Space(10);
            if (GUILayout.Button("Add Fence (with candles)", GUILayout.Height(28)))
            {
                BuildFence();
            }
            if (GUILayout.Button("Add Candles To All Fence Posts", GUILayout.Height(24)))
            {
                AddCandlesToAllFences();
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField(
                "Entrance is the +z wall (door gap x 16.5-19.5, stairs at x~18).",
                EditorStyles.miniLabel);
        }

        private void BuildFence()
        {
            length = Mathf.Max(1f, length);
            height = Mathf.Clamp(height, 0.4f, 3f);

            Material mat = EnsureFenceMaterial();
            if (mat == null)
            {
                Debug.LogWarning("[AddFenceTool] Could not load or create fence material. Aborting.");
                return;
            }

            Undo.SetCurrentGroupName("Add Fence");
            int group = Undo.GetCurrentGroup();

            RemoveExistingFence();

            GameObject root = new GameObject(FenceRootName);
            Transform parent = GameObject.Find(EnvironmentRootName)?.transform;
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }
            Undo.RegisterCreatedObjectUndo(root, "Add Fence");

            root.transform.SetPositionAndRotation(fencePosition, Quaternion.Euler(0f, rotationY, 0f));

            Quaternion rot = root.transform.rotation;
            Transform t = root.transform;
            float half = length * 0.5f;

            // End posts - slightly taller than the pickets.
            float postH = height + PostExtraHeight;
            CreateBox("Post Left", t, t.TransformPoint(new Vector3(-half, postH * 0.5f, 0f)),
                new Vector3(PostSize, postH, PostSize), rot, mat);
            CreateBox("Post Right", t, t.TransformPoint(new Vector3(half, postH * 0.5f, 0f)),
                new Vector3(PostSize, postH, PostSize), rot, mat);

            // Two horizontal rails behind the pickets, spanning post to post.
            float railY1 = height * 0.4f;
            float railY2 = height * 0.82f;
            CreateBox("Rail Lower", t, t.TransformPoint(new Vector3(0f, railY1, -0.045f)),
                new Vector3(length, RailThickness, RailDepth), rot, mat);
            CreateBox("Rail Upper", t, t.TransformPoint(new Vector3(0f, railY2, -0.045f)),
                new Vector3(length, RailThickness, RailDepth), rot, mat);

            // Pickets evenly spaced between the posts.
            float span = length - (PostSize + PicketWidth);
            int count = Mathf.Max(1, Mathf.RoundToInt(span / PicketTargetSpacing));
            float step = count > 1 ? span / count : 0f;
            for (int i = 0; i <= count; i++)
            {
                float x = -half + PostSize * 0.5f + PicketWidth * 0.5f + i * step;
                CreateBox($"Picket {i + 1}", t, t.TransformPoint(new Vector3(x, height * 0.5f, 0f)),
                    new Vector3(PicketWidth, height, PicketDepth), rot, mat);
            }

            AddCandles(t, candleEmitsLight);

            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[AddFenceTool] Added fence at {fencePosition} (length {length:F1}, height {height:F1}).");
        }

        private void AddCandlesToAllFences()
        {
            Undo.SetCurrentGroupName("Add Candles To Fence Posts");
            int group = Undo.GetCurrentGroup();

            List<Transform> fences = FindAllFenceRoots();
            if (fences.Count == 0)
            {
                Debug.LogWarning("[AddFenceTool] No fence found in the scene.");
                return;
            }

            int candles = 0;
            foreach (Transform fence in fences)
            {
                candles += AddCandles(fence, candleEmitsLight);
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log($"[AddFenceTool] Added/refreshed candles on {candles} posts across {fences.Count} fence(s).");
        }

        /// <summary>
        /// Puts a small candle (wax + emissive flame + optional point light) on top
        /// of "Post Left" and "Post Right". Existing candles are replaced so the
        /// button can be clicked again after changing settings.
        /// </summary>
        private int AddCandles(Transform fenceRoot, bool emitLight)
        {
            Material wax = EnsureMaterial(WaxMaterialPath, new Color(0.93f, 0.9f, 0.82f, 1f), 0.3f, null);
            Material flame = EnsureMaterial(FlameMaterialPath, new Color(1f, 0.6f, 0.25f, 1f), 0f,
                new Color(3f, 1.4f, 0.5f, 1f));
            if (wax == null || flame == null)
            {
                Debug.LogWarning("[AddFenceTool] Could not load candle materials. Skipping candles.");
                return 0;
            }

            // Replace candles from a previous run.
            for (int i = fenceRoot.childCount - 1; i >= 0; i--)
            {
                string childName = fenceRoot.GetChild(i).name;
                if (childName == "Candle Left" || childName == "Candle Right")
                {
                    Undo.DestroyObjectImmediate(fenceRoot.GetChild(i).gameObject);
                }
            }

            int added = 0;
            foreach (string postName in new[] { "Post Left", "Post Right" })
            {
                Transform post = FindDirectChild(fenceRoot, postName);
                if (post == null)
                {
                    continue;
                }

                float postTop = post.localPosition.y + post.localScale.y * 0.5f;
                string side = postName == "Post Left" ? "Left" : "Right";

                GameObject candle = new GameObject($"Candle {side}");
                candle.transform.SetParent(fenceRoot, false);
                candle.transform.localPosition = new Vector3(post.localPosition.x, postTop, post.localPosition.z);
                Undo.RegisterCreatedObjectUndo(candle, "Add Candles To Fence Posts");

                // Wax body: ~5.5cm wide, 7cm tall.
                GameObject waxGo = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                waxGo.name = "Wax";
                waxGo.transform.SetParent(candle.transform, false);
                waxGo.transform.localPosition = new Vector3(0f, 0.035f, 0f);
                waxGo.transform.localScale = new Vector3(0.055f, 0.035f, 0.055f);
                waxGo.GetComponent<Renderer>().material = wax;
                Object.DestroyImmediate(waxGo.GetComponent<Collider>());
                Undo.RegisterCreatedObjectUndo(waxGo, "Add Candles To Fence Posts");

                // Small emissive flame sphere sitting on the wax.
                GameObject flameGo = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                flameGo.name = "Flame";
                flameGo.transform.SetParent(candle.transform, false);
                flameGo.transform.localPosition = new Vector3(0f, 0.084f, 0f);
                flameGo.transform.localScale = Vector3.one * 0.03f;
                flameGo.GetComponent<Renderer>().material = flame;
                Object.DestroyImmediate(flameGo.GetComponent<Collider>());
                Undo.RegisterCreatedObjectUndo(flameGo, "Add Candles To Fence Posts");

                if (emitLight)
                {
                    GameObject lightGo = new GameObject("Candle Light");
                    lightGo.transform.SetParent(candle.transform, false);
                    lightGo.transform.localPosition = new Vector3(0f, 0.09f, 0f);
                    Undo.RegisterCreatedObjectUndo(lightGo, "Add Candles To Fence Posts");

                    Light light = lightGo.AddComponent<Light>();
                    light.type = LightType.Point;
                    light.range = 3f;
                    light.intensity = 1.4f;
                    light.color = new Color(1f, 0.75f, 0.45f);
                    light.shadows = LightShadows.Hard;
                    light.useColorTemperature = false;

                    // Low tier (256px per face): the range is only 3m, so tiny
                    // slices look identical and 24 candle faces never crowd the
                    // 4096 atlas next to the room lights.
                    LightingAtmosphereTool.ApplyShadowResolutionTier(light, 0);

                    CandleFlicker flicker = lightGo.AddComponent<CandleFlicker>();
                    flicker.flame = flameGo.transform;
                }

                added++;
            }

            return added;
        }

        private static Transform FindDirectChild(Transform parent, string name)
        {
            for (int i = 0; i < parent.childCount; i++)
            {
                if (parent.GetChild(i).name == name)
                {
                    return parent.GetChild(i);
                }
            }
            return null;
        }

        /// <summary>Finds every fence root, including Unity duplicates like "Fence (1)".</summary>
        private static List<Transform> FindAllFenceRoots()
        {
            List<Transform> fences = new List<Transform>();
            foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                CollectFences(rootObject.transform, fences);
            }
            return fences;
        }

        private static void CollectFences(Transform current, List<Transform> results)
        {
            if (current.name == "Fence" || current.name.StartsWith("Fence ("))
            {
                results.Add(current);
            }
            for (int i = 0; i < current.childCount; i++)
            {
                CollectFences(current.GetChild(i), results);
            }
        }

        private static GameObject CreateBox(string name, Transform parent, Vector3 worldPos,
            Vector3 size, Quaternion rotation, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;

            cube.transform.SetParent(parent, true);
            cube.transform.rotation = rotation;
            cube.transform.localScale = size;
            cube.transform.position = worldPos;

            cube.GetComponent<Renderer>().material = material;
            Undo.RegisterCreatedObjectUndo(cube, "Add Fence");
            return cube;
        }

        /// <summary>Removes the fence root (and children) if the tool ran before.</summary>
        private static void RemoveExistingFence()
        {
            foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform existing = FindDeepChild(rootObject.transform, FenceRootName);
                if (existing != null)
                {
                    Undo.DestroyObjectImmediate(existing.gameObject);
                    Debug.Log("[AddFenceTool] Replaced previous fence.");
                    return;
                }
            }
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

        private static Material EnsureFenceMaterial()
        {
            return EnsureMaterial(FenceMaterialPath, new Color(0.52f, 0.4f, 0.3f, 1f), 0.15f, null);
        }

        /// <summary>
        /// Loads or creates a URP Lit material, then applies color/smoothness
        /// (and an HDR emission color when given, e.g. for candle flames).
        /// </summary>
        private static Material EnsureMaterial(string path, Color color, float smoothness, Color? emission)
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(path);
            bool created = false;

            if (mat == null)
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

                mat = new Material(shader);
                AssetDatabase.CreateAsset(mat, path);
                created = true;
            }

            mat.color = color;
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", color);
            }
            if (mat.HasProperty("_Smoothness"))
            {
                mat.SetFloat("_Smoothness", smoothness);
            }

            if (emission.HasValue)
            {
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission.Value);
            }

            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[AddFenceTool] Created {System.IO.Path.GetFileName(path)}.");
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
