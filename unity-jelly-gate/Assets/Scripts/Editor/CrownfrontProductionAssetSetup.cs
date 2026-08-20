using System;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace JellyGate.Editor
{
    // Keeps the Blender source-of-truth roster consistent on every workstation and build
    // target.  Read/write is required because the authored COL_* meshes become convex tactical
    // trigger colliders at runtime.
    [InitializeOnLoad]
    public static class CrownfrontProductionAssetSetup
    {
        private const string Root = "Assets/Resources/CrownfrontProduction";

        static CrownfrontProductionAssetSetup()
        {
            EditorApplication.delayCall += Configure;
        }

        [MenuItem("Jelly Gate/Configure Production Defender Roster")]
        public static void Configure()
        {
            var paths = AssetDatabase.FindAssets("t:Model", new[] { Root })
                .Select(AssetDatabase.GUIDToAssetPath)
                .Where(path => path.EndsWith(".fbx", StringComparison.OrdinalIgnoreCase))
                .ToArray();

            foreach (var path in paths)
            {
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer) continue;
                var changed = !importer.importAnimation ||
                              importer.animationType != ModelImporterAnimationType.Generic ||
                              !importer.isReadable ||
                              importer.importBlendShapes;
                importer.importAnimation = true;
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.isReadable = true;
                importer.importBlendShapes = false;
                importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;

                var clips = importer.defaultClipAnimations;
                foreach (var clip in clips)
                {
                    var loop = clip.name.IndexOf("Idle", StringComparison.OrdinalIgnoreCase) >= 0 ||
                               clip.name.IndexOf("Walk", StringComparison.OrdinalIgnoreCase) >= 0;
                    if (clip.loopTime == loop) continue;
                    clip.loopTime = loop;
                    changed = true;
                }
                if (clips.Length > 0) importer.clipAnimations = clips;
                if (changed) importer.SaveAndReimport();
            }

            Debug.Log($"CROWNFRONT_PRODUCTION_IMPORT models={paths.Length}");
        }
    }
}
