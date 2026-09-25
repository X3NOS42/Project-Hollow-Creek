using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.Rendering;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Builds graybox windows onto a selected wall for prototyping.
    /// Access via menu: Tools > Hollow Creek > Add Window...
    /// "Add Visual Window" adds a decorative frame + glass pane proud of the wall face.
    /// "Add Window Opening" replaces the solid wall with slabs around a real hole,
    /// then places frame + glass inside it so both sides are visible and see-through.
    /// </summary>
    public class AddWindowTool : EditorWindow
    {
        private const string WallMaterialPath = "Assets/Materials/Graybox_Wall.mat";
        private const string GlassMaterialPath = "Assets/Materials/Graybox_Glass.mat";

        private float windowWidth = 1.5f;
        private float windowHeight = 1.1f;
        private float sillHeight = 0.6f;
        private float frameThickness = 0.1f;
        private bool flipSide;

        [MenuItem("Tools/Hollow Creek/Add Window...")]
        private static void Open()
        {
            AddWindowTool window = GetWindow<AddWindowTool>("Add Window");
            window.minSize = new Vector2(340, 300);
        }

        private void OnGUI()
        {
            GameObject wall = Selection.activeGameObject;

            EditorGUILayout.HelpBox(
                "Select a wall (a cube), then add a prototype window to it. " +
                "\"Add Window Opening\" replaces the solid wall with slabs and a window hole.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Target", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                wall != null ? wall.name : "Nothing selected",
                wall != null ? EditorStyles.boldLabel : EditorStyles.miniLabel);

            EditorGUILayout.Space(8);
            EditorGUILayout.LabelField("Window size", EditorStyles.boldLabel);
            windowWidth = EditorGUILayout.FloatField("Width", windowWidth);
            windowHeight = EditorGUILayout.FloatField("Height", windowHeight);
            sillHeight = EditorGUILayout.FloatField("Sill height (from wall bottom)", sillHeight);
            frameThickness = EditorGUILayout.FloatField("Frame thickness", frameThickness);
            flipSide = EditorGUILayout.Toggle("Window side", flipSide);

            EditorGUILayout.Space(10);
            GUI.enabled = wall != null;
            if (GUILayout.Button("Add Visual Window", GUILayout.Height(28)))
            {
                BuildVisualWindow(wall);
            }
            if (GUILayout.Button("Add Window Opening", GUILayout.Height(28)))
            {
                BuildWallOpening(wall);
            }
            GUI.enabled = true;

            EditorGUILayout.Space(8);
            if (wall != null && GUILayout.Button("Fit Values To Wall..."))
            {
                FitToWall(wall);
            }
        }

        private void FitToWall(GameObject wall)
        {
            WallInfo info = AnalyzeWall(wall);
            if (info.bounds.size == Vector3.zero)
            {
                return;
            }

            windowWidth = Mathf.Min(1.5f, info.width * 0.5f);
            windowHeight = Mathf.Min(1.2f, info.height * 0.6f);
            sillHeight = Mathf.Min(0.6f, info.height * 0.4f);
            Repaint();
        }

        private void BuildVisualWindow(GameObject wall)
        {
            WallInfo info = AnalyzeWall(wall);
            if (info.bounds.size == Vector3.zero)
            {
                Debug.LogWarning("[AddWindowTool] Selected object has no Renderer, so it can't host a window.");
                return;
            }

            Transform wallT = wall.transform;
            Vector3 center = info.center;
            Vector3 normal = info.normal;
            Vector3 up = info.up;
            Vector3 right = info.right;

            float halfThickness = info.thickness * 0.5f;
            float glassThickness = 0.04f;
            float proudBackOffset = 0.03f;
            float frameDepth = glassThickness + 0.08f;

            Vector3 windowCenterY = center + up * ((info.bounds.min.y + sillHeight + windowHeight * 0.5f) - center.y);
            Vector3 glassCenter = windowCenterY + normal * (halfThickness + proudBackOffset + glassThickness * 0.5f);

            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            Material glassMat = EnsureGlassMaterial();
            if (wallMat == null || glassMat == null)
            {
                Debug.LogWarning("[AddWindowTool] Could not load materials. Aborting.");
                return;
            }

            Undo.SetCurrentGroupName("Add Graybox Window");
            int group = Undo.GetCurrentGroup();

            GameObject windowRoot = new GameObject("Window");
            windowRoot.transform.SetParent(wallT, true);
            Undo.RegisterCreatedObjectUndo(windowRoot, "Add Window");

            float halfWidth = windowWidth * 0.5f;
            float halfHeight = windowHeight * 0.5f;

            // Glass pane
            CreateCube("Window Glass", windowRoot.transform, glassCenter, new Vector3(windowWidth, windowHeight, glassThickness), wallT.rotation, glassMat);

            // Frame bars: top, sill (bottom), left, right
            Vector3 topCenter = glassCenter + up * (halfHeight + frameThickness * 0.5f);
            CreateCube("Window Frame Top", windowRoot.transform, topCenter,
                new Vector3(windowWidth + frameThickness * 2f, frameThickness, frameDepth), wallT.rotation, wallMat);

            Vector3 bottomCenter = glassCenter - up * (halfHeight + frameThickness * 0.5f);
            CreateCube("Window Frame Sill", windowRoot.transform, bottomCenter,
                new Vector3(windowWidth + frameThickness * 2f, frameThickness, frameDepth + 0.06f), wallT.rotation, wallMat);

            Vector3 leftCenter = glassCenter - right * (halfWidth + frameThickness * 0.5f);
            CreateCube("Window Frame Left", windowRoot.transform, leftCenter,
                new Vector3(frameThickness, windowHeight, frameDepth), wallT.rotation, wallMat);

            Vector3 rightCenter = glassCenter + right * (halfWidth + frameThickness * 0.5f);
            CreateCube("Window Frame Right", windowRoot.transform, rightCenter,
                new Vector3(frameThickness, windowHeight, frameDepth), wallT.rotation, wallMat);

            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(wall.scene);
            Debug.Log($"[AddWindowTool] Added window to '{wall.name}' at width {windowWidth:F2}, height {windowHeight:F2}, sill {sillHeight:F2}.");
        }

        private void BuildWallOpening(GameObject wall)
        {
            WallInfo info = AnalyzeWall(wall);
            if (info.bounds.size == Vector3.zero)
            {
                Debug.LogWarning("[AddWindowTool] Selected object has no Renderer, so it can't host a window.");
                return;
            }

            if (windowWidth <= 0.1f || windowHeight <= 0.1f || sillHeight < 0f)
            {
                Debug.LogWarning("[AddWindowTool] Window needs a positive width/height and a non-negative sill.");
                return;
            }
            if (windowWidth >= info.width - 0.2f || sillHeight + windowHeight >= info.height - 0.1f)
            {
                Debug.LogWarning($"[AddWindowTool] {windowWidth:F1}x{windowHeight:F1} at sill {sillHeight:F1} doesn't fit the wall ({info.width:F1} wide, {info.height:F1} tall). Adjust values or use Fit Values To Wall.");
                return;
            }

            Transform wallT = wall.transform;
            Vector3 center = info.center;
            Vector3 right = info.right;
            Vector3 up = info.up;

            float wallWidth = info.width;
            float wallHeight = info.height;
            float thickness = info.thickness;
            float centerUp = Vector3.Dot(center, up);

            float halfWidth = windowWidth * 0.5f;
            float openBottom = Vector3.Dot(info.bounds.min, up) + sillHeight;
            float openTop = openBottom + windowHeight;
            float spaceAbove = Vector3.Dot(info.bounds.max, up) - openTop;

            float leftSlabWidth = (wallWidth - windowWidth) * 0.5f;
            if (leftSlabWidth < 0.05f || spaceAbove < 0.05f)
            {
                Debug.LogWarning("[AddWindowTool] Opening would leave almost no wall beside or above it. Resize the window.");
                return;
            }

            Material wallMat = AssetDatabase.LoadAssetAtPath<Material>(WallMaterialPath);
            Material glassMat = EnsureGlassMaterial();
            if (wallMat == null || glassMat == null)
            {
                Debug.LogWarning("[AddWindowTool] Could not load materials. Aborting.");
                return;
            }

            Undo.SetCurrentGroupName("Cut Window Opening");
            int group = Undo.GetCurrentGroup();

            // Remove any window built by this tool earlier.
            for (int i = wallT.childCount - 1; i >= 0; i--)
            {
                Transform child = wallT.GetChild(i);
                if (child.name == "Window" || child.name == "Window Opening")
                {
                    Undo.DestroyObjectImmediate(child.gameObject);
                }
            }

            // The solid wall is replaced by slabs; keep it blocking everywhere except the hole.
            Renderer wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer != null)
            {
                Undo.RecordObject(wallRenderer, "Cut Window Opening");
                wallRenderer.enabled = false;
            }
            Collider wallCollider = wall.GetComponent<Collider>();
            if (wallCollider != null)
            {
                Undo.RecordObject(wallCollider, "Cut Window Opening");
                wallCollider.enabled = false;
            }

            GameObject openingRoot = new GameObject("Window Opening");
            openingRoot.transform.SetParent(wallT, true);
            Undo.RegisterCreatedObjectUndo(openingRoot, "Cut Window Opening");

            Vector3 At(float rightOffset, float upOffset)
            {
                return center + right * rightOffset + up * upOffset;
            }

            Quaternion rot = wallT.rotation;

            // Wall slabs: left, right, sill (under the opening), header (over it).
            float slabOffset = halfWidth + leftSlabWidth * 0.5f;
            CreateSlab("Wall Left", openingRoot.transform, At(-slabOffset, 0f),
                new Vector3(leftSlabWidth, wallHeight, thickness), rot, wallMat);
            CreateSlab("Wall Right", openingRoot.transform, At(slabOffset, 0f),
                new Vector3(leftSlabWidth, wallHeight, thickness), rot, wallMat);
            CreateSlab("Wall Sill", openingRoot.transform, At(0f, openBottom - sillHeight * 0.5f - centerUp),
                new Vector3(windowWidth, sillHeight, thickness), rot, wallMat);
            CreateSlab("Wall Header", openingRoot.transform, At(0f, openTop + spaceAbove * 0.5f - centerUp),
                new Vector3(windowWidth, spaceAbove, thickness), rot, wallMat);

            // Glass centered in the hole (keeps its collider so nothing walks through).
            float glassDepth = Mathf.Min(thickness, 0.06f);
            Vector3 glassCenter = At(0f, openBottom + windowHeight * 0.5f - centerUp);
            CreateSlab("Window Glass", openingRoot.transform, glassCenter,
                new Vector3(windowWidth, windowHeight, glassDepth), rot, glassMat);

            // Frame bars straddle the wall so both faces show the frame.
            float frameDepth = thickness + 0.05f;
            CreateCube("Window Frame Top", openingRoot.transform,
                glassCenter + up * (windowHeight * 0.5f + frameThickness * 0.5f),
                new Vector3(windowWidth + frameThickness * 2f, frameThickness, frameDepth), rot, wallMat);
            CreateCube("Window Frame Sill", openingRoot.transform,
                glassCenter - up * (windowHeight * 0.5f + frameThickness * 0.5f),
                new Vector3(windowWidth + frameThickness * 2f, frameThickness, frameDepth), rot, wallMat);
            CreateCube("Window Frame Left", openingRoot.transform,
                glassCenter - right * (halfWidth + frameThickness * 0.5f),
                new Vector3(frameThickness, windowHeight, frameDepth), rot, wallMat);
            CreateCube("Window Frame Right", openingRoot.transform,
                glassCenter + right * (halfWidth + frameThickness * 0.5f),
                new Vector3(frameThickness, windowHeight, frameDepth), rot, wallMat);

            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(wall.scene);
            Debug.Log($"[AddWindowTool] Cut a window opening in '{wall.name}' ({windowWidth:F2} x {windowHeight:F2}, sill {sillHeight:F2}).");
        }

        private WallInfo AnalyzeWall(GameObject wall)
        {
            WallInfo info = default;
            Renderer wallRenderer = wall.GetComponent<Renderer>();
            if (wallRenderer == null)
            {
                return info;
            }

            Transform wallT = wall.transform;
            info.bounds = wallRenderer.bounds;
            info.center = info.bounds.center;

            // The window faces out along the wall's thinnest local axis.
            Vector3 localScale = wallT.localScale;
            int thinAxis = Mathf.Abs(localScale.x) <= Mathf.Abs(localScale.y) ? 0 : 1;
            if (Mathf.Abs(localScale.z) < Mathf.Abs(localScale.x) && Mathf.Abs(localScale.z) < Mathf.Abs(localScale.y))
            {
                thinAxis = 2;
            }
            Vector3 localAxis = thinAxis == 0 ? Vector3.right : thinAxis == 1 ? Vector3.up : Vector3.forward;
            Vector3 worldAxis = wallT.TransformDirection(localAxis).normalized;

            // Outward normal points away from the mass of the parented wall group.
            Vector3 normal = worldAxis;
            Vector3 groupCenter = ComputeGroupCenter(wallT);
            if (groupCenter != info.center && Vector3.Dot(normal, info.center - groupCenter) < 0f)
            {
                normal = -normal;
            }
            if (flipSide)
            {
                normal = -normal;
            }

            info.right = wallT.right;
            info.up = wallT.up;
            info.normal = normal;
            info.thickness = Mathf.Abs(Vector3.Dot(info.bounds.size, worldAxis));
            info.width = Mathf.Abs(Vector3.Dot(info.bounds.size, info.right));
            info.height = Mathf.Abs(Vector3.Dot(info.bounds.size, info.up));
            return info;
        }

        private struct WallInfo
        {
            public Bounds bounds;
            public Vector3 center;
            public Vector3 right;
            public Vector3 up;
            public Vector3 normal;
            public float thickness;
            public float width;
            public float height;
        }

        /// <summary>Creates a cube keeping its BoxCollider (used for wall slabs, glass).</summary>
        private static GameObject CreateSlab(string name, Transform parent, Vector3 worldPos, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;

            cube.transform.SetParent(parent, true);
            cube.transform.rotation = rotation;
            cube.transform.localScale = size;
            cube.transform.position = worldPos;

            cube.GetComponent<Renderer>().material = material;
            Undo.RegisterCreatedObjectUndo(cube, "Cut Window Opening");
            return cube;
        }

        /// <summary>Creates a cube without a collider (used for decorative frame bars).</summary>
        private static GameObject CreateCube(string name, Transform parent, Vector3 worldPos, Vector3 size, Quaternion rotation, Material material)
        {
            GameObject cube = CreateSlab(name, parent, worldPos, size, rotation, material);
            Object.DestroyImmediate(cube.GetComponent<BoxCollider>());
            return cube;
        }

        /// <summary>
        /// Averages the bounds centers of the wall group (siblings under the wall's parent,
        /// falling back to the wall itself) so the window faces away from the house mass.
        /// </summary>
        private static Vector3 ComputeGroupCenter(Transform root)
        {
            Transform groupRoot = root.parent != null ? root.parent : root;
            int count = 0;
            Vector3 sum = Vector3.zero;

            foreach (Renderer renderer in groupRoot.GetComponentsInChildren<Renderer>())
            {
                if (renderer == null)
                {
                    continue;
                }
                sum += renderer.bounds.center;
                count++;
            }

            if (count == 0 && root.GetComponent<Renderer>() != null)
            {
                return root.GetComponent<Renderer>().bounds.center;
            }

            return count > 0 ? sum / count : root.position;
        }

        private static Material EnsureGlassMaterial()
        {
            Material mat = AssetDatabase.LoadAssetAtPath<Material>(GlassMaterialPath);

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

                mat = new Material(shader)
                {
                    color = new Color(0.55f, 0.7f, 0.8f, 0.4f)
                };
                AssetDatabase.CreateAsset(mat, GlassMaterialPath);
                created = true;
            }

            ConfigureTransparent(mat);
            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log("[AddWindowTool] Created Graybox_Glass material.");
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }

        /// <summary>
        /// Switches a URP Lit material to a transparent, double-sided look.
        /// The _SURFACE_TYPE_TRANSPARENT keyword is what actually enables the transparent pass.
        /// </summary>
        private static void ConfigureTransparent(Material mat)
        {
            if (mat.HasProperty("_Surface"))
            {
                mat.SetFloat("_Surface", 1f); // 0 = Opaque, 1 = Transparent
            }
            if (mat.HasProperty("_Blend"))
            {
                mat.SetFloat("_Blend", 0f); // Alpha
            }
            if (mat.HasProperty("_SrcBlend"))
            {
                mat.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha);
            }
            if (mat.HasProperty("_DstBlend"))
            {
                mat.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
            }
            if (mat.HasProperty("_ZWrite"))
            {
                mat.SetFloat("_ZWrite", 0f);
            }
            if (mat.HasProperty("_BaseColor"))
            {
                mat.SetColor("_BaseColor", mat.color);
            }
            mat.SetOverrideTag("RenderType", "Transparent");
            mat.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            mat.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            mat.renderQueue = (int)RenderQueue.Transparent;
        }
    }
}