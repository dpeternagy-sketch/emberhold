using UnityEditor;

namespace Emberhold.EditorTools
{
    /// <summary>Leaf and grass textures keep their cut-out shape at a distance (mip maps preserve alpha coverage).</summary>
    public class NatureImport : AssetPostprocessor
    {
        public override uint GetVersion() { return 1; }

        void OnPreprocessTexture()
        {
            var p = assetPath.Replace('\\', '/');
            if (!p.Contains("/Resources/Models/Nature/Tex/")) return;
            var ti = (TextureImporter)assetImporter;
            ti.textureType = TextureImporterType.Default;
            ti.mipmapEnabled = true;
            ti.alphaIsTransparency = true;
            ti.mipMapsPreserveCoverage = true;
            ti.alphaTestReferenceValue = 0.5f;
            ti.wrapMode = UnityEngine.TextureWrapMode.Repeat;
            ti.maxTextureSize = 512;
        }
    }
}
