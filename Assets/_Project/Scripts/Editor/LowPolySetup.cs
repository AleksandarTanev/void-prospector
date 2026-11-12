using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using VoidProspector.Fx;

namespace VoidProspector.EditorTools
{
    /// <summary>
    /// Low-poly experiment setup: adds a Universal (3D) renderer to the URP asset next to the 2D one, turns main-light
    /// shadows off, and creates the shared lit material in Resources. Safe to run again.
    /// </summary>
    public static class LowPolySetup
    {
        public const string RendererPath = "Assets/Settings/UniversalRenderer.asset";
        public const string MaterialAssetPath = "Assets/_Project/Resources/" + LowPolyMeshes.MaterialPath + ".mat";
        const string PostProcessDataPath = "Packages/com.unity.render-pipelines.universal/Runtime/Data/PostProcessData.asset";

        /// <summary>Index of the Universal renderer in the URP asset's list (GameRoot uses it when low-poly is on).</summary>
        public const int RendererIndex = 1;

        [MenuItem("Void Prospector/Low-poly Setup")]
        public static void Run()
        {
            // This project sets the URP asset per quality level (all levels share one asset).
            var urp = (GraphicsSettings.defaultRenderPipeline ?? QualitySettings.renderPipeline ?? GraphicsSettings.currentRenderPipeline)
                as UniversalRenderPipelineAsset;
            if (urp == null) { Debug.LogError("Low-poly Setup: no URP asset in Graphics or Quality settings."); return; }

            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(RendererPath);
            if (renderer == null)
            {
                renderer = ScriptableObject.CreateInstance<UniversalRendererData>();
                renderer.postProcessData = AssetDatabase.LoadAssetAtPath<PostProcessData>(PostProcessDataPath);
                AssetDatabase.CreateAsset(renderer, RendererPath);
            }

            var so = new SerializedObject(urp);
            var list = so.FindProperty("m_RendererDataList");
            bool present = Enumerable.Range(0, list.arraySize).Any(i => list.GetArrayElementAtIndex(i).objectReferenceValue == renderer);
            if (!present)
            {
                while (list.arraySize < RendererIndex) list.InsertArrayElementAtIndex(list.arraySize);
                if (list.arraySize == RendererIndex) list.InsertArrayElementAtIndex(RendererIndex);
                list.GetArrayElementAtIndex(RendererIndex).objectReferenceValue = renderer;
            }
            so.FindProperty("m_MainLightShadowsSupported").boolValue = false;
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(urp);

            var mat = AssetDatabase.LoadAssetAtPath<Material>(MaterialAssetPath);
            if (mat == null)
            {
                Directory.CreateDirectory(Path.GetDirectoryName(MaterialAssetPath));
                mat = new Material(Shader.Find("Universal Render Pipeline/Simple Lit")) { name = "LowPolyLit" };
                AssetDatabase.CreateAsset(mat, MaterialAssetPath);
            }
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.DisableKeyword("_SPECULAR_COLOR");
            mat.SetFloat("_ReceiveShadows", 0f);
            mat.EnableKeyword("_RECEIVE_SHADOWS_OFF");
            // Emission on, black by default; gems set their own glow per renderer.
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", Color.black);
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(mat);

            AssetDatabase.SaveAssets();
            Debug.Log($"Low-poly Setup done: renderer {RendererPath} at index {RendererIndex}, material {MaterialAssetPath}.");
        }
    }
}
