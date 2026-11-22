using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Builds the whole game when any scene starts, so the project needs no hand-made scene.
    /// Press Play in Unity and the city appears.
    /// </summary>
    public static class GameBootstrap
    {
        static bool hooked;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void FirstBoot()
        {
            if (!hooked)
            {
                hooked = true;
                // Also rebuild after a scene reload (e.g. "Restart city").
                UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) => { if (Time.frameCount > 1) Boot(); };
            }
            Boot();
        }

        static void Boot()
        {
            if (Object.FindAnyObjectByType<GameManager>() != null) return;

            // Static registries survive when Unity's domain reload is off; start clean.
            Building.All.Clear(); TreeNode.All.Clear(); Enemy.All.Clear(); Villager.All.Clear(); LightSource.All.Clear();

            Application.targetFrameRate = 60;
            if (Application.isMobilePlatform) Screen.orientation = ScreenOrientation.Portrait;

            // Camera: reuse the scene's main camera if there is one.
            var cam = Camera.main;
            if (cam == null)
            {
                var camGo = new GameObject("Main Camera");
                camGo.tag = "MainCamera";
                cam = camGo.AddComponent<Camera>();
                camGo.AddComponent<AudioListener>();
            }

            // Sun: reuse a directional light if the scene has one.
            Light sun = null;
            foreach (var l in Object.FindObjectsByType<Light>(FindObjectsSortMode.None))
                if (l.type == LightType.Directional) { sun = l; break; }
            if (sun == null)
            {
                var sunGo = new GameObject("Sun");
                sun = sunGo.AddComponent<Light>();
                sun.type = LightType.Directional;
            }
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.75f;

            var root = new GameObject("Emberhold");
            root.AddComponent<AudioManager>();

            var rig = root.AddComponent<CameraRig>();
            rig.Init(cam);

            var world = new GameObject("World").AddComponent<WorldBuilder>();
            var cycle = root.AddComponent<DayNightCycle>();
            var spawner = new GameObject("Enemies").AddComponent<EnemySpawner>();

            var gm = root.AddComponent<GameManager>();
            gm.Init(world, cycle, spawner, new LocalSaveBackend());
            cycle.Init(sun, cam, gm.State.dayTimeLeft > 0 ? gm.State.dayTimeLeft : DayNightCycle.DayLength);
            var raids = root.AddComponent<RaidDirector>();
            raids.Init(gm);
            gm.Raids = raids;

            var interaction = root.AddComponent<InteractionController>();
            interaction.Init(rig, gm);

            var hud = root.AddComponent<HUD>();
            hud.Init(gm, interaction, rig);

            AudioManager.SetAmbience("day_amb");
            gm.Toast("Welcome to Emberhold");
        }
    }
}
