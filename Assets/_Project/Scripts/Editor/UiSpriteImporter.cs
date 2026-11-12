using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace VoidProspector.EditorTools
{
    /// <summary>
    /// Import settings for the UI art in <c>Assets/_Project/Resources/UI/</c> (cut from the sheets in
    /// <c>art/ui-source/</c> by <c>tools/ui/slice_ui.py</c>): single sprites, no mipmaps, no compression, and the
    /// 9-slice borders of the frames. Change a border here, then use Void Prospector → Reimport UI Sprites.
    /// </summary>
    public class UiSpriteImporter : AssetPostprocessor
    {
        const string Folder = "Assets/_Project/Resources/UI/";

        /// <summary>Borders in sprite pixels: left, bottom, right, top (Unity's order). Measured from the art.</summary>
        static readonly Dictionary<string, Vector4> Borders = new Dictionary<string, Vector4>
        {
            { "panel", new Vector4(30, 30, 30, 48) },
            { "row", new Vector4(40, 18, 40, 22) },
            { "header", new Vector4(24, 36, 24, 30) },
            { "card", new Vector4(24, 30, 24, 56) },
            { "strip", new Vector4(20, 16, 20, 18) },
            { "tabbar", new Vector4(18, 18, 18, 18) },
            { "tab_selected", new Vector4(24, 18, 24, 26) },
            { "bar_track", new Vector4(16, 16, 16, 16) },
            { "bar_fill", new Vector4(16, 18, 16, 18) },
            { "bar_fill_white", new Vector4(16, 18, 16, 18) },
            { "btn_primary", new Vector4(24, 38, 24, 38) },
            { "btn_confirm", new Vector4(24, 38, 24, 38) },
            { "btn_gold", new Vector4(24, 38, 24, 38) },
            { "btn_secondary", new Vector4(24, 38, 24, 38) },
            { "btn_disabled", new Vector4(24, 38, 24, 38) },
            { "btn_pressed", new Vector4(24, 38, 24, 38) },
            { "icon_btn", new Vector4(28, 44, 28, 44) },
            { "icon_btn_selected", new Vector4(28, 50, 28, 50) },
        };

        public override uint GetVersion() => 1;

        void OnPreprocessTexture()
        {
            if (!assetPath.StartsWith(Folder)) return;
            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.spritePixelsPerUnit = 100;
            importer.mipmapEnabled = false;
            importer.alphaIsTransparency = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.textureCompression = TextureImporterCompression.Uncompressed;

            var name = System.IO.Path.GetFileNameWithoutExtension(assetPath);
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.spriteMeshType = SpriteMeshType.FullRect;   // required for sliced images
            settings.spriteBorder = assetPath.Contains("/Frames/") && Borders.TryGetValue(name, out var b) ? b : Vector4.zero;
            importer.SetTextureSettings(settings);
        }

        [MenuItem("Void Prospector/Reimport UI Sprites")]
        static void Reimport()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Texture2D", new[] { Folder.TrimEnd('/') }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }
    }
}
