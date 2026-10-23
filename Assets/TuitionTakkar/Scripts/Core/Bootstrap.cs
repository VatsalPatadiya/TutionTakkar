using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>Makes sure the game starts even from an empty scene.</summary>
    public static class Bootstrap
    {
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Init()
        {
            if (Object.FindFirstObjectByType<GameManager>() != null) return;
            new GameObject("TuitionTakkar").AddComponent<GameManager>();
        }
    }
}
