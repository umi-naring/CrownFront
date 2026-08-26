using GooglePlayGames.Editor;
using UnityEditor;
using UnityEngine;

namespace JellyGate.Editor
{
    /// <summary>Repeatable, headless-safe setup for Crownfront's Play Games Services project.</summary>
    public static class CrownfrontPlayGamesSetup
    {
        public const string AppId = "228925673337";

        [MenuItem("Crownfront/Configure Google Play Games")]
        public static void Configure()
        {
            var success = GPGSAndroidSetupUI.PerformSetup(string.Empty, AppId, null);
            if (!success)
                throw new System.InvalidOperationException("Google Play Games setup failed for app " + AppId);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
            Debug.Log("CROWNFRONT_GPGS_SETUP appId=" + AppId + " configured=true");
        }
    }
}
