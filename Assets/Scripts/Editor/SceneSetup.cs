using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Editor utility to set up the Hollow Creek scene structure.
    /// Access via menu: Tools > Hollow Creek > Setup Scene.
    /// </summary>
    public static class SceneSetup
    {
        [MenuItem("Tools/Hollow Creek/Setup Scene")]
        public static void SetupScene()
        {
            // Create hierarchy folders
            Transform environmentParent = CreateParent("--- Environment ---");
            Transform playerParent = CreateParent("--- Player ---");
            Transform uiParent = CreateParent("--- UI ---");
            Transform managersParent = CreateParent("--- Managers ---");

            // Move existing objects into folders
            MoveToParent("Directional Light", environmentParent);
            MoveToParent("Main Camera", null); // Will be removed, camera is child of Player
            MoveToParent("Canvas", uiParent);
            MoveToParent("EventSystem", uiParent);

            // Create floor
            if (GameObject.Find("Floor") == null)
            {
                GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
                floor.name = "Floor";
                floor.transform.SetParent(environmentParent);
                floor.transform.position = Vector3.zero;
                floor.transform.localScale = new Vector3(5, 1, 5);
            }
            MoveToParent("Floor", environmentParent);

            // Create Global Volume
            GameObject volumeObj = GameObject.Find("Global Volume");
            if (volumeObj == null)
            {
                volumeObj = new GameObject("Global Volume");
            }
            Volume volume = volumeObj.GetComponent<Volume>();
            if (volume == null)
            {
                volume = volumeObj.AddComponent<Volume>();
            }
            volume.isGlobal = true;

            VolumeProfile profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Settings/SampleSceneProfile.asset");
            if (profile != null)
            {
                volume.sharedProfile = profile;
            }
            MoveToParent("Global Volume", environmentParent);

            // Create player
            GameObject player = GameObject.Find("Player");
            if (player == null)
            {
                player = new GameObject("Player");
                player.AddComponent<CharacterController>();
                player.GetComponent<CharacterController>().height = 2f;
                player.GetComponent<CharacterController>().center = new Vector3(0, 1, 0);
                player.GetComponent<CharacterController>().radius = 0.4f;
            }
            player.transform.SetParent(playerParent);
            player.transform.position = new Vector3(0, 1, 0);

            // Create camera as child of player
            Transform cameraTransform = player.transform.Find("Camera");
            GameObject cameraObj;
            if (cameraTransform == null)
            {
                cameraObj = new GameObject("Camera");
                cameraObj.transform.SetParent(player.transform);
                cameraObj.transform.localPosition = Vector3.zero;
                cameraObj.AddComponent<Camera>();
                cameraObj.AddComponent<AudioListener>();
            }
            else
            {
                cameraObj = cameraTransform.gameObject;
            }
            cameraObj.tag = "MainCamera";

            // Add scripts
            if (player.GetComponent<HollowCreek.Player.PlayerController>() == null)
            {
                player.AddComponent<HollowCreek.Player.PlayerController>();
            }
            if (cameraObj.GetComponent<HollowCreek.Player.CameraLook>() == null)
            {
                cameraObj.AddComponent<HollowCreek.Player.CameraLook>();
            }

            // Assign input actions asset
            InputActionAsset inputActions = AssetDatabase.LoadAssetAtPath<InputActionAsset>("Assets/InputSystem_Actions.inputactions");
            if (inputActions != null)
            {
                var playerController = player.GetComponent<HollowCreek.Player.PlayerController>();
                var serializedPlayer = new SerializedObject(playerController);
                serializedPlayer.FindProperty("inputActions").objectReferenceValue = inputActions;
                serializedPlayer.ApplyModifiedProperties();

                var cameraLook = cameraObj.GetComponent<HollowCreek.Player.CameraLook>();
                var serializedCamera = new SerializedObject(cameraLook);
                serializedCamera.FindProperty("inputActions").objectReferenceValue = inputActions;
                serializedCamera.ApplyModifiedProperties();
            }

            // Create managers
            if (GameObject.Find("GameManager") == null)
            {
                new GameObject("GameManager");
            }
            MoveToParent("GameManager", managersParent);

            if (GameObject.Find("AudioManager") == null)
            {
                new GameObject("AudioManager");
            }
            MoveToParent("AudioManager", managersParent);

            if (GameObject.Find("TimeManager") == null)
            {
                new GameObject("TimeManager");
            }
            MoveToParent("TimeManager", managersParent);

            // Clean up default camera
            GameObject defaultCamera = GameObject.Find("Main Camera");
            if (defaultCamera != null && defaultCamera != cameraObj)
            {
                Object.DestroyImmediate(defaultCamera);
            }

            Debug.Log("[Hollow Creek] Scene setup complete!");
        }

        private static Transform CreateParent(string name)
        {
            GameObject parent = GameObject.Find(name);
            if (parent == null)
            {
                parent = new GameObject(name);
            }
            return parent.transform;
        }

        private static void MoveToParent(string objectName, Transform parent)
        {
            GameObject obj = GameObject.Find(objectName);
            if (obj != null)
            {
                obj.transform.SetParent(parent);
            }
        }
    }
}
