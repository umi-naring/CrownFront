using System;
using System.IO;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEditor.Android;
using UnityEngine;

namespace JellyGate.Editor
{
    [InitializeOnLoad]
    public static class JellyGateBuild
    {
        private const string MapAsset = "Assets/Resources/crownfront-casual-expanded-v1.png";
        private const string NavigationBlockMaskAsset = "Assets/Resources/navigation-block-mask-v1.png";
        private const string AppIconAsset = "Assets/Resources/app-icon-hero-shield-v1.png";
        private const string SceneAsset = "Assets/Scenes/SampleScene.unity";

        static JellyGateBuild()
        {
            EditorApplication.delayCall += ConfigureImportedArt;
        }

        [MenuItem("Jelly Gate/Configure Project")]
        public static void ConfigureProject()
        {
            ConfigureImportedArt();
            ConfigureAndroidAppIcon();

            PlayerSettings.companyName = "Toy Kingdom Studio";
            PlayerSettings.productName = "CROWNFRONT";
            PlayerSettings.bundleVersion = "1.00";
            // Google Play requires this internal integer to increase on every uploaded release.
            // Version codes 1-3 have already been used by prior Google Play uploads.
            // Keep the user-facing version name at 1.00 and publish this verified update as code 4.
            PlayerSettings.Android.bundleVersionCode = 4;
            PlayerSettings.SetApplicationIdentifier(NamedBuildTarget.Android, "com.toykingdom.jellygate");
            PlayerSettings.defaultInterfaceOrientation = UIOrientation.Portrait;
            PlayerSettings.Android.minSdkVersion = AndroidSdkVersions.AndroidApiLevel26;
            PlayerSettings.Android.targetSdkVersion = AndroidSdkVersions.AndroidApiLevelAuto;
            // Modern Android distribution is 64-bit; keeping one ABI also keeps the packaged APK
            // small enough for reliable on-device installation and Gradle packaging.
            PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            PlayerSettings.SetScriptingBackend(NamedBuildTarget.Android, ScriptingImplementation.IL2CPP);
            EditorUserBuildSettings.buildAppBundle = false;

            EditorBuildSettings.scenes = new[]
            {
                new EditorBuildSettingsScene(SceneAsset, true)
            };

            AssetDatabase.SaveAssets();
            Debug.Log("Jelly Gate project settings configured.");
        }

        [MenuItem("Jelly Gate/QA/Verify Android App Icon 2.70.9")]
        public static void VerifyAndroidAppIcon279()
        {
            ConfigureAndroidAppIcon();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconAsset);
            if (icon == null) throw new BuildFailedException($"App icon is missing: {AppIconAsset}");
            if (icon.width != icon.height || icon.width < 1024)
                throw new BuildFailedException($"App icon must be a square source of at least 1024 px: {icon.width}x{icon.height}");

            VerifyPlatformIconKind(AndroidPlatformIconKind.Legacy, "legacy", 1);
            VerifyPlatformIconKind(AndroidPlatformIconKind.Round, "round", 1);
            VerifyPlatformIconKind(AndroidPlatformIconKind.Adaptive, "adaptive", 2);
            AssetDatabase.SaveAssets();
            Debug.Log($"QA_APP_ICON_279_PASS asset={AppIconAsset} size={icon.width}x{icon.height} legacy=pass round=pass adaptive=pass");
        }

        private static void ConfigureAndroidAppIcon()
        {
            ConfigureAppIconTexture();
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(AppIconAsset);
            if (icon == null)
            {
                Debug.LogError($"Android app icon could not be loaded: {AppIconAsset}");
                return;
            }

            SetPlatformIconKind(AndroidPlatformIconKind.Legacy, icon, 1);
            SetPlatformIconKind(AndroidPlatformIconKind.Round, icon, 1);
            // Adaptive launchers require two layers. The edge-to-edge source deliberately has no baked
            // rounded corners, so using it for both layers remains safe under OEM circle/squircle masks.
            SetPlatformIconKind(AndroidPlatformIconKind.Adaptive, icon, 2);
        }

        private static void SetPlatformIconKind(PlatformIconKind kind, Texture2D icon, int layerCount)
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            for (var i = 0; i < slots.Length; i++)
            {
                var layers = new Texture2D[layerCount];
                for (var layer = 0; layer < layers.Length; layer++) layers[layer] = icon;
                slots[i].SetTextures(layers);
            }
            PlayerSettings.SetPlatformIcons(NamedBuildTarget.Android, kind, slots);
        }

        private static void VerifyPlatformIconKind(PlatformIconKind kind, string label, int expectedLayers)
        {
            var slots = PlayerSettings.GetPlatformIcons(NamedBuildTarget.Android, kind);
            if (slots == null || slots.Length == 0)
                throw new BuildFailedException($"Android {label} icon slots are unavailable.");
            for (var i = 0; i < slots.Length; i++)
            {
                var layers = slots[i].GetTextures();
                if (layers == null || layers.Length < expectedLayers)
                    throw new BuildFailedException($"Android {label} icon slot {i} has {layers?.Length ?? 0}/{expectedLayers} layers.");
                for (var layer = 0; layer < expectedLayers; layer++)
                {
                    if (layers[layer] == null || AssetDatabase.GetAssetPath(layers[layer]) != AppIconAsset)
                        throw new BuildFailedException($"Android {label} icon slot {i}, layer {layer} is not assigned to {AppIconAsset}.");
                }
            }
        }

        private static void ConfigureAppIconTexture()
        {
            if (AssetImporter.GetAtPath(AppIconAsset) is not TextureImporter importer) return;
            var changed = importer.textureType != TextureImporterType.Default ||
                          importer.mipmapEnabled || importer.maxTextureSize < 1024 ||
                          importer.alphaSource != TextureImporterAlphaSource.None;
            if (!changed) return;

            importer.textureType = TextureImporterType.Default;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }

        [MenuItem("Jelly Gate/Build Android APK")]
        public static void BuildAndroid()
        {
            ConfigureProject();
            var emulatorBuild = HasArgument("-emulatorBuild");
            if (emulatorBuild) PlayerSettings.Android.targetArchitectures = AndroidArchitecture.X86_64;

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support is not installed for this Unity editor.");

            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Could not switch the active build target to Android.");

            var outputPath = ReadArgument("-outputPath");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/JellyGate-Unity.apk"));

            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            BuildReport report;
            try
            {
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { SceneAsset },
                    locationPathName = outputPath,
                    targetGroup = BuildTargetGroup.Android,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
            }
            finally
            {
                if (emulatorBuild)
                {
                    PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
                    AssetDatabase.SaveAssets();
                }
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Android build failed: {report.summary.result}");

            Debug.Log($"Jelly Gate APK built: {outputPath}");
        }

        public static void ConfigureAndroidSdkForRelease()
        {
            var androidSdkRoot = Environment.GetEnvironmentVariable("CROWNFRONT_ANDROID_SDK");
            if (string.IsNullOrWhiteSpace(androidSdkRoot) || !Directory.Exists(androidSdkRoot))
                throw new BuildFailedException("CROWNFRONT_ANDROID_SDK must point to the verified Android SDK.");
            AndroidExternalToolsSettings.sdkRootPath = Path.GetFullPath(androidSdkRoot);
            Debug.Log($"CROWNFRONT Android SDK configured: {AndroidExternalToolsSettings.sdkRootPath}");
        }

        [MenuItem("Jelly Gate/Build Google Play AAB")]
        public static void BuildAndroidAppBundle()
        {
            Debug.Log($"CROWNFRONT Windows build shell: ComSpec={Environment.GetEnvironmentVariable("ComSpec")}; PATHEXT={Environment.GetEnvironmentVariable("PATHEXT")}");
            var androidNdkRoot = Environment.GetEnvironmentVariable("CROWNFRONT_ANDROID_NDK");
            if (!string.IsNullOrWhiteSpace(androidNdkRoot))
            {
                if (!Directory.Exists(androidNdkRoot))
                    throw new BuildFailedException("CROWNFRONT_ANDROID_NDK does not exist.");
                AndroidExternalToolsSettings.ndkRootPath = Path.GetFullPath(androidNdkRoot);
                Debug.Log($"CROWNFRONT Android NDK configured: {AndroidExternalToolsSettings.ndkRootPath}");
            }
            // Unity 6.0.60f1+ can cache the Android Platform Tools version as 0.0 when
            // PlayerSettings are rewritten immediately before an Android build. Project
            // configuration is persisted separately; this entry point validates it and
            // starts the build without mutating the Android settings cache.
            if (PlayerSettings.bundleVersion != "1.00" || PlayerSettings.Android.bundleVersionCode != 4)
                throw new BuildFailedException("CROWNFRONT release settings must be version 1.00 (code 4) before building.");
            var keystorePath = Environment.GetEnvironmentVariable("CROWNFRONT_UPLOAD_KEYSTORE");
            var keystorePass = Environment.GetEnvironmentVariable("CROWNFRONT_UPLOAD_KEYSTORE_PASS");
            var aliasName = Environment.GetEnvironmentVariable("CROWNFRONT_UPLOAD_ALIAS");
            var aliasPass = Environment.GetEnvironmentVariable("CROWNFRONT_UPLOAD_ALIAS_PASS");
            if (string.IsNullOrWhiteSpace(keystorePath) || !File.Exists(keystorePath))
                throw new BuildFailedException("CROWNFRONT_UPLOAD_KEYSTORE must point to the upload keystore.");
            if (string.IsNullOrWhiteSpace(keystorePass) || string.IsNullOrWhiteSpace(aliasName) ||
                string.IsNullOrWhiteSpace(aliasPass))
                throw new BuildFailedException("Google Play upload signing environment variables are incomplete.");

            if (!BuildPipeline.IsBuildTargetSupported(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Android Build Support is not installed for this Unity editor.");
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Android, BuildTarget.Android))
                throw new BuildFailedException("Could not switch the active build target to Android.");

            var outputPath = ReadArgument("-outputPath");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../outputs/Crownfront-v1.00.aab"));
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            BuildReport report;
            try
            {
                EditorUserBuildSettings.buildAppBundle = true;
                PlayerSettings.Android.useCustomKeystore = true;
                PlayerSettings.Android.keystoreName = Path.GetFullPath(keystorePath);
                PlayerSettings.Android.keystorePass = keystorePass;
                PlayerSettings.Android.keyaliasName = aliasName;
                PlayerSettings.Android.keyaliasPass = aliasPass;
                report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
                {
                    scenes = new[] { SceneAsset },
                    locationPathName = outputPath,
                    targetGroup = BuildTargetGroup.Android,
                    target = BuildTarget.Android,
                    options = BuildOptions.None
                });
            }
            finally
            {
                // Secrets are supplied only to this build process and must not remain serialized
                // in the Unity project after the signed bundle has been produced.
                PlayerSettings.Android.keystorePass = string.Empty;
                PlayerSettings.Android.keyaliasPass = string.Empty;
                PlayerSettings.Android.keyaliasName = string.Empty;
                PlayerSettings.Android.keystoreName = string.Empty;
                PlayerSettings.Android.useCustomKeystore = false;
                EditorUserBuildSettings.buildAppBundle = false;
                AssetDatabase.SaveAssets();
            }

            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Google Play AAB build failed: {report.summary.result}");
            Debug.Log($"CROWNFRONT Google Play AAB built: {outputPath}");
        }

        public static void BuildWindowsQa()
        {
            ConfigureProject();
            if (!EditorUserBuildSettings.SwitchActiveBuildTarget(BuildTargetGroup.Standalone, BuildTarget.StandaloneWindows64))
                throw new BuildFailedException("Could not switch the active build target to Windows 64-bit.");

            var outputPath = ReadArgument("-outputPath");
            if (string.IsNullOrWhiteSpace(outputPath))
                outputPath = Path.GetFullPath(Path.Combine(Application.dataPath, "../../work/JellyGate-QA/JellyGate-QA.exe"));
            var outputDirectory = Path.GetDirectoryName(outputPath);
            if (!string.IsNullOrEmpty(outputDirectory)) Directory.CreateDirectory(outputDirectory);

            var report = BuildPipeline.BuildPlayer(new BuildPlayerOptions
            {
                scenes = new[] { SceneAsset },
                locationPathName = outputPath,
                targetGroup = BuildTargetGroup.Standalone,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None
            });
            if (report.summary.result != BuildResult.Succeeded)
                throw new BuildFailedException($"Windows QA build failed: {report.summary.result}");
        }

        private static void ConfigureImportedArt()
        {
            CrownfrontRenderPipelineSetup.Configure();
            ConfigureMapTexture();
            ConfigureNavigationBlockMask();
            KayKit2p5DAssetSetup.Configure();
            CrownfrontProductionAssetSetup.Configure();
            var artAssets = new[]
            {
                "Assets/Resources/unit-tank-v1.png",
                "Assets/Resources/unit-melee-v1.png",
                "Assets/Resources/unit-archer-v1.png",
                "Assets/Resources/unit-area-mage-v1.png",
                "Assets/Resources/unit-single-mage-v1.png",
                "Assets/Resources/hero-tank-v2.png",
                "Assets/Resources/hero-melee-v2.png",
                "Assets/Resources/hero-archer-v2.png",
                "Assets/Resources/hero-area-mage-v2.png",
                "Assets/Resources/hero-single-mage-v2.png",
                "Assets/Resources/hero-single-mage-v3.png",
                "Assets/Resources/enemy-atlas-v1.png",
                "Assets/Resources/defender-animation-atlas-v1.png",
                "Assets/Resources/enemy-animation-atlas-v1.png",
                "Assets/Resources/enemy-jelly-animation-atlas-v2.png",
                "Assets/Resources/enemy-warrior-animation-atlas-v2.png",
                "Assets/Resources/enemy-magic-animation-atlas-v2-final.png",
                "Assets/Resources/enemy-vfx-atlas-v1.png",
                "Assets/Resources/bombardier-animation-strip-v1.png",
                "Assets/Resources/defender-direction-down-v1.png",
                "Assets/Resources/defender-direction-side-v1.png",
                "Assets/Resources/defender-direction-up-v1.png",
                "Assets/Resources/defender-direction-down-v2.png",
                "Assets/Resources/defender-direction-side-v2.png",
                "Assets/Resources/walk-tank-consistent-v1.png",
                "Assets/Resources/walk-melee-consistent-v1.png",
                "Assets/Resources/walk-single-mage-consistent-v1.png",
                "Assets/Resources/walk-hero-single-mage-consistent-v1.png",
                "Assets/Resources/hero-direction-down-v1.png",
                "Assets/Resources/hero-direction-side-v1.png",
                "Assets/Resources/hero-direction-up-v1.png",
                "Assets/Resources/hero-tank-direction-v2.png",
                "Assets/Resources/hero-melee-direction-v2.png",
                "Assets/Resources/hero-archer-direction-v2.png",
                "Assets/Resources/hero-area-mage-direction-v2.png",
                "Assets/Resources/hero-single-mage-direction-v2.png",
                "Assets/Resources/hero-bombardier-direction-v2.png",
                "Assets/Resources/bombardier-direction-v1.png",
                "Assets/Resources/hero-bombardier-direction-v1.png",
                "Assets/Resources/lancer-direction-v1.png",
                "Assets/Resources/hero-lancer-direction-v1.png",
                "Assets/Resources/druid-direction-v1.png",
                "Assets/Resources/hero-druid-direction-v1.png",
                "Assets/Resources/musketeer-direction-v1.png",
                "Assets/Resources/hero-musketeer-direction-v1.png",
                "Assets/Resources/oracle-direction-v1.png",
                "Assets/Resources/hero-oracle-direction-v1.png",
                "Assets/Resources/skin-tank-variants-v1.png",
                "Assets/Resources/skin-melee-variants-v1.png",
                "Assets/Resources/skin-archer-variants-v2.png",
                "Assets/Resources/skin-area-mage-variants-v1.png",
                "Assets/Resources/skin-single-mage-variants-v1.png",
                "Assets/Resources/skin-bombardier-variants-v1.png",
                "Assets/Resources/skin-lancer-variants-v1.png",
                "Assets/Resources/skin-druid-variants-v1.png",
                "Assets/Resources/skin-musketeer-variants-v1.png",
                "Assets/Resources/skin-oracle-variants-v1.png",
                "Assets/Resources/main-menu-core-v5.png",
                "Assets/Resources/main-menu-sunrise-v5.png",
                "Assets/Resources/main-menu-moonlit-v6.png",
                "Assets/Resources/boss-lineup-a-v1.png",
                "Assets/Resources/boss-lineup-b-v1.png",
                "Assets/Resources/boss-lineup-a-back-v1.png",
                "Assets/Resources/boss-lineup-b-back-v1.png",
                "Assets/Resources/enemy-back-roster-v1.png",
                "Assets/Resources/battlefield-castle-azure-v1.png",
                "Assets/Resources/battlefield-castle-ember-v1.png",
                "Assets/Resources/battlefield-castle-ember-v2.png",
                "Assets/Resources/augment-recruit-portraits-v1.png",
                "Assets/Resources/enemy-roster-v2.png",
                "Assets/Resources/enemy-special-roster-v1.png",
                "Assets/Resources/enemy-mage-golem-v1.png",
                "Assets/Resources/enemy-golem-variants-v2.png",
                "Assets/Resources/enemy-golem-variants-back-v2.png",
                "Assets/Resources/enemy-abyss-roster-v1.png",
                "Assets/Resources/enemy-abyss-roster-back-v1.png",
                "Assets/Resources/enemy-jelly-mage-back-v1.png",
                "Assets/Resources/enemy-jelly-mage-back-v2.png",
                "Assets/Resources/enemy-veil-binder-walk-directions-v1.png",
                "Assets/Resources/enemy-veil-binder-attack-directions-v1.png",
                "Assets/Resources/enemy-veil-binder-skill-directions-v1.png",
                "Assets/Resources/enemy-armor-render-walk-directions-v1.png",
                "Assets/Resources/enemy-armor-render-attack-directions-v1.png",
                "Assets/Resources/enemy-armor-render-skill-directions-v1.png",
                "Assets/Resources/enemy-silence-shroud-walk-directions-v1.png",
                "Assets/Resources/enemy-silence-shroud-attack-directions-v1.png",
                "Assets/Resources/enemy-silence-shroud-skill-directions-v1.png",
                "Assets/Resources/crownfront-logo-crest-v1.png",
                "Assets/Resources/crownfront-logo-title-v2.png",
                "Assets/Resources/loading-screen-v3.png",
                "Assets/Resources/vfx-basic-focused-v4.png",
                "Assets/Resources/vfx-basic-ground-v4.png",
                "Assets/Resources/vfx-basic-physical-b-v4.png",
                "Assets/Resources/vfx-skill-physical-v4.png",
                "Assets/Resources/vfx-skill-magic-v4.png",
                "Assets/Resources/vfx-skill-remainders-v4.png",
                "Assets/Resources/vfx-ultimate-physical-v4.png",
                "Assets/Resources/vfx-ultimate-magic-v4.png",
                "Assets/Resources/vfx-ultimate-remainders-v4.png",
                "Assets/Resources/vfx-active-command-v6.png",
                "Assets/Resources/boss-jelly-king-clean-v1.png",
                "Assets/Resources/ui-placement-undo-v1.png"
            };
            foreach (var asset in artAssets) ConfigureCharacterTexture(asset);
            ConfigureSmallUiIcon("Assets/Resources/ui-placement-undo-small-v2.png");
            foreach (var asset in AssetDatabase.GetAllAssetPaths())
            {
                var generatedSkin = asset.StartsWith("Assets/Resources/skin-", StringComparison.OrdinalIgnoreCase);
                var bossDirectionSheet = asset.StartsWith("Assets/Resources/boss-", StringComparison.OrdinalIgnoreCase) &&
                                         asset.Contains("-directions-", StringComparison.OrdinalIgnoreCase);
                if ((!generatedSkin && !bossDirectionSheet) ||
                    !asset.EndsWith(".png", StringComparison.OrdinalIgnoreCase)) continue;
                ConfigureCharacterTexture(asset);
            }
        }

        private static void ConfigureMapTexture()
        {
            if (AssetImporter.GetAtPath(MapAsset) is not TextureImporter importer) return;

            var changed = importer.textureType != TextureImporterType.Sprite ||
                          importer.spriteImportMode != SpriteImportMode.Single ||
                          !importer.isReadable || importer.mipmapEnabled;
            if (!changed) return;

            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100f;
            importer.isReadable = true;
            importer.mipmapEnabled = false;
            importer.sRGBTexture = true;
            importer.alphaIsTransparency = false;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static void ConfigureCharacterTexture(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return;
            var android = importer.GetPlatformTextureSettings("Android");
            var needsRuntimeReadback = assetPath.Contains("direction-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("walk-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("attack-directions-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("skill-directions-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.EndsWith("enemy-jelly-mage-back-v1.png", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.EndsWith("enemy-jelly-mage-back-v2.png", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("skin-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("animation-atlas", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("animation-strip", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("vfx-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("roster", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("boss-lineup", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("boss-jelly-king", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("-directions-", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("mage-golem", StringComparison.OrdinalIgnoreCase) ||
                                       assetPath.Contains("golem-variants", StringComparison.OrdinalIgnoreCase);
            var changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                          importer.isReadable != needsRuntimeReadback ||
                          !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                          importer.wrapMode != TextureWrapMode.Clamp || !android.overridden ||
                          android.maxTextureSize != 2048 ||
                          android.format != TextureImporterFormat.ASTC_6x6 ||
                          android.textureCompression != TextureImporterCompression.CompressedHQ ||
                          android.compressionQuality != 82;
            if (!changed) return;

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = needsRuntimeReadback;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            // API 26+ devices support ASTC. A 6x6 block is the mobile quality/size midpoint:
            // alpha-edged character silhouettes remain clean while the previous ETC2-sized
            // package and GPU residency are cut substantially. Readability is retained only for
            // atlases whose runtime alpha metrics genuinely require it.
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 2048,
                format = TextureImporterFormat.ASTC_6x6,
                textureCompression = TextureImporterCompression.CompressedHQ,
                compressionQuality = 82,
                allowsAlphaSplitting = false
            });
            importer.SaveAndReimport();
        }

        private static void ConfigureSmallUiIcon(string assetPath)
        {
            if (AssetImporter.GetAtPath(assetPath) is not TextureImporter importer) return;
            var android = importer.GetPlatformTextureSettings("Android");
            var changed = importer.textureType != TextureImporterType.Default || importer.mipmapEnabled ||
                          importer.isReadable || !importer.alphaIsTransparency ||
                          importer.npotScale != TextureImporterNPOTScale.None ||
                          importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Bilinear ||
                          !android.overridden || android.maxTextureSize != 1024 ||
                          android.format != TextureImporterFormat.RGBA32 ||
                          android.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed) return;

            // Small high-contrast UI marks must remain a single, uncompressed texture. Automatic
            // sprite slicing and ASTC blocks made the previous arrow fragment when downscaled.
            importer.textureType = TextureImporterType.Default;
            importer.isReadable = false;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = true;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 1024;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = "Android",
                overridden = true,
                maxTextureSize = 1024,
                format = TextureImporterFormat.RGBA32,
                textureCompression = TextureImporterCompression.Uncompressed,
                compressionQuality = 100,
                allowsAlphaSplitting = false
            });
            importer.SaveAndReimport();
        }

        private static void ConfigureNavigationBlockMask()
        {
            if (AssetImporter.GetAtPath(NavigationBlockMaskAsset) is not TextureImporter importer) return;
            var changed = importer.textureType != TextureImporterType.Default ||
                          !importer.isReadable || importer.mipmapEnabled ||
                          !importer.alphaIsTransparency || importer.npotScale != TextureImporterNPOTScale.None ||
                          importer.wrapMode != TextureWrapMode.Clamp || importer.filterMode != FilterMode.Point ||
                          importer.textureCompression != TextureImporterCompression.Uncompressed;
            if (!changed) return;

            importer.textureType = TextureImporterType.Default;
            importer.isReadable = true;
            importer.alphaIsTransparency = true;
            importer.mipmapEnabled = false;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.sRGBTexture = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = 2048;
            importer.SaveAndReimport();
        }

        private static string ReadArgument(string name)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], name, StringComparison.OrdinalIgnoreCase)) return args[i + 1];
            return null;
        }

        private static bool HasArgument(string name)
        {
            foreach (var arg in Environment.GetCommandLineArgs())
                if (string.Equals(arg, name, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }
    }
}
