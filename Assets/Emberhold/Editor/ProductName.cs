using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>The player-facing name is "Mega Fort" (Peter, 2026-11-23) and its app icon is Peter's
    /// metallic M on a royal-blue flag. The repo, namespace and project folder keep the old working name for now.</summary>
    [InitializeOnLoad]
    static class ProductName
    {
        public const string Name = "Mega Fort";
        const string IconPath = "Assets/Emberhold/Art/AppIcon/megafort_icon.png";

        static ProductName()
        {
            if (PlayerSettings.productName != Name) PlayerSettings.productName = Name;
            EditorApplication.delayCall += SetIcon;
        }

        static void SetIcon()
        {
            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            if (icon == null) return;
            var now = PlayerSettings.GetIconsForTargetGroup(BuildTargetGroup.Unknown);
            if (now != null && now.Length > 0 && now[0] == icon) return;
            PlayerSettings.SetIconsForTargetGroup(BuildTargetGroup.Unknown, new[] { icon });
        }
    }
}
