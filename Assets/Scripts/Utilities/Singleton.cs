using UnityEngine;

namespace HollowCreek.Utilities
{
    /// <summary>
    /// Generic singleton base class for MonoBehaviours.
    /// Attach to a GameObject in the scene to make it the persistent instance.
    /// </summary>
    public abstract class Singleton<T> : MonoBehaviour where T : MonoBehaviour
    {
        private static T instance;
        private static readonly object lockObject = new object();

        /// <summary>
        /// The single instance of this MonoBehaviour.
        /// Returns null if no instance exists in the scene.
        /// </summary>
        public static T Instance
        {
            get
            {
                lock (lockObject)
                {
                    if (instance == null)
                    {
                        instance = FindAnyObjectByType<T>();
                    }

                    return instance;
                }
            }
        }

        /// <summary>
        /// Whether this instance is the active singleton.
        /// </summary>
        public bool IsInstance => instance == this;

        protected virtual void Awake()
        {
            if (instance != null && instance != this)
            {
                Debug.LogWarning($"[Singleton] Duplicate {typeof(T)} detected on {gameObject.name}. Destroying duplicate.");
                Destroy(gameObject);
                return;
            }

            instance = this as T;
        }
    }
}
