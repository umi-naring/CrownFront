using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering.Universal;

namespace JellyGate.Editor
{
    public static class CrownfrontRenderPipelineSetup
    {
        private const string PipelinePath = "Assets/Settings/UniversalRP.asset";
        private const string RendererPath = "Assets/Settings/Renderer2D.asset";

        public static void Configure()
        {
            var renderer = AssetDatabase.LoadAssetAtPath<ScriptableRendererData>(RendererPath);
            if (renderer == null) return;

            var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(PipelinePath);
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
