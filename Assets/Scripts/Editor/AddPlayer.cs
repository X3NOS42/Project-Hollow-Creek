using UnityEngine;
using UnityEditor;
using UnityEngine.InputSystem;

namespace HollowCreek.Editor
{
    /// <summary>
    /// Adds a player capsule with all scripts to the current scene.
    /// Access via menu: Tools > Hollow Creek > Add Player.
    /// </summary>
    public static class AddPlayer
    {
        [MenuItem("Tools/Hollow Creek/Add Player")]
        public static void AddPlayerToScene()
        {
            // Check if player already exists
            if (GameObject.Find("Player") != null)
            {
                Debug.LogWarning("[Hollow Creek] Player already exists in the scene.");
                Selection.activeGameObject = GameObject.Find("Player");
                return;
            }

            // Create player parent
            GameObject player = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            player.name = "Player";
            player.transform.position = new Vector3(0, 0, 0);

            // Hide capsule mesh — visible in Scene view for debugging, invisible in Game
            Renderer renderer = player.GetComponent<Renderer>();
            if (renderer != null)
            {
                renderer.enabled = false;
            }

            // Keep collider for reference but disable it (CharacterController replaces it)
            CapsuleCollider capsule = player.GetComponent<CapsuleCollider>();
            if (capsule != null)
            {
                Object.DestroyImmediate(capsule);
            }

            CharacterController cc = player.AddComponent<CharacterController>();
            cc.height = 2f;
            cc.center = new Vector3(0, 1, 0);
            cc.radius = 0.4f;

            // Create camera as child
            GameObject cameraObj = new GameObject("Camera");
            cameraObj.transform.SetParent(player.transform);
            cameraObj.transform.localPosition = new Vector3(0, 1.6f, 0);
            cameraObj.tag = "MainCamera";

            Camera cam = cameraObj.AddComponent<Camera>();
            cam.nearClipPlane = 0.1f;
            cameraObj.AddComponent<AudioListener>();

            // Add scripts
            player.AddComponent<HollowCreek.Player.PlayerController>();
            cameraObj.AddComponent<HollowCreek.Player.CameraLook>();

            // Assign input actions
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

            // Select the player
            Selection.activeGameObject = player;

            Debug.Log("[Hollow Creek] Player added! Press Play to test.");
        }
    }
}
