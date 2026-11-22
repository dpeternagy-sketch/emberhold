using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// Imports the Kenney UI shapes in Resources/UI as crisp 9-sliced sprites (no mipmaps, no
    /// compression), so buttons and panels stretch to any size without blurry or stretched corners.
    /// </summary>
    public class UIImport : AssetPostprocessor
    {
        public override uint GetVersion() { return 1; }

        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/UI/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Sprite;
            ti.spriteImportMode = SpriteImportMode.Single;
            ti.mipmapEnabled = false;
            ti.alphaIsTransparency = true;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.filterMode = FilterMode.Bilinear;
            ti.textureCompression = TextureImporterCompression.Uncompressed;
            ti.spritePixelsPerUnit = 100;
            ti.spriteBorder = Border(System.IO.Path.GetFileNameWithoutExtension(p));
        }

        /// <summary>Slice borders (left, bottom, right, top) for the Kenney "Double" shapes.</summary>
        static Vector4 Border(string name)
        {
            bool depth = name.Contains("depth");
            if (name.StartsWith("button_rectangle") || name.StartsWith("button_square"))
                return new Vector4(24, depth ? 32 : 24, 24, 24);
            return Vector4.zero;   // round buttons and icons are never stretched
        }
    }
}
