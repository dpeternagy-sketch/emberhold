using UnityEditor;

namespace Emberhold.EditorTools
{
    /// <summary>The player-facing name is "Mega Fort" (Peter, 2026-11-23). The repo, namespace and project
    /// folder keep the old working name for now.</summary>
    [InitializeOnLoad]
    static class ProductName
    {
        public const string Name = "Mega Fort";

        static ProductName()
        {
            if (PlayerSettings.productName != Name) PlayerSettings.productName = Name;
        }
    }
}
