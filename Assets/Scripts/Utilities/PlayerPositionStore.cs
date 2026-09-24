using System.Collections.Generic;
using UnityEngine;

namespace HollowCreek.Utilities
{
    /// <summary>
    /// Runtime-visible data + handoff for the Player Position Tool.
    /// Keep this file free of UnityEditor references so it works in builds.
    /// </summary>
    public static class PlayerPositionStore
    {
        [System.Serializable]
        public class LocationData
        {
            public string name = "";
            public string category = "Testing";
            public Vector3 position;
            public Vector3 playerRotation;
            public Vector3 cameraLocalRotation;
        }

        [System.Serializable]
        public class LocationListData
        {
            public List<LocationData> locations = new List<LocationData>();
            public List<string> categories = new List<string>();
        }

        /// <summary>Raised when the stored list changes (editor listens to refresh its window).</summary>
        public static event System.Action Changed;

        public static void NotifyChanged()
        {
            Changed?.Invoke();
        }

        /// <summary>
        /// Set by PlayerPositionShortcut (F8, in the game loop) and picked up by the
        /// editor window which writes it to EditorPrefs.
        /// </summary>
        public static LocationData PendingSave;
        public static bool HasPending;
    }
}