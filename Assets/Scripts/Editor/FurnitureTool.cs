using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using HollowCreek.Interaction;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Spawns graybox furniture (table, chair, bench, bed) and interactable
    /// test props (note, key), plus a one-button Test Yard on the left side of
    /// the house with a note-and-key table setup.
    /// Access via menu: Tools > Hollow Creek > Furniture...
    /// </summary>
    public class FurnitureTool : EditorWindow
    {
        private const string FurnitureMaterialPath = "Assets/Materials/Graybox_Furniture.mat";
        private const string BeddingMaterialPath = "Assets/Materials/Graybox_Bedding.mat";
        private const string EnvironmentRootName = "--- Environment ---";
        private const string TestYardRootName = "Test Yard";

        // Test Yard sits on the house's left side (open grass, x 27-32, z -93..-99).
        private static readonly Vector3 TestYardPosition = new Vector3(29.5f, 0f, -96f);

        private Vector3 spawnPosition = TestYardPosition;

        [MenuItem("Tools/Hollow Creek/Furniture...")]
        private static void Open()
        {
            FurnitureTool window = GetWindow<FurnitureTool>("Furniture");
            window.minSize = new Vector2(430, 330);
        }

        private void OnGUI()
        {
            EditorGUILayout.HelpBox(
                "Spawn individual props (they appear at Position and get selected so " +
                "you can drag them), or build the whole Test Yard with one click. " +
                "Notes and keys work with the existing interaction system (E). " +
                "Every piece of furniture gets an invisible 'Collision' box that is " +
                "bigger than the mesh, so the player can never climb on it.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("1. Place Item", EditorStyles.boldLabel);
            spawnPosition = EditorGUILayout.Vector3Field("Position (base at ground)", spawnPosition);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Table", GUILayout.Height(26))) SpawnIndividual("Table");
            if (GUILayout.Button("Chair", GUILayout.Height(26))) SpawnIndividual("Chair");
            if (GUILayout.Button("Bench", GUILayout.Height(26))) SpawnIndividual("Bench");
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Bed", GUILayout.Height(26))) SpawnIndividual("Bed");
            if (GUILayout.Button("Double Bed", GUILayout.Height(26))) SpawnIndividual("Double Bed");
            if (GUILayout.Button("Note", GUILayout.Height(26))) SpawnIndividual("Note");
            if (GUILayout.Button("Key", GUILayout.Height(26))) SpawnIndividual("Key");
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("2. Test Yard", EditorStyles.boldLabel);
            EditorGUILayout.LabelField(
                "Left side of the house: table with 2 notes + key, chairs, bed, bench.",
                EditorStyles.miniLabel);

            EditorGUILayout.BeginHorizontal();
            if (GUILayout.Button("Add Test Yard (replaces previous)", GUILayout.Height(28)))
            {
                BuildTestYard();
            }
            if (GUILayout.Button("Remove Test Yard", GUILayout.Height(28)))
            {
                RemoveTestYard();
            }
            EditorGUILayout.EndHorizontal();
        }

        // ---------------------------------------------------- individual items

        private void SpawnIndividual(string kind)
        {
            Undo.SetCurrentGroupName($"Add {kind}");
            int group = Undo.GetCurrentGroup();

            Material furniture = EnsureFurnitureMaterial();
            Material bedding = EnsureBeddingMaterial();

            GameObject root = kind switch
            {
                "Table" => BuildTable(null, spawnPosition, 0f, furniture),
                "Chair" => BuildChair(null, spawnPosition, 0f, furniture),
                "Bench" => BuildBench(null, spawnPosition, 0f, furniture),
                "Bed" => BuildBed(null, spawnPosition, 0f, furniture, bedding, twoPlace: false),
                "Double Bed" => BuildBed(null, spawnPosition, 0f, furniture, bedding, twoPlace: true),
                "Note" => BuildNote(null, spawnPosition + new Vector3(0f, 0.01f, 0f), 0f, "Test Note",
                    "This is a test note. If you can read this, the interaction system works.",
                    displayFullSheet: false),
                "Key" => BuildKey(null, spawnPosition + new Vector3(0f, 0.18f, 0f), 0f),
                _ => null
            };

            if (root == null)
            {
                return;
            }

            Undo.CollapseUndoOperations(group);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = root;
            Debug.Log($"[FurnitureTool] Added {kind} at {spawnPosition}.");
        }

        // ---------------------------------------------------------- test yard

        private static Transform FindTestYard()
        {
            foreach (GameObject rootObject in SceneManager.GetActiveScene().GetRootGameObjects())
            {
                Transform found = FindDeepChild(rootObject.transform, TestYardRootName);
                if (found != null)
                {
                    return found;
                }
            }
            return null;
        }

        private void BuildTestYard()
        {
            Undo.SetCurrentGroupName("Add Test Yard");
            int group = Undo.GetCurrentGroup();

            Material furniture = EnsureFurnitureMaterial();
            Material bedding = EnsureBeddingMaterial();
            if (furniture == null || bedding == null)
            {
                Debug.LogWarning("[FurnitureTool] Could not create materials. Aborting.");
                return;
            }

            RemoveExistingTestYard();

            GameObject yard = new GameObject(TestYardRootName);
            Transform environment = EnvironmentRoot();
            if (environment != null)
            {
                yard.transform.SetParent(environment, false);
            }
            yard.transform.localPosition = TestYardPosition;
            Undo.RegisterCreatedObjectUndo(yard, "Add Test Yard");
            Transform y = yard.transform;

            // Table with the interactables on top (parented to the table so
            // moving the table carries the notes and key with it).
            Transform table = BuildTable(y, Vector3.zero, 0f, furniture).transform;
            const float topY = 0.75f;
            BuildNote(table, new Vector3(-0.3f, topY + 0.01f, 0.1f), 0f, "Test Journal",
                "September 14th\n\nI found something strange near the barn today. The symbols " +
                "carved into the wall don't match anything I've seen before, and the carving " +
                "feels fresh. Too fresh.\n\nI tried to trace one of the lines, and the lantern " +
                "flickered the moment I touched it. I told myself it was just the wind.\n\n" +
                "Tomorrow I'll come back with a better light. Something tells me this isn't " +
                "the last we'll see of those marks.",
                displayFullSheet: true);
            BuildNote(table, new Vector3(0.35f, topY + 0.01f, 0.1f), 15f, "Test Memo",
                "Milk, eggs, bread.\nDon't forget the bread this time.",
                displayFullSheet: false);
            BuildKey(table, new Vector3(0f, topY + 0.18f, -0.15f), 0f);

            // Chairs west of the table, facing it (rotY 90 = facing +x).
            BuildChair(y, new Vector3(-1.4f, 0f, 0.85f), 90f, furniture);
            BuildChair(y, new Vector3(-1.4f, 0f, -0.85f), 90f, furniture);

            // Bed in the far corner, bench beside it.
            BuildBed(y, new Vector3(2.5f, 0f, 2.5f), 0f, furniture, bedding, twoPlace: true);
            BuildBench(y, new Vector3(2f, 0f, -2.5f), 0f, furniture);

            Undo.CollapseUndoOperations(group);

            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Selection.activeGameObject = yard;
            Debug.Log("[FurnitureTool] Test Yard added on the house's left side at " + TestYardPosition + ".");
        }

        private void RemoveTestYard()
        {
            Transform yard = FindTestYard();
            if (yard == null)
            {
                Debug.LogWarning("[FurnitureTool] No Test Yard in the scene.");
                return;
            }

            Undo.DestroyObjectImmediate(yard.gameObject);
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.Log("[FurnitureTool] Test Yard removed.");
        }

        private void RemoveExistingTestYard()
        {
            Transform yard = FindTestYard();
            if (yard != null)
            {
                Undo.DestroyObjectImmediate(yard.gameObject);
                Debug.Log("[FurnitureTool] Replaced previous Test Yard.");
            }
        }

        // ----------------------------------------------------------- builders
        // All builders use LOCAL coordinates under the parent (or world when
        // parent is null) so a yard can be moved as one unit.

        private static GameObject NewRoot(string name, Transform parent, Vector3 localPos, float rotY)
        {
            GameObject root = new GameObject(name);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
            }
            root.transform.localPosition = localPos;
            root.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            Undo.RegisterCreatedObjectUndo(root, $"Add {name}");
            return root;
        }

        private static GameObject Child(Transform parent, string name, Vector3 localPos, Vector3 scale, Material mat)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = name;
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = localPos;
            cube.transform.localScale = scale;
            if (mat != null)
            {
                cube.GetComponent<Renderer>().material = mat;
            }
            return cube;
        }

        private static GameObject Blocker(Transform parent, Vector3 size)
        {
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.name = "Collision";
            cube.transform.SetParent(parent, false);
            cube.transform.localPosition = new Vector3(0f, size.y * 0.5f, 0f);
            cube.transform.localScale = size;
            UnityEngine.Object.DestroyImmediate(cube.GetComponent<Renderer>());
            Undo.RegisterCreatedObjectUndo(cube, "Add collision box");
            return cube;
        }

        private static GameObject BuildTable(Transform parent, Vector3 localPos, float rotY, Material mat)
        {
            GameObject root = NewRoot("Table", parent, localPos, rotY);
            Transform t = root.transform;
            // Top surface at y = 0.75.
            Child(t, "Table Top", new Vector3(0f, 0.72f, 0f), new Vector3(1.6f, 0.06f, 0.9f), mat);
            foreach (Vector3 leg in new[]
            {
                new Vector3(0.72f, 0.36f, 0.39f), new Vector3(0.72f, 0.36f, -0.39f),
                new Vector3(-0.72f, 0.36f, 0.39f), new Vector3(-0.72f, 0.36f, -0.39f)
            })
            {
                Child(t, "Leg", leg, new Vector3(0.06f, 0.72f, 0.06f), mat);
            }
            Blocker(t, new Vector3(1.7f, 0.72f, 1f));
            return root;
        }

        private static GameObject BuildChair(Transform parent, Vector3 localPos, float rotY, Material mat)
        {
            GameObject root = NewRoot("Chair", parent, localPos, rotY);
            Transform t = root.transform;
            Child(t, "Seat", new Vector3(0f, 0.4f, 0f), new Vector3(0.45f, 0.05f, 0.45f), mat);
            Child(t, "Back", new Vector3(0f, 0.68f, -0.2f), new Vector3(0.45f, 0.55f, 0.05f), mat);
            foreach (Vector3 leg in new[]
            {
                new Vector3(0.18f, 0.19f, 0.18f), new Vector3(0.18f, 0.19f, -0.18f),
                new Vector3(-0.18f, 0.19f, 0.18f), new Vector3(-0.18f, 0.19f, -0.18f)
            })
            {
                Child(t, "Leg", leg, new Vector3(0.05f, 0.38f, 0.05f), mat);
            }
            Blocker(t, new Vector3(0.55f, 0.4f, 0.55f));
            return root;
        }

        private static GameObject BuildBench(Transform parent, Vector3 localPos, float rotY, Material mat)
        {
            GameObject root = NewRoot("Bench", parent, localPos, rotY);
            Transform t = root.transform;
            Child(t, "Bench Top", new Vector3(0f, 0.41f, 0f), new Vector3(1.2f, 0.08f, 0.4f), mat);
            Child(t, "Leg", new Vector3(0.5f, 0.185f, 0f), new Vector3(0.1f, 0.37f, 0.36f), mat);
            Child(t, "Leg", new Vector3(-0.5f, 0.185f, 0f), new Vector3(0.1f, 0.37f, 0.36f), mat);
            Blocker(t, new Vector3(1.3f, 0.42f, 0.5f));
            return root;
        }

        private static GameObject BuildBed(Transform parent, Vector3 localPos, float rotY,
            Material frameMat, Material beddingMat, bool twoPlace)
        {
            float width = twoPlace ? 1.5f : 1f;
            GameObject root = NewRoot(twoPlace ? "Bed (Double)" : "Bed", parent, localPos, rotY);
            Transform t = root.transform;
            Child(t, "Frame", new Vector3(0f, 0.15f, 0f), new Vector3(2f, 0.3f, width), frameMat);
            Child(t, "Mattress", new Vector3(0f, 0.39f, 0f), new Vector3(1.9f, 0.18f, width - 0.1f), beddingMat);
            if (twoPlace)
            {
                Child(t, "Pillow", new Vector3(-0.72f, 0.53f, 0.35f), new Vector3(0.4f, 0.1f, 0.6f), beddingMat);
                Child(t, "Pillow", new Vector3(-0.72f, 0.53f, -0.35f), new Vector3(0.4f, 0.1f, 0.6f), beddingMat);
            }
            else
            {
                Child(t, "Pillow", new Vector3(-0.72f, 0.53f, 0f), new Vector3(0.4f, 0.1f, 0.6f), beddingMat);
            }
            Child(t, "Headboard", new Vector3(-1f, 0.375f, 0f), new Vector3(0.06f, 0.75f, width), frameMat);
            Blocker(t, new Vector3(2.2f, 0.45f, width + 0.1f));
            return root;
        }

        private static GameObject BuildNote(Transform parent, Vector3 localPos, float rotY,
            string title, string body, bool displayFullSheet)
        {
            GameObject note = GameObject.CreatePrimitive(PrimitiveType.Cube);
            note.name = $"Note ({title})";
            if (parent != null)
            {
                note.transform.SetParent(parent, false);
            }
            note.transform.localPosition = localPos;
            note.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            note.transform.localScale = new Vector3(0.3f, 0.02f, 0.4f);
            Undo.RegisterCreatedObjectUndo(note, "Add Note");

            Renderer renderer = note.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.9f, 0.85f, 0.5f, 1f)
            };
            renderer.material = mat;

            InteractableNote noteComp = note.AddComponent<InteractableNote>();
            var serialized = new SerializedObject(noteComp);
            serialized.FindProperty("genericName").stringValue = "Piece of paper";
            serialized.FindProperty("noteTitle").stringValue = title;
            serialized.FindProperty("noteText").stringValue = body;
            serialized.FindProperty("promptText").stringValue = "Read note";
            serialized.FindProperty("displayAsFullSheet").boolValue = displayFullSheet;
            serialized.FindProperty("displaySeconds").floatValue = displayFullSheet ? 25f : 8f;
            serialized.ApplyModifiedProperties();
            return note;
        }

        private static GameObject BuildKey(Transform parent, Vector3 localPos, float rotY)
        {
            GameObject key = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            key.name = "Key";
            if (parent != null)
            {
                key.transform.SetParent(parent, false);
            }
            key.transform.localPosition = localPos;
            key.transform.localRotation = Quaternion.Euler(0f, rotY, 0f);
            key.transform.localScale = new Vector3(0.06f, 0.18f, 0.06f);
            Undo.RegisterCreatedObjectUndo(key, "Add Key");

            Renderer renderer = key.GetComponent<Renderer>();
            Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"))
            {
                color = new Color(0.85f, 0.7f, 0.2f, 1f)
            };
            mat.SetFloat("_Metallic", 0.8f);
            mat.SetFloat("_Smoothness", 0.9f);
            renderer.material = mat;

            InteractableKeyPickup keyComp = key.AddComponent<InteractableKeyPickup>();
            var serialized = new SerializedObject(keyComp);
            serialized.FindProperty("genericName").stringValue = "Key";
            serialized.FindProperty("keyName").stringValue = "Test Key";
            serialized.FindProperty("mustExamineBeforePickup").boolValue = true;
            serialized.FindProperty("keyId").stringValue = "key_test";
            serialized.FindProperty("promptText").stringValue = "Inspect key";
            serialized.FindProperty("description").stringValue =
                "A small brass test key. If the examine-then-pickup flow works, it's yours.";
            serialized.ApplyModifiedProperties();
            return key;
        }

        // ------------------------------------------------------------ helpers

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

        private static Material EnsureFurnitureMaterial()
        {
            return EnsureMaterial(FurnitureMaterialPath, new Color(0.42f, 0.3f, 0.2f, 1f), 0.2f);
        }

        private static Material EnsureBeddingMaterial()
        {
            return EnsureMaterial(BeddingMaterialPath, new Color(0.85f, 0.85f, 0.82f, 1f), 0.1f);
        }

        private static Material EnsureMaterial(string path, Color color, float smoothness)
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

            if (created)
            {
                AssetDatabase.SaveAssets();
                Debug.Log($"[FurnitureTool] Created {System.IO.Path.GetFileName(path)}.");
            }
            else
            {
                EditorUtility.SetDirty(mat);
            }
            return mat;
        }
    }
}
