using UnityEngine;
using UnityEditor;
using UnityEngine.UI;
using TMPro;
using HollowCreek.Interaction;
using HollowCreek.Story;
using UnityEngine.InputSystem;
using UnityEditor.SceneManagement;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Sets up sample interactables and UI for testing the interaction system.
    /// Access via menu: Tools > Hollow Creek > Setup Interactions.
    /// </summary>
    public static class SetupInteractions
    {
        [MenuItem("Tools/Hollow Creek/Setup Interactions")]
        public static void Setup()
        {
            // 1. Add InteractionSystem to Camera
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                Debug.LogWarning("[Hollow Creek] No Player found. Add a player first.");
                return;
            }

            Transform cameraTransform = player.transform.Find("Camera");
            if (cameraTransform == null)
            {
                Debug.LogWarning("[Hollow Creek] No Camera found under Player.");
                return;
            }

            GameObject cameraObj = cameraTransform.gameObject;
            InteractionSystem interactionSystem = cameraObj.GetComponent<InteractionSystem>();
            if (interactionSystem == null)
            {
                interactionSystem = cameraObj.AddComponent<InteractionSystem>();
            }

            // Assign input actions
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputActions != null)
            {
                var serializedObj = new SerializedObject(interactionSystem);
                serializedObj.FindProperty("inputActions").objectReferenceValue = inputActions;
                serializedObj.ApplyModifiedProperties();
            }

            // 2. Setup UI
            SetupUI(interactionSystem);

            // 2b. Note UI: instantiate the asset's Insight UI Manager overlay (if missing)
            RemoveLegacyNoteReadPanel();
            EnsureInsightUIManager();
            RemoveDuplicateEventSystems();
            BuildNoteSheetUI();

            // 3. Create sample interactables
            SetupSampleInteractables();

            // 4. Setup GameManager
            if (HollowCreek.Story.GameManager.Instance == null)
            {
                GameObject managers = GameObject.Find("--- Managers ---");
                if (managers == null) managers = new GameObject("--- Managers ---");

                GameObject gameManager = GameObject.Find("GameManager");
                if (gameManager == null)
                {
                    gameManager = new GameObject("GameManager");
                    gameManager.transform.SetParent(managers.transform);
                }

                if (gameManager.GetComponent<HollowCreek.Story.GameManager>() == null)
                {
                    gameManager.AddComponent<HollowCreek.Story.GameManager>();
                }
            }

            Debug.Log("[Hollow Creek] Interaction system setup complete! Press Play to test.");
        }

        private static void SetupUI(InteractionSystem interactionSystem)
        {
            // Create or find Canvas
            Canvas canvas = GameObject.Find("Canvas")?.GetComponent<Canvas>();
            if (canvas == null)
            {
                GameObject canvasObj = new GameObject("Canvas");
                canvas = canvasObj.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = Camera.main;
                canvas.planeDistance = 1f;
                CanvasScaler scaler = canvasObj.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920, 1080);
                canvasObj.AddComponent<GraphicRaycaster>();
            }

            // Create EventSystem if missing
            if (Object.FindAnyObjectByType<UnityEngine.EventSystems.EventSystem>() == null)
            {
                GameObject eventSystem = new GameObject("EventSystem");
                eventSystem.AddComponent<UnityEngine.EventSystems.EventSystem>();
                eventSystem.AddComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>();
            }

            // --- Interaction Prompt (bottom center) ---
            GameObject promptObj = GameObject.Find("InteractionPrompt");
            if (promptObj == null)
            {
                promptObj = CreateUIText(canvas.transform, "InteractionPrompt", "Press E to interact",
                    new Vector2(0, -150), new Vector2(400, 60), 24, TextAlignmentOptions.Center);
                promptObj.AddComponent<InteractionPrompt>();

                var prompt = promptObj.GetComponent<InteractionPrompt>();
                var serializedPrompt = new SerializedObject(prompt);
                serializedPrompt.FindProperty("interactionSystem").objectReferenceValue = interactionSystem;
                serializedPrompt.ApplyModifiedProperties();
            }

            // --- Crosshair (center dot) ---
            GameObject crosshairObj = GameObject.Find("Crosshair");
            if (crosshairObj == null)
            {
                crosshairObj = new GameObject("Crosshair");
                crosshairObj.transform.SetParent(canvas.transform, false);

                RectTransform rect = crosshairObj.AddComponent<RectTransform>();
                rect.anchorMin = new Vector2(0.5f, 0.5f);
                rect.anchorMax = new Vector2(0.5f, 0.5f);
                rect.anchoredPosition = Vector2.zero;
                rect.sizeDelta = new Vector2(6, 6);

                UnityEngine.UI.Image dot = crosshairObj.AddComponent<UnityEngine.UI.Image>();
                dot.color = new Color(1f, 1f, 1f, 0.6f);

                crosshairObj.AddComponent<Crosshair>();

                var crosshair = crosshairObj.GetComponent<Crosshair>();
                var serializedCrosshair = new SerializedObject(crosshair);
                serializedCrosshair.FindProperty("interactionSystem").objectReferenceValue = interactionSystem;
                serializedCrosshair.ApplyModifiedProperties();
            }

            // --- Note Read Panel removed; note UI now uses the Insight UI Manager prefab ---

            // Move Canvas under UI folder
            Transform uiParent = GameObject.Find("--- UI ---")?.transform;
            if (uiParent != null && canvas.transform.parent != uiParent)
            {
                canvas.transform.SetParent(uiParent);
            }
        }

        private static GameObject CreateUIText(Transform parent, string name, string text,
            Vector2 position, Vector2 size, float fontSize, TextAlignmentOptions alignment, bool bold = false)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchoredPosition = position;
            rect.sizeDelta = size;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.alignment = alignment;
            tmp.color = Color.white;
            if (bold) tmp.fontStyle = FontStyles.Bold;

            return textObj;
        }

        private static void RemoveLegacyNoteReadPanel()
        {
            GameObject panel = GameObject.Find("NoteReadPanel");
            if (panel != null)
            {
                Object.DestroyImmediate(panel);
                Debug.Log("[Hollow Creek] Removed legacy NoteReadPanel.");
            }
        }

        private const float NoteDisplaySeconds = 20f;

        /// <summary>
        /// Instantiates the asset's Insight UI Manager prefab once, so notes can
        /// display through its overlay canvases. Its own crosshair dot is disabled
        /// because the scene already has a crosshair.
        /// </summary>
        private static void EnsureInsightUIManager()
        {
            GameObject existing = GameObject.Find("Insight UI Manager");
            if (existing != null)
            {
                ApplyNoteDisplaySeconds(existing);
                PositionNameNearDetails(existing);
                return;
            }

            const string prefabPath = "Assets/Object Insight Highlighter V1.1/Prefabs/Managers/Insight UI Manager.prefab";
            GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(prefabPath);
            if (prefab == null)
            {
                Debug.LogWarning("[Hollow Creek] Insight UI Manager prefab not found at: " + prefabPath);
                return;
            }

            GameObject instance = (GameObject)PrefabUtility.InstantiatePrefab(prefab);
            instance.name = "Insight UI Manager";

            Transform uiParent = GameObject.Find("--- UI ---")?.transform;
            if (uiParent != null)
            {
                instance.transform.SetParent(uiParent, false);
            }

            Transform crosshairCanvas = instance.transform.Find("Crosshair - Canvas");
            if (crosshairCanvas != null)
            {
                crosshairCanvas.gameObject.SetActive(false);
                Debug.Log("[Hollow Creek] Disabled the Insight UI Manager's crosshair (scene already has one).");
            }

            Transform prefabEventSystem = instance.transform.Find("EventSystem");
            if (prefabEventSystem != null)
            {
                prefabEventSystem.gameObject.SetActive(false);
                Debug.Log("[Hollow Creek] Disabled the Insight UI Manager's EventSystem (scene already has one).");
            }

            Debug.Log("[Hollow Creek] Insight UI Manager added to scene.");
            ApplyNoteDisplaySeconds(instance);
            PositionNameNearDetails(instance);
        }

        private static void PositionNameNearDetails(GameObject managerObj)
        {
            Transform nameBg = managerObj.transform.Find("Main Details - Canvas/UI - Object Name BG");
            if (nameBg == null)
            {
                return;
            }

            RectTransform rect = (RectTransform)nameBg;
            // The details message sits at the bottom of the screen (anchor bottom-center).
            // Move the object name to sit right above it so they look connected.
            rect.anchorMin = new Vector2(0.5f, 0f);
            rect.anchorMax = new Vector2(0.5f, 0f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = new Vector2(0f, 165f);
            Debug.Log("[Hollow Creek] Object name repositioned to sit above the details message.");
        }

        private static void ApplyNoteDisplaySeconds(GameObject managerObj)
        {
            var manager = managerObj.GetComponent<HollowCreek.UI.TextInspectUIManager>();
            if (manager == null)
            {
                return;
            }

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("onScreenTimer").floatValue = NoteDisplaySeconds;
            so.ApplyModifiedProperties();
            Debug.Log($"[Hollow Creek] Insight UI Manager onScreenTimer set to {NoteDisplaySeconds}s.");
        }

        [MenuItem("Tools/Hollow Creek/Rebuild Note UI")]
        public static void RebuildNoteUI()
        {
            BuildNoteSheetUI();
        }

        /// <summary>
        /// Builds the full-screen scrollable note sheet under the Insight UI Manager's
        /// "Main Details - Canvas" and wires it into the manager via SerializedObject.
        /// The rebuild is idempotent: any existing "Note Sheet" is destroyed first.
        /// </summary>
        private static void BuildNoteSheetUI()
        {
            GameObject managerObj = GameObject.Find("Insight UI Manager");
            if (managerObj == null)
            {
                Debug.LogWarning("[Hollow Creek] No 'Insight UI Manager' in scene. Run 'Setup Interactions' first.");
                return;
            }

            var manager = managerObj.GetComponent<HollowCreek.UI.TextInspectUIManager>();
            if (manager == null)
            {
                Debug.LogWarning("[Hollow Creek] 'Insight UI Manager' has no TextInspectUIManager component.");
                return;
            }

            Transform canvas = managerObj.transform.Find("Main Details - Canvas");
            if (canvas == null)
            {
                Debug.LogWarning("[Hollow Creek] 'Main Details - Canvas' not found on the Insight UI Manager.");
                return;
            }

            // Idempotent: remove any previous build
            Transform existing = canvas.Find("Note Sheet");
            if (existing != null)
            {
                Object.DestroyImmediate(existing.gameObject);
            }

            // --- Root panel ---
            GameObject sheet = new GameObject("Note Sheet");
            sheet.transform.SetParent(canvas, false);

            RectTransform sheetRect = sheet.AddComponent<RectTransform>();
            sheetRect.anchorMin = new Vector2(0.5f, 0.5f);
            sheetRect.anchorMax = new Vector2(0.5f, 0.5f);
            sheetRect.pivot = new Vector2(0.5f, 0.5f);
            sheetRect.anchoredPosition = Vector2.zero;
            sheetRect.sizeDelta = new Vector2(1500, 850);

            Image sheetImage = sheet.AddComponent<Image>();
            Sprite grunge = AssetDatabase.LoadAssetAtPath<Sprite>(
                "Assets/Object Insight Highlighter V1.1/Sprites/Sprite_GrungeBacking_2_500px.png");
            if (grunge != null)
            {
                sheetImage.sprite = grunge;
                sheetImage.type = Image.Type.Sliced;
            }
            sheetImage.color = new Color(0.08f, 0.08f, 0.08f, 0.97f);

            CanvasGroup sheetGroup = sheet.AddComponent<CanvasGroup>();
            sheetGroup.alpha = 0f;
            sheetGroup.interactable = false;
            sheetGroup.blocksRaycasts = false;

            ScrollRect scrollRect = sheet.AddComponent<ScrollRect>();
            scrollRect.horizontal = false;
            scrollRect.vertical = true;
            scrollRect.movementType = ScrollRect.MovementType.Clamped;
            scrollRect.inertia = false;
            scrollRect.scrollSensitivity = 12f;

            // --- Title ---
            CreateSheetText(sheet.transform, "Note Sheet Title", "", 36, TextAlignmentOptions.Center, FontStyles.Bold,
                new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0.5f, 1), new Vector2(0, -35), new Vector2(800, 70));

            // --- Viewport (clips body text) ---
            GameObject viewport = new GameObject("Viewport");
            viewport.transform.SetParent(sheet.transform, false);

            RectTransform viewportRect = viewport.AddComponent<RectTransform>();
            viewportRect.anchorMin = Vector2.zero;
            viewportRect.anchorMax = Vector2.one;
            viewportRect.offsetMin = new Vector2(180, 60);
            viewportRect.offsetMax = new Vector2(-180, -120);

            viewport.AddComponent<RectMask2D>();

            // --- Content (grows with the body's preferred height) ---
            GameObject content = new GameObject("Content");
            content.transform.SetParent(viewport.transform, false);

            RectTransform contentRect = content.AddComponent<RectTransform>();
            contentRect.anchorMin = new Vector2(0, 1);
            contentRect.anchorMax = new Vector2(1, 1);
            contentRect.pivot = new Vector2(0.5f, 1);
            contentRect.anchoredPosition = Vector2.zero;
            contentRect.sizeDelta = new Vector2(0, 0);

            ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            // --- Body text ---
            TextMeshProUGUI bodyText = CreateSheetText(content.transform, "Note Sheet Body", "", 26,
                TextAlignmentOptions.TopLeft, FontStyles.Normal,
                new Vector2(0, 1), new Vector2(1, 1), new Vector2(0.5f, 1), Vector2.zero, Vector2.zero);

            // --- Wire up scroll rect ---
            scrollRect.viewport = viewportRect;
            scrollRect.content = contentRect;

            SerializedObject so = new SerializedObject(manager);
            so.FindProperty("noteSheet").objectReferenceValue = sheet;
            so.FindProperty("noteSheetTitleText").objectReferenceValue = sheet.transform.Find("Note Sheet Title").GetComponent<TextMeshProUGUI>();
            so.FindProperty("noteSheetBodyText").objectReferenceValue = bodyText;
            so.FindProperty("noteSheetScrollRect").objectReferenceValue = scrollRect;
            so.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(canvas.gameObject.scene);
            Debug.Log("[Hollow Creek] Note Sheet built and wired into Insight UI Manager.");
        }

        private static TextMeshProUGUI CreateSheetText(Transform parent, string name, string text, float fontSize,
            TextAlignmentOptions alignment, FontStyles style,
            Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 sizeDelta)
        {
            GameObject textObj = new GameObject(name);
            textObj.transform.SetParent(parent, false);

            RectTransform rect = textObj.AddComponent<RectTransform>();
            rect.anchorMin = anchorMin;
            rect.anchorMax = anchorMax;
            rect.pivot = pivot;
            rect.anchoredPosition = anchoredPosition;
            rect.sizeDelta = sizeDelta;

            TextMeshProUGUI tmp = textObj.AddComponent<TextMeshProUGUI>();
            tmp.text = text;
            tmp.fontSize = fontSize;
            tmp.fontSizeMin = 18;
            tmp.enableAutoSizing = false;
            tmp.alignment = alignment;
            tmp.fontStyle = style;
            tmp.color = new Color(0.95f, 0.93f, 0.88f, 1f);
            return tmp;
        }

        private static void RemoveDuplicateEventSystems()
        {
            var all = Object.FindObjectsByType<UnityEngine.EventSystems.EventSystem>(FindObjectsInactive.Include, FindObjectsSortMode.None);

            UnityEngine.EventSystems.EventSystem keeper = null;
            foreach (var es in all)
            {
                if (es.GetComponent<UnityEngine.InputSystem.UI.InputSystemUIInputModule>() != null)
                {
                    keeper = es;
                    break;
                }
            }
            if (keeper == null && all.Length > 0)
            {
                keeper = all[0];
            }

            foreach (var es in all)
            {
                if (es != keeper)
                {
                    Object.DestroyImmediate(es.gameObject);
                }
            }

            if (all.Length > 1)
            {
                Debug.Log($"[Hollow Creek] Removed {all.Length - 1} duplicate EventSystem(s). Keeping '{keeper.name}'.");
            }
        }

        private static void SetupSampleInteractables()
        {
            // Find the house
            Transform house = GameObject.Find("Main Farm House")?.transform;
            if (house == null)
            {
                Debug.LogWarning("[Hollow Creek] No 'Main Farm House' found. Create the house first.");
                return;
            }

            Transform walls = house.Find("Walls");
            Transform environment = GameObject.Find("--- Environment ---")?.transform;

            // --- Sample Note Table (a square block to mimic a table) ---
            // Interior walkable floor is the top of House_Foundation (y = 1.0),
            // so the table stands 0.8 m tall on top of it (top at y = 1.8).
            const float floorY = 1.0f;
            const float tableHeight = 0.8f;
            const float tableTopY = floorY + tableHeight;

            Vector3 tableCenter = walls != null
                ? new Vector3(13, floorY + tableHeight * 0.5f, -93.5f)
                : new Vector3(0, floorY + tableHeight * 0.5f, 0);

            GameObject tableObj = GameObject.Find("Sample_Note_Table");
            if (tableObj == null)
            {
                tableObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                tableObj.name = "Sample_Note_Table";

                if (environment != null)
                    tableObj.transform.SetParent(environment);

                Renderer tableRenderer = tableObj.GetComponent<Renderer>();
                if (tableRenderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.42f, 0.3f, 0.2f);
                    tableRenderer.material = mat;
                }
            }

            // Always reposition and resize the table (fixes an existing misplaced object)
            tableObj.transform.localScale = new Vector3(1.6f, tableHeight, 1.0f);
            tableObj.transform.position = tableCenter;

            // --- Sample Note ---
            GameObject noteObj = GameObject.Find("Sample_Note");
            if (noteObj == null)
            {
                noteObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                noteObj.name = "Sample_Note";
                noteObj.transform.localScale = new Vector3(0.3f, 0.02f, 0.4f);

                if (environment != null)
                    noteObj.transform.SetParent(environment);

                Renderer renderer = noteObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.9f, 0.85f, 0.5f);
                    renderer.material = mat;
                }
            }

            // Always reposition the journal onto the table
            float noteY = tableTopY + 0.02f;
            noteObj.transform.position = new Vector3(12.5f, noteY, tableCenter.z + 0.1f);

            // Always configure InteractableNote (even if object already exists)
            {
                InteractableNote noteComp = noteObj.GetComponent<InteractableNote>();
                if (noteComp == null) noteComp = noteObj.AddComponent<InteractableNote>();

                var serializedNote = new SerializedObject(noteComp);
                serializedNote.FindProperty("genericName").stringValue = "Piece of paper";
                serializedNote.FindProperty("noteTitle").stringValue = "Old Journal Entry";
                serializedNote.FindProperty("noteText").stringValue =
                    "September 14th\n\nI found something strange in the barn today. The symbols carved into the wall don't match anything I've seen before, and the carving feels fresh. Too fresh.\n\nI tried to trace one of the lines, but my skin went cold the moment I touched it. The lantern flickered. I told myself it was just the wind.\n\nI need to investigate further. Tomorrow, I'll go back with a better light and maybe take a photograph for the records. Something tells me this isn't the last we'll see of those marks.\n\nAlong the way I noticed the cellar door was open again. No one else is supposed to be here this week. I swear I locked it on Sunday.\n\nWhatever is down there... I think it's been watching me through the cracks.";
                serializedNote.FindProperty("promptText").stringValue = "Read journal";
                serializedNote.FindProperty("displayAsFullSheet").boolValue = true;
                serializedNote.FindProperty("displaySeconds").floatValue = 25f;
                serializedNote.ApplyModifiedProperties();

                Selection.activeGameObject = noteObj;
            }

            // --- Sample Note (short, tooltip mode) beside the journal ---
            GameObject shortNoteObj = GameObject.Find("Sample_Note_Short");
            if (shortNoteObj == null)
            {
                shortNoteObj = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shortNoteObj.name = "Sample_Note_Short";
                shortNoteObj.transform.localScale = new Vector3(0.3f, 0.02f, 0.4f);

                if (environment != null)
                    shortNoteObj.transform.SetParent(environment);

                Renderer shortRenderer = shortNoteObj.GetComponent<Renderer>();
                if (shortRenderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.9f, 0.85f, 0.5f);
                    shortRenderer.material = mat;
                }
            }

            // Always reposition the grocery list onto the table (beside the journal)
            shortNoteObj.transform.position = new Vector3(13.5f, noteY, tableCenter.z + 0.1f);

            {
                InteractableNote shortNote = shortNoteObj.GetComponent<InteractableNote>();
                if (shortNote == null) shortNote = shortNoteObj.AddComponent<InteractableNote>();

                var serializedShort = new SerializedObject(shortNote);
                serializedShort.FindProperty("genericName").stringValue = "Piece of paper";
                serializedShort.FindProperty("noteTitle").stringValue = "Grocery List";
                serializedShort.FindProperty("noteText").stringValue =
                    "Milk, eggs, bread.\nDon't forget the bread this time.";
                serializedShort.FindProperty("promptText").stringValue = "Read list";
                serializedShort.FindProperty("displayAsFullSheet").boolValue = false;
                serializedShort.FindProperty("displaySeconds").floatValue = 8f;
                serializedShort.ApplyModifiedProperties();
            }

            // --- Sample Key ---
            GameObject keyObj = GameObject.Find("Sample_Key");
            if (keyObj == null)
            {
                keyObj = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                keyObj.name = "Sample_Key";
                keyObj.transform.localScale = new Vector3(0.05f, 0.15f, 0.05f);

                if (walls != null)
                    keyObj.transform.position = new Vector3(23, 1.1f, -100);
                else
                    keyObj.transform.position = new Vector3(2, 1.1f, -2);

                if (environment != null)
                    keyObj.transform.SetParent(environment);

                keyObj.AddComponent<InteractableKeyPickup>();

                Renderer renderer = keyObj.GetComponent<Renderer>();
                if (renderer != null)
                {
                    Material mat = new Material(Shader.Find("Universal Render Pipeline/Lit"));
                    mat.color = new Color(0.85f, 0.7f, 0.2f);
                    mat.SetFloat("_Metallic", 0.8f);
                    mat.SetFloat("_Smoothness", 0.9f);
                    renderer.material = mat;
                }
            }

            // Always configure the sample key (even if the object already exists)
            if (keyObj.GetComponent<InteractableKeyPickup>() == null)
            {
                keyObj.AddComponent<InteractableKeyPickup>();
            }
            {
                var key = keyObj.GetComponent<InteractableKeyPickup>();
                var serializedKey = new SerializedObject(key);
                serializedKey.FindProperty("genericName").stringValue = "Key";
                serializedKey.FindProperty("keyName").stringValue = "Bedroom Key";
                serializedKey.FindProperty("mustExamineBeforePickup").boolValue = true;
                serializedKey.FindProperty("keyId").stringValue = "key_bedroom";
                serializedKey.FindProperty("promptText").stringValue = "Inspect key";
                serializedKey.FindProperty("description").stringValue =
                    "A small brass key with an ornate bow. The tag on it reads 'Bedroom'.";
                serializedKey.ApplyModifiedProperties();
            }

            // Always reposition the key onto the table (beside the notes), sitting on the table top
            keyObj.transform.localScale = new Vector3(0.06f, 0.18f, 0.06f);
            keyObj.transform.position = new Vector3(tableCenter.x, tableTopY + 0.18f, tableCenter.z - 0.1f);
        }
    }
}
