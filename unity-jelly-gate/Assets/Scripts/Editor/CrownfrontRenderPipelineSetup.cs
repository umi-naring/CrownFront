using UnityEditor;
using UnityEngine;

namespace JellyGate.Editor
{
    public static class CrownfrontRenderPipelineSetup
    {
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string RendererPath = "Assets/Settings/Renderer2D.asset";

        public static void Configure()
        {
            // Keep this setup utility compilable even while the Package Manager is
            // temporarily unavailable in a headless CI run.  The serialized asset
            // references are sufficient here; no URP runtime type is required.
            var renderer = AssetDatabase.LoadMainAssetAtPath(RendererPath);
            if (renderer == null) return;

            var pipeline = AssetDatabase.LoadMainAssetAtPath(PipelinePath);
            if (pipeline == null) return;
            var serialized = new SerializedObject(pipeline);
            var list = serialized.FindProperty("m_RendererDataList");
            var defaultIndex = serialized.FindProperty("m_DefaultRendererIndex");
            if (list == null || defaultIndex == null) return;
            list.arraySize = 1;
            list.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
            defaultIndex.intValue = 0;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
            EditorUtility.SetDirty(pipeline);
            AssetDatabase.SaveAssets();
        }
    }
}
