using UnityEngine;

namespace TuitionTakkar
{
    /// <summary>
    /// Keeps friends alive for signed-in players while the game runs: hands notifications that
    /// arrive with the game open to the game (pop-ups), uploads this phone's notification token
    /// as soon as Firebase has it, retries a friends start that failed, and catches up after the
    /// game comes back from the background.
    /// </summary>
    public class SocialRunner : MonoBehaviour
    {
        static SocialRunner instance;
        GameManager gm;
        float nextTick, nextBoot;

        public static void Ensure(GameManager gm)
        {
            if (instance != null) return;
            var go = new GameObject("Social");
            DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideInHierarchy;
            instance = go.AddComponent<SocialRunner>();
            instance.gm = gm;
            Push.SetForeground(true);
        }

        void Update()
        {
            if (gm == null || Time.realtimeSinceStartup < nextTick) return;
            nextTick = Time.realtimeSinceStartup + 1f;

            foreach (var d in Push.TakeInbox())
            {
                try { Friends.FromPush(d); }
                catch (System.Exception e) { Debug.LogException(e); }
            }

            if (!Friends.Allowed(gm.Save)) return;
            Friends.UploadPushToken();
            if (!Friends.Ready && Time.realtimeSinceStartup >= nextBoot)
            {
                nextBoot = Time.realtimeSinceStartup + 30f;
                Friends.Boot(gm);
            }
        }

        void OnApplicationPause(bool paused)
        {
            Push.SetForeground(!paused);
            if (paused || gm == null) return;
            Push.CheckTappedLink(); // opened again by tapping an invite / friend notification
            if (Friends.Allowed(gm.Save)) Friends.Refresh(gm);
        }

        void OnApplicationFocus(bool focus)
        {
            if (focus) Push.SetForeground(true);
        }
    }
}
