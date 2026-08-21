using System;
using System.IO;
using System.Text;
using UnityEditor.Android;
using UnityEngine;

namespace JellyGate.Editor
{
    /// <summary>
    /// Keeps the Unity project free of generated Maven binaries while producing a Play-ready
    /// Gradle project with Billing 9.1, Google Mobile Ads 25.4, Unity Ads 4.19,
    /// the Google Unity mediation adapter 4.19.0.1, UMP consent 4.0 and login-free
    /// Android backup rules for the portable challenge/run checkpoint file.
    /// </summary>
    public sealed class GooglePlayAndroidPostprocessor : IPostGenerateGradleAndroidProject
    {
        [Serializable]
        private sealed class GoogleServicesConfig
        {
            public bool useTestAds = true;
            public string adMobAppId = string.Empty;
        }

        private const string TestAdMobAppId = "ca-app-pub-3940256099942544~3347511713";
        public int callbackOrder => 50;

        public void OnPostGenerateGradleAndroidProject(string path)
        {
            var unityLibrary = Directory.Exists(Path.Combine(path, "unityLibrary"))
                ? Path.Combine(path, "unityLibrary")
                : path;
            var config = LoadGoogleServicesConfig();
            PatchGradle(Path.Combine(unityLibrary, "build.gradle"));
            WriteBackupResources(unityLibrary);
            PatchManifest(Path.Combine(unityLibrary, "src", "main", "AndroidManifest.xml"), config);
            CopyBridge(Path.Combine(unityLibrary, "src", "main", "java", "com", "crownfront",
                "monetization", "CrownfrontMonetizationBridge.java"));
        }

        private static void PatchGradle(string gradlePath)
        {
            if (!File.Exists(gradlePath))
            {
                Debug.LogWarning($"Crownfront monetization: Gradle file not found at {gradlePath}");
                return;
            }
            var text = File.ReadAllText(gradlePath);
            const string billing = "implementation 'com.android.billingclient:billing:9.1.0'";
            const string ads = "implementation 'com.google.android.gms:play-services-ads:25.4.0'";
            const string unityAds = "implementation 'com.unity3d.ads:unity-ads:4.19.0'";
            const string unityAdapter = "implementation 'com.google.ads.mediation:unity:4.19.0.1'";
            const string ump = "implementation 'com.google.android.ump:user-messaging-platform:4.0.0'";
            foreach (var dependency in new[]
                     {
                         billing, ads, unityAds, unityAdapter, ump,
                         "implementation 'com.google.android.gms:play-services-" + "games-v2:21.0.0'"
                     })
            {
                text = text.Replace("    " + dependency + "\r\n", string.Empty)
                    .Replace("    " + dependency + "\n", string.Empty);
            }
            var dependencyIndex = text.IndexOf("dependencies {", System.StringComparison.Ordinal);
            if (dependencyIndex < 0)
            {
                Debug.LogWarning("Crownfront monetization: dependencies block was not found.");
                return;
            }
            var insertAt = dependencyIndex + "dependencies {".Length;
            text = text.Insert(insertAt,
                $"\n    {billing}\n    {ads}\n    {unityAds}\n    {unityAdapter}\n    {ump}\n");
            File.WriteAllText(gradlePath, text, new UTF8Encoding(false));
        }

        private static GoogleServicesConfig LoadGoogleServicesConfig()
        {
            var path = Path.Combine(Application.dataPath, "Resources", "crownfront-google-services.json");
            GoogleServicesConfig config = null;
            if (File.Exists(path))
            {
                try
                {
                    config = JsonUtility.FromJson<GoogleServicesConfig>(File.ReadAllText(path));
                }
                catch (Exception exception)
                {
                    Debug.LogError($"Crownfront Google services config is invalid: {exception.Message}");
                }
            }
            config ??= new GoogleServicesConfig();
            return config;
        }

        private static void WriteBackupResources(string unityLibrary)
        {
            var xmlDirectory = Path.Combine(unityLibrary, "src", "main", "res", "xml");
            Directory.CreateDirectory(xmlDirectory);
            const string legacyRules = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<full-backup-content>\n" +
                "    <include domain=\"external\" path=\"crownfront_portable_progress_v1.json\" />\n" +
                "</full-backup-content>\n";
            const string modernRules = "<?xml version=\"1.0\" encoding=\"utf-8\"?>\n" +
                "<data-extraction-rules>\n" +
                "    <cloud-backup disableIfNoEncryptionCapabilities=\"false\">\n" +
                "        <include domain=\"external\" path=\"crownfront_portable_progress_v1.json\" />\n" +
                "    </cloud-backup>\n" +
                "    <device-transfer>\n" +
                "        <include domain=\"external\" path=\"crownfront_portable_progress_v1.json\" />\n" +
                "    </device-transfer>\n" +
                "</data-extraction-rules>\n";
            File.WriteAllText(Path.Combine(xmlDirectory, "crownfront_backup_rules.xml"), legacyRules,
                new UTF8Encoding(false));
            File.WriteAllText(Path.Combine(xmlDirectory, "crownfront_data_extraction_rules.xml"), modernRules,
                new UTF8Encoding(false));
        }

        private static void PatchManifest(string manifestPath, GoogleServicesConfig config)
        {
            if (!File.Exists(manifestPath)) return;
            var text = File.ReadAllText(manifestPath);
            if (!text.Contains("com.android.vending.BILLING"))
                text = text.Replace("<application", "<uses-permission android:name=\"com.android.vending.BILLING\" />\n    <application");
            if (!text.Contains("android:allowBackup="))
                text = text.Replace("<application", "<application android:allowBackup=\"true\"");
            if (!text.Contains("android:fullBackupContent="))
                text = text.Replace("<application", "<application android:fullBackupContent=\"@xml/crownfront_backup_rules\"");
            if (!text.Contains("android:dataExtractionRules="))
                text = text.Replace("<application", "<application android:dataExtractionRules=\"@xml/crownfront_data_extraction_rules\"");
            if (!text.Contains("com.google.android.gms.ads.APPLICATION_ID"))
            {
                var adMobAppId = config != null && !config.useTestAds &&
                                 !string.IsNullOrWhiteSpace(config.adMobAppId)
                    ? config.adMobAppId.Trim()
                    : TestAdMobAppId;
                text = text.Replace("</application>",
                    "        <meta-data android:name=\"com.google.android.gms.ads.APPLICATION_ID\" " +
                    $"android:value=\"{adMobAppId}\" />\n    </application>");
            }
            File.WriteAllText(manifestPath, text, new UTF8Encoding(false));
        }

        private static void CopyBridge(string targetPath)
        {
            var sourcePath = Path.Combine(Application.dataPath, "Plugins", "Android",
                "CrownfrontMonetizationBridge.java.txt");
            if (!File.Exists(sourcePath))
            {
                Debug.LogError($"Crownfront monetization bridge template is missing: {sourcePath}");
                return;
            }
            Directory.CreateDirectory(Path.GetDirectoryName(targetPath) ?? string.Empty);
            File.Copy(sourcePath, targetPath, true);
        }
    }
}
