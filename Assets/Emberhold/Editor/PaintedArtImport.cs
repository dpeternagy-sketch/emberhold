using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// Peter's painted art (buildings, portraits, vegetation atlas, mountain backdrop) must look as sharp
    /// as his images: full mip chain with trilinear + anisotropic filtering and a slight sharpening bias,
    /// high-quality compression, and alpha coverage kept for the alpha-cut shaders.
    /// </summary>
    public class PaintedArtImport : AssetPostprocessor
    {
        public override uint GetVersion() { return 2; }

        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Sprites/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.sRGBTexture = !p.EndsWith("_emis.png");
            ti.alphaIsTransparency = true;
            ti.mipmapEnabled = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.5f;
            ti.mipMapBias = -0.6f;
            ti.filterMode = FilterMode.Trilinear;
            ti.anisoLevel = 8;
            ti.wrapMode = TextureWrapMode.Clamp;
            ti.maxTextureSize = 2048;
            ti.textureCompression = TextureImporterCompression.CompressedHQ;
        }
    }
}
