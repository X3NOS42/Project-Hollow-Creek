using System.Collections.Generic;
using UnityEngine;

namespace HollowCreek.Story
{
    /// <summary>
    /// Central game state manager. Tracks discovered keys, clues, and story progression.
    /// Persists across scenes as a singleton.
    /// </summary>
    public class GameManager : HollowCreek.Utilities.Singleton<GameManager>
    {
        [Header("Debug")]
        [SerializeField] private bool debugMode;

        private List<string> foundKeys = new List<string>();
        private List<string> discoveredClues = new List<string>();
        private List<string> unlockedDoors = new List<string>();

        /// <summary>
        /// Checks if the player has a specific key.
        /// </summary>
        public bool HasKey(string keyId)
        {
            return foundKeys.Contains(keyId);
        }

        /// <summary>
        /// Adds a key to the player's inventory.
        /// </summary>
        public void AddKey(string keyId)
        {
            if (!foundKeys.Contains(keyId))
            {
                foundKeys.Add(keyId);
                if (debugMode) Debug.Log($"[GameManager] Key added: {keyId}");
            }
        }

        /// <summary>
        /// Checks if a clue has been discovered.
        /// </summary>
        public bool HasClue(string clueId)
        {
            return discoveredClues.Contains(clueId);
        }

        /// <summary>
        /// Records a discovered clue.
        /// </summary>
        public void DiscoverClue(string clueId)
        {
            if (!discoveredClues.Contains(clueId))
            {
                discoveredClues.Add(clueId);
                if (debugMode) Debug.Log($"[GameManager] Clue discovered: {clueId}");
            }
        }

        /// <summary>
        /// Checks if a door has been unlocked.
        /// </summary>
        public bool IsDoorUnlocked(string doorId)
        {
            return unlockedDoors.Contains(doorId);
        }

        /// <summary>
        /// Unlocks a door.
        /// </summary>
        public void UnlockDoor(string doorId)
        {
            if (!unlockedDoors.Contains(doorId))
            {
                unlockedDoors.Add(doorId);
                if (debugMode) Debug.Log($"[GameManager] Door unlocked: {doorId}");
            }
        }

        /// <summary>
        /// Returns the number of keys found.
        /// </summary>
        public int GetKeyCount()
        {
            return foundKeys.Count;
        }

        /// <summary>
        /// Returns the number of clues discovered.
        /// </summary>
        public int GetClueCount()
        {
            return discoveredClues.Count;
        }
    }
}
