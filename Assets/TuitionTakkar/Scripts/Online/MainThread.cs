using System;
using System.Collections.Concurrent;
using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Runs callbacks from Android (background threads) on Unity's main thread.</summary>
    public class MainThread : MonoBehaviour
    {
        static readonly ConcurrentQueue<Action> queue = new ConcurrentQueue<Action>();
        static MainThread instance;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Boot()
        {
            if (instance != null) return;
            var go = new GameObject("MainThread");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            instance = go.AddComponent<MainThread>();
        }

        public static void Run(Action a)
        {
            if (a != null) queue.Enqueue(a);
        }

        void Update()
        {
            while (queue.TryDequeue(out var a))
            {
                try { a(); }
                catch (Exception e) { Debug.LogException(e); }
            }
        }
    }
}
