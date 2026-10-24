using UnityEditor;
using Unity.Notifications;

namespace TuitionTakkar.EditorTools
{
    /// <summary>
    /// Mobile Notifications project settings: never ask for permission at launch on iOS
    /// (the game asks after the first win), and keep Android reminders after a phone restart.
    /// </summary>
    [InitializeOnLoad]
    static class NotificationSetup
    {
        static NotificationSetup()
        {
            EditorApplication.delayCall += () =>
            {
                if (NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch)
                    NotificationSettings.iOSSettings.RequestAuthorizationOnAppLaunch = false;
                if (!NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart)
                    NotificationSettings.AndroidSettings.RescheduleOnDeviceRestart = true;
            };
        }
    }
}
