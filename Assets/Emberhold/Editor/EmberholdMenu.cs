using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emberhold.EditorTools
{
    public static class EmberholdMenu
    {
        const string ScenePath = "Assets/Emberhold/Scenes/Game.unity";

        [MenuItem("Emberhold/Open Game Scene", priority = 0)]
        public static void OpenGameScene()
        {
            if (!System.IO.File.Exists(ScenePath))
            {
                System.IO.Directory.CreateDirectory("Assets/Emberhold/Scenes");
                var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
                EditorSceneManager.SaveScene(scene, ScenePath);
                EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
                AssetDatabase.Refresh();
            }
            EditorSceneManager.OpenScene(ScenePath);
        }

        [MenuItem("Emberhold/Delete Save (start fresh)", priority = 20)]
        public static void DeleteSave()
        {
            new LocalSaveBackend().Delete();
            Debug.Log("[Emberhold] Save deleted.");
        }
    }
}
