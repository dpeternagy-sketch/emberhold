using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// Unattended look preview: Unity -projectPath <copy> -executeMethod Emberhold.EditorTools.PreviewRunner.Run
    /// Opens the game scene on a fresh save, enters Play, runs PreviewTour, exits Play and quits.
    /// Only ever run on a separate preview copy of the project.
    /// </summary>
    [InitializeOnLoad]
    public static class PreviewRunner
    {
        const string Key = "EH_PREVIEW_STATE";   // 1 = entering play, 2 = tour running, 3 = done
        static double startAt;

        public static void Run()
        {
            new LocalSaveBackend().Delete();
            EmberholdMenu.OpenGameScene();
            SessionState.SetInt(Key, 1);
            Debug.Log("[Preview] entering Play");
            EditorApplication.EnterPlaymode();
        }

        static PreviewRunner()
        {
            int state = SessionState.GetInt(Key, 0);
            if (state == 0) return;
            if (state == 3 && !EditorApplication.isPlayingOrWillChangePlaymode)
            {
                SessionState.SetInt(Key, 0);
                Debug.Log("[Preview] finished, quitting");
                EditorApplication.delayCall += () => EditorApplication.Exit(0);
                return;
            }
            startAt = EditorApplication.timeSinceStartup + 4.0;
            EditorApplication.update += Tick;
        }

        static void Tick()
        {
            int state = SessionState.GetInt(Key, 0);
            if (!Application.isPlaying) return;
            if (state == 1 && EditorApplication.timeSinceStartup > startAt && GameManager.Instance != null)
            {
                SessionState.SetInt(Key, 2);
                PreviewTour.Run();
            }
            else if (state == 2 && PreviewTour.Finished)
            {
                SessionState.SetInt(Key, 3);
                EditorApplication.update -= Tick;
                EditorApplication.playModeStateChanged += st =>
                {
                    if (st != PlayModeStateChange.EnteredEditMode || SessionState.GetInt(Key, 0) != 3) return;
                    SessionState.SetInt(Key, 0);
                    Debug.Log("[Preview] finished, quitting");
                    EditorApplication.Exit(0);
                };
                EditorApplication.ExitPlaymode();
            }
        }
    }
}
