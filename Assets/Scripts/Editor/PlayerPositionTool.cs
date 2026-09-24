using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Reflection;
using HollowCreek.Player;
using HollowCreek.Utilities;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Helper window for saving named player positions and teleporting the player
    /// to them while testing. Locations are grouped into categories (e.g. Testing).
    /// Open it with: Tools > Hollow Creek > Player Position Tool
    /// In Play mode, press F8 to save the current spot instantly.
    /// </summary>
    [InitializeOnLoad]
    public class PlayerPositionTool : EditorWindow
    {
        private const string PrefsPrefix = "HollowCreek.PlayerPositions.";
        private const string LastCategoryKey = PrefsPrefix + "LastCategory";
        private const string FilterAll = "All";
        private const string DefaultCategory = "Testing";
        private const string OtherCategory = "Other";
        private const string CategoriesFieldName = "PlayerPositionCategories";

        private static readonly string[] SeedCategories = { "Testing", "Other" };

        private PlayerPositionStore.LocationListData data = new PlayerPositionStore.LocationListData();
        private string newLocationName = "";
        private string categoriesText = "";
        private string filterCategory = FilterAll;
        private bool showManageCategories;
        private Vector2 scroll;

        static PlayerPositionTool()
        {
            EditorApplication.update += PollPendingSave;
        }

        [MenuItem("Tools/Hollow Creek/Player Position Tool")]
        private static void Open()
        {
            PlayerPositionTool window = GetWindow<PlayerPositionTool>("Player Positions");
            window.minSize = new Vector2(780, 260);
        }

        private static string PrefsKey { get { return PrefsPrefix + ProjectId; } }

        private static string ProjectId
        {
            get
            {
                using (var md5 = System.Security.Cryptography.MD5.Create())
                {
                    byte[] hash = md5.ComputeHash(System.Text.Encoding.UTF8.GetBytes(Application.dataPath));
                    return System.BitConverter.ToString(hash).Replace("-", "").Substring(0, 12);
                }
            }
        }

        private static PlayerPositionStore.LocationListData LoadData()
        {
            string json = EditorPrefs.GetString(PrefsKey, "{}");
            PlayerPositionStore.LocationListData list;
            try
            {
                list = JsonUtility.FromJson<PlayerPositionStore.LocationListData>(json);
            }
            catch
            {
                list = null;
            }
            if (list == null)
            {
                list = new PlayerPositionStore.LocationListData();
            }
            if (list.locations == null)
            {
                list.locations = new List<PlayerPositionStore.LocationData>();
            }
            if (list.categories == null)
            {
                list.categories = new List<string>();
            }

            // Seed categories on first run; keep the list in sync with used ones.
            bool changed = false;
            if (list.categories.Count == 0)
            {
                list.categories.AddRange(SeedCategories);
                changed = true;
            }
            foreach (var loc in list.locations)
            {
                if (string.IsNullOrWhiteSpace(loc.category))
                {
                    loc.category = DefaultCategory;
                    changed = true;
                }
                if (!list.categories.Contains(loc.category))
                {
                    list.categories.Add(loc.category);
                    changed = true;
                }
            }
            if (changed)
            {
                SaveData(list);
            }
            return list;
        }

        private static void SaveData(PlayerPositionStore.LocationListData list)
        {
            EditorPrefs.SetString(PrefsKey, JsonUtility.ToJson(list));
        }

        private static string LastCategory
        {
            get { return EditorPrefs.GetString(LastCategoryKey, DefaultCategory); }
            set { EditorPrefs.SetString(LastCategoryKey, value); }
        }

        private static string SafeCategory(List<string> categories, string preferred)
        {
            if (!string.IsNullOrEmpty(preferred) && categories.Contains(preferred))
            {
                return preferred;
            }
            if (categories.Contains(DefaultCategory))
            {
                return DefaultCategory;
            }
            if (categories.Contains(OtherCategory))
            {
                return OtherCategory;
            }
            return categories.Count > 0 ? categories[0] : DefaultCategory;
        }

        private static void PollPendingSave()
        {
            if (!PlayerPositionStore.HasPending)
            {
                return;
            }

            PlayerPositionStore.LocationData loc = PlayerPositionStore.PendingSave;
            PlayerPositionStore.PendingSave = null;
            PlayerPositionStore.HasPending = false;

            PlayerPositionStore.LocationListData list = LoadData();
            loc.category = SafeCategory(list.categories, LastCategory);

            list.locations.Add(loc);
            SaveData(list);
            PlayerPositionStore.NotifyChanged();

            Debug.Log($"[PlayerPositionTool] F8 saved '{loc.name}' in '{loc.category}' at {loc.position}, facing {GetLookYaw(loc):F0}deg, pitch {GetLookPitch(loc):F0}deg. Rename it in the window.");
        }

        private void OnEnable()
        {
            PlayerPositionStore.Changed += OnStoreChanged;
            Reload();
        }

        private void OnDisable()
        {
            PlayerPositionStore.Changed -= OnStoreChanged;
        }

        private void OnStoreChanged()
        {
            Reload();
            Repaint();
        }

        private void Reload()
        {
            data = LoadData();
        }

        private static Transform FindPlayer()
        {
            return GameObject.Find("Player")?.transform;
        }

        private void OnGUI()
        {
            Transform player = FindPlayer();
            if (player == null)
            {
                EditorGUILayout.HelpBox(
                    "No 'Player' found in the scene. Open a scene with the player in it (enter Play mode to record the runtime player).",
                    MessageType.Warning);
            }
            else
            {
                EnsureShortcut(player);
            }

            EditorGUILayout.HelpBox(
                "In Play mode, press F8 to save the player's current spot instantly. Rename any entry below.",
                MessageType.Info);

            EditorGUILayout.Space(4);
            EditorGUILayout.LabelField("Add a location", EditorStyles.boldLabel);

            EditorGUILayout.BeginHorizontal();
            newLocationName = EditorGUILayout.TextField(newLocationName);
            EditorGUILayout.LabelField("Category", GUILayout.Width(70));
            int seedIndex = Mathf.Max(0, data.categories.IndexOf(SafeCategory(data.categories, LastCategory)));
            int chosen = EditorGUILayout.Popup(seedIndex, data.categories.ToArray(), GUILayout.Width(150));
            if (chosen >= 0 && chosen < data.categories.Count)
            {
                LastCategory = data.categories[chosen];
            }
            if (GUILayout.Button("Save Current Position", GUILayout.Width(180)) && player != null)
            {
                AddLocation(player);
            }
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.Space(8);
            showManageCategories = EditorGUILayout.Foldout(showManageCategories, "Manage categories", true);
            if (showManageCategories)
            {
                EditorGUI.indentLevel++;
                EditorGUILayout.HelpBox(
                    "One category per line. Add a line to create a category; delete a line to remove it.",
                    MessageType.None);

                GUI.SetNextControlName(CategoriesFieldName);
                bool changedBefore = GUI.changed;
                categoriesText = EditorGUILayout.TextArea(categoriesText, GUILayout.Height(80));
                bool textChanged = GUI.changed != changedBefore;

                bool isFocused = GUI.GetNameOfFocusedControl() == CategoriesFieldName;
                if (!isFocused)
                {
                    categoriesText = SerializeCategories(data.categories);
                }
                else if (textChanged)
                {
                    ApplyCategoriesFromText();
                }
                EditorGUI.indentLevel--;
            }

            EditorGUILayout.Space(8);
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Saved locations", EditorStyles.boldLabel);
            GUILayout.FlexibleSpace();
            EditorGUILayout.LabelField("Filter", GUILayout.Width(40));
            List<string> filterOptions = new List<string>();
            filterOptions.Add(FilterAll);
            filterOptions.AddRange(data.categories);
            int filterIndex = Mathf.Max(0, filterOptions.IndexOf(filterCategory));
            int newFilterIndex = EditorGUILayout.Popup(filterIndex, filterOptions.ToArray(), GUILayout.Width(150));
            if (newFilterIndex >= 0 && newFilterIndex < filterOptions.Count)
            {
                filterCategory = filterOptions[newFilterIndex];
            }
            EditorGUILayout.EndHorizontal();

            if (data.locations.Count == 0)
            {
                EditorGUILayout.HelpBox("No saved locations yet.", MessageType.Info);
            }

            scroll = EditorGUILayout.BeginScrollView(scroll);
            for (int i = 0; i < data.locations.Count; i++)
            {
                PlayerPositionStore.LocationData loc = data.locations[i];
                if (filterCategory != FilterAll && loc.category != filterCategory)
                {
                    continue;
                }
                DrawLocationRow(i, player);
            }
            EditorGUILayout.EndScrollView();
        }

        private static string SerializeCategories(List<string> categories)
        {
            return string.Join("\n", categories);
        }

        private void ApplyCategoriesFromText()
        {
            List<string> newCategories = new List<string>();
            string[] lines = categoriesText.Split(new[] { '\n', '\r' }, System.StringSplitOptions.RemoveEmptyEntries);
            foreach (string rawLine in lines)
            {
                string trimmed = rawLine.Trim();
                if (trimmed.Length > 0 && !newCategories.Contains(trimmed))
                {
                    newCategories.Add(trimmed);
                }
            }

            if (newCategories.Count == 0)
            {
                newCategories.Add(DefaultCategory);
            }

            bool added = false;
            foreach (string cat in newCategories)
            {
                if (!data.categories.Contains(cat))
                {
                    added = true;
                }
            }
            bool removed = false;
            foreach (string cat in data.categories)
            {
                if (!newCategories.Contains(cat))
                {
                    removed = true;
                }
            }

            if (!added && !removed)
            {
                return;
            }

            string fallback = SafeCategory(newCategories, OtherCategory);
            foreach (var loc in data.locations)
            {
                if (!newCategories.Contains(loc.category))
                {
                    loc.category = fallback;
                }
            }

            data.categories = newCategories;
            if (!newCategories.Contains(LastCategory))
            {
                LastCategory = SafeCategory(newCategories, DefaultCategory);
            }
            if (filterCategory != FilterAll && !newCategories.Contains(filterCategory))
            {
                filterCategory = FilterAll;
            }

            SaveData(data);
            PlayerPositionStore.NotifyChanged();
            Repaint();
        }

        private static void EnsureShortcut(Transform player)
        {
            if (player.GetComponent<PlayerPositionShortcut>() != null)
            {
                return;
            }

            player.gameObject.AddComponent<PlayerPositionShortcut>();
            Debug.Log("[PlayerPositionTool] Added PlayerPositionShortcut to the Player (F8 saves position).");
        }

        private void AddLocation(Transform player)
        {
            if (player == null)
            {
                Debug.LogWarning("[PlayerPositionTool] No Player found to save.");
                return;
            }

            string name = string.IsNullOrWhiteSpace(newLocationName)
                ? "Location " + (data.locations.Count + 1)
                : newLocationName.Trim();

            var loc = CaptureLocation(player, name);
            loc.category = SafeCategory(data.categories, LastCategory);
            data.locations.Add(loc);
            SaveData(data);
            PlayerPositionStore.NotifyChanged();

            Debug.Log($"[PlayerPositionTool] Saved '{loc.name}' in '{loc.category}' at {loc.position}, facing {GetLookYaw(loc):F0}deg, pitch {GetLookPitch(loc):F0}deg.");
            newLocationName = "";
        }

        private static PlayerPositionStore.LocationData CaptureLocation(Transform player, string name)
        {
            return new PlayerPositionStore.LocationData
            {
                name = name,
                position = player.position,
                playerRotation = player.eulerAngles,
                cameraLocalRotation = GetCameraLocalRotation(player)
            };
        }

        private static Vector3 GetCameraLocalRotation(Transform player)
        {
            Transform cameraTransform = player != null ? player.Find("Camera") : null;
            return cameraTransform != null ? cameraTransform.localEulerAngles : Vector3.zero;
        }

        private static float GetLookYaw(PlayerPositionStore.LocationData loc)
        {
            return NormalizeAngle(loc.playerRotation.y);
        }

        private static float GetLookPitch(PlayerPositionStore.LocationData loc)
        {
            float pitch = NormalizeAngle(loc.cameraLocalRotation.x);
            return pitch > 180f ? pitch - 360f : pitch;
        }

        private static float NormalizeAngle(float angle)
        {
            angle %= 360f;
            return angle < 0f ? angle + 360f : angle;
        }

        private void DrawLocationRow(int index, Transform player)
        {
            PlayerPositionStore.LocationData loc = data.locations[index];

            EditorGUILayout.BeginHorizontal();

            int categoryIndex = Mathf.Max(0, data.categories.IndexOf(loc.category));
            int newCategoryIndex = EditorGUILayout.Popup(categoryIndex, data.categories.ToArray(), GUILayout.Width(110));
            if (newCategoryIndex >= 0 && newCategoryIndex < data.categories.Count &&
                data.categories[newCategoryIndex] != loc.category)
            {
                loc.category = data.categories[newCategoryIndex];
                SaveData(data);
            }

            string editedName = EditorGUILayout.TextField(loc.name, GUILayout.Width(180));
            if (editedName != loc.name)
            {
                loc.name = editedName;
                SaveData(data);
            }

            GUILayout.Label(
                string.Format("({0:F1}, {1:F1}, {2:F1})", loc.position.x, loc.position.y, loc.position.z),
                GUILayout.Width(150));

            GUILayout.Label(
                string.Format("Facing {0:F0}deg  Pitch {1:F0}deg", GetLookYaw(loc), GetLookPitch(loc)),
                GUILayout.Width(150));

            if (GUILayout.Button("Move Player", GUILayout.Width(84)))
            {
                MovePlayerTo(loc, player);
            }

            if (GUILayout.Button("X", GUILayout.Width(22)))
            {
                data.locations.RemoveAt(index);
                SaveData(data);
                PlayerPositionStore.NotifyChanged();
            }

            EditorGUILayout.EndHorizontal();
        }

        private void MovePlayerTo(PlayerPositionStore.LocationData loc, Transform player)
        {
            if (player == null)
            {
                Debug.LogWarning("[PlayerPositionTool] No Player found to move.");
                return;
            }

            if (!Application.isPlaying)
            {
                Undo.RecordObject(player, "Move Player");
            }

            // The player has a CharacterController; teleporting with transform.position
            // while it is active doesn't stick (it snaps back on the next Move).
            // Disable it, move, then re-enable it in the same frame.
            CharacterController cc = player.GetComponent<CharacterController>();
            if (cc != null)
            {
                cc.enabled = false;
            }

            player.position = loc.position;
            player.rotation = Quaternion.Euler(loc.playerRotation);

            Transform cameraTransform = player.Find("Camera");
            if (cameraTransform != null)
            {
                // Set the camera's local rotation and also sync CameraLook's private
                // xRotation field, otherwise it overwrites the pitch next frame.
                cameraTransform.localRotation = Quaternion.Euler(loc.cameraLocalRotation);
                RestoreCameraPitch(cameraTransform);
            }

            if (cc != null)
            {
                cc.enabled = true;
            }

            Debug.Log($"[PlayerPositionTool] Moved player to '{loc.name}' at {loc.position}.");
        }

        private static void RestoreCameraPitch(Transform cameraTransform)
        {
            CameraLook look = cameraTransform.GetComponent<CameraLook>();
            if (look == null)
            {
                return;
            }

            FieldInfo pitchField = typeof(CameraLook).GetField("xRotation", BindingFlags.Instance | BindingFlags.NonPublic);
            if (pitchField == null)
            {
                return;
            }

            try
            {
                float pitch = cameraTransform.localEulerAngles.x;
                if (pitch > 180f)
                {
                    pitch -= 360f;
                }
                pitchField.SetValue(look, pitch);
            }
            catch
            {
                // Ignore reflection issues; the local rotation is already set.
            }
        }
    }
}