using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// Creature models use simple legacy animation (no Animator setup needed): walk, run and idle loop.
    /// Building and prop models import without cameras, lights or animation.
    /// </summary>
    class ModelImport : AssetPostprocessor
    {
        public override uint GetVersion() { return 2; }

        [MenuItem("Emberhold/Setup/Reimport art models", priority = 21)]
        static void ReimportAll()
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { "Assets/Emberhold/Resources" }))
                AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
        }

        void OnPreprocessModel()
        {
            if (!assetPath.Contains("/Resources/Models/") && !assetPath.Contains("/Resources/KayKit/")) return;
            var mi = (ModelImporter)assetImporter;
            mi.importCameras = false;
            mi.importLights = false;
            if (assetPath.Contains("/Models/Dinos/"))
            {
                mi.animationType = ModelImporterAnimationType.Legacy;
                mi.importAnimation = true;
            }
            else
            {
                mi.animationType = ModelImporterAnimationType.None;
                mi.importAnimation = false;
            }
        }

        void OnPostprocessAnimation(GameObject root, AnimationClip clip)
        {
            if (!assetPath.Contains("/Models/Dinos/")) return;
            string n = clip.name;
            bool loop = n.EndsWith("_Walk") || n.EndsWith("_Run") || n.EndsWith("_Idle");
            clip.wrapMode = loop ? WrapMode.Loop : WrapMode.Once;
        }
    }
}
