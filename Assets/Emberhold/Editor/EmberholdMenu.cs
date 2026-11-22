using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// The game keeps static lists and caches (enemies, villagers, meshes). Without a domain reload
    /// they would leak from one Play session into the next, so every Play starts clean.
    /// </summary>
    [InitializeOnLoad]
    static class CleanPlayMode
    {
        static CleanPlayMode()
        {
            if (EditorSettings.enterPlayModeOptionsEnabled) EditorSettings.enterPlayModeOptionsEnabled = false;
        }
    }

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

        // ---------------- Play-mode test tools (used for play-testing) ----------------

        static GameManager GM { get { return Application.isPlaying ? GameManager.Instance : null; } }
        static InteractionController Input { get { return Object.FindAnyObjectByType<InteractionController>(); } }

        [MenuItem("Emberhold/Test/Raid now", priority = 100)]
        static void TestRaid() { if (GM != null && GM.Raids != null) GM.Raids.TriggerNow(); }

        [MenuItem("Emberhold/Test/Wall ring + gate", priority = 102)]
        static void WallRing()
        {
            if (GM == null) return;
            GM.State.steel += 2000; GM.State.coins += 500;
            var wall = BuildingCatalog.Get("wall"); var gate = BuildingCatalog.Get("gate");
            int cx = Mathf.FloorToInt(GameManager.CastleCenter.x), cz = Mathf.FloorToInt(GameManager.CastleCenter.z);
            int x0 = cx - 7, x1 = cx + 7, z0 = cz - 9, z1 = GridMap.BuildBackZ - 1;
            var gateCell = new Vector2Int(cx, z0);
            int placed = 0, skipped = 0;
            for (int x = x0; x <= x1; x++)
                for (int z = z0; z <= z1; z++)
                {
                    bool edge = x == x0 || x == x1 || z == z0 || z == z1;
                    if (!edge || (x == gateCell.x && z == gateCell.y)) continue;
                    var c = new Vector2Int(x, z);
                    var tr = GM.Grid.At(c) as TreeNode;
                    if (tr != null) { GM.Grid.Free(new[] { c }, tr); Object.Destroy(tr.gameObject); }
                    var o = GM.Grid.At(c) as Building;
                    if (o != null) GM.Demolish(o);
                    if (GM.PlaceNew(wall, x, z, 0) != null) placed++; else skipped++;
                }
            var go = GM.Grid.At(gateCell) as Building; if (go != null) GM.Demolish(go);
            bool sealRefused = !GM.CanPlace(wall, gateCell.x, gateCell.y, 0);
            var g = GM.PlaceNew(gate, gateCell.x, gateCell.y, 0);
            GM.RefreshWallJoins();
            Debug.Log("[WallTest] walls placed " + placed + ", skipped " + skipped + ", closing wall refused: " + sealRefused + ", gate placed: " + (g != null));
        }

        [MenuItem("Emberhold/Test/Start night", priority = 101)]
        static void TestNight() { if (GM != null) GM.Cycle.StartNightNow(); }

        [MenuItem("Emberhold/Test/Speed x1", priority = 110)]
        static void Speed1() { Time.timeScale = 1f; }

        [MenuItem("Emberhold/Test/Speed x4", priority = 111)]
        static void Speed4() { Time.timeScale = 4f; }

        [MenuItem("Emberhold/Test/Give resources", priority = 120)]
        static void Give()
        {
            if (GM == null) return;
            var s = GM.State;
            s.steel += 400; s.energy += 200; s.coins += 300; s.food += 200; s.water += 200;
        }

        [MenuItem("Emberhold/Test/Wreck two buildings", priority = 121)]
        static void Wreck()
        {
            if (GM == null) return;
            int n = 0;
            foreach (var b in Building.All.ToArray())
            {
                if (b == GM.Castle || b.Ruined || b.Def.passable) continue;
                if (n == 0) b.TakeDamage(b.Data.hp + 1);          // ruin
                else if (n == 1) b.TakeDamage(b.Data.hp / 2 + 1); // damaged, smoking
                if (++n >= 2) break;
            }
        }

        [MenuItem("Emberhold/Test/Castle level +1", priority = 122)]
        static void CastleUp()
        {
            if (GM == null) return;
            GM.Castle.Data.level++;
            GM.OnBuildingUpgraded(GM.Castle);
        }

        static void PlaceAtView(string id)
        {
            if (GM == null) return;
            var input = Input;
            input.BeginPlacing(BuildingCatalog.Get(id));
            input.ConfirmPlacement();
        }

        [MenuItem("Emberhold/Test/Place Gun Turret", priority = 130)] static void P1() { PlaceAtView("watchtower"); }
        [MenuItem("Emberhold/Test/Place Laser Turret", priority = 131)] static void P2() { PlaceAtView("ballista"); }
        [MenuItem("Emberhold/Test/Place Missile Launcher", priority = 132)] static void P3() { PlaceAtView("cannon"); }
        [MenuItem("Emberhold/Test/Place Solar Panels", priority = 133)] static void P4() { PlaceAtView("solar"); }
        [MenuItem("Emberhold/Test/Place Steel Drill", priority = 134)] static void P5() { PlaceAtView("mine"); }

        [MenuItem("Emberhold/Test/Wall line (8)", priority = 140)]
        static void WallLine()
        {
            if (GM == null) return;
            var input = Input;
            input.BeginPlacing(BuildingCatalog.Get("wall"));
            input.ConfirmPlacement();                 // first piece
            var g = input.Ghost;
            if (g == null) return;
            g.Data.x += 6; g.Data.z -= 2;            // pull the end 6 right and 2 down: an L-shaped line
            typeof(InteractionController).GetMethod("RefreshGhost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(input, null);
            input.ConfirmPlacement();
            input.CancelPlacement();
        }

        [MenuItem("Emberhold/Test/Select a weapon (range circle)", priority = 141)]
        static void SelectWeapon()
        {
            if (GM == null) return;
            foreach (var b in Building.All) if (b != GM.Castle && b.Def.attackRange > 0f) { Input.Select(b); return; }
        }

        [MenuItem("Emberhold/Test/Select HQ", priority = 141)]
        static void SelectHQ() { if (GM != null && GM.Castle != null) Input.Select(GM.Castle); }

        [MenuItem("Emberhold/Test/Preview Gun Turret (ghost)", priority = 142)]
        static void Ghost() { if (GM != null) Input.BeginPlacing(BuildingCatalog.Get("watchtower")); }

        [MenuItem("Emberhold/Test/Ghost hidden behind HQ (x-ray)", priority = 145)]
        static void GhostBehind()
        {
            if (GM == null) return;
            Input.BeginPlacing(BuildingCatalog.Get("house"));
            var g = Input.Ghost;
            g.Data.x = GM.Castle.Data.x + 1; g.Data.z = GM.Castle.Data.z + 4;
            typeof(InteractionController).GetMethod("RefreshGhost", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(Input, null);
        }

        [MenuItem("Emberhold/Test/Info popup (selected)", priority = 146)]
        static void InfoPopup() { var h = Object.FindAnyObjectByType<HUD>(); if (h != null && Input.Selected != null) h.ShowInfo(Input.Selected); }

        [MenuItem("Emberhold/Test/Upgrade popup (selected)", priority = 147)]
        static void UpgradePopup() { var h = Object.FindAnyObjectByType<HUD>(); if (h != null && Input.Selected != null) h.ShowUpgrade(Input.Selected); }

        [MenuItem("Emberhold/Test/Start upgrade (selected)", priority = 148)]
        static void StartUpgrade() { if (GM != null && Input.Selected != null) GM.TryUpgrade(Input.Selected); }

        [MenuItem("Emberhold/Test/Log 40 spawn points", priority = 152)]
        static void LogSpawns()
        {
            float maxZ = -999f; var sb = new System.Text.StringBuilder();
            for (int i = 0; i < 40; i++)
            {
                var p = EnemySpawner.SpawnPoint(Random.Range(0f, 360f), Random.Range(-5f, 5f));
                maxZ = Mathf.Max(maxZ, p.z);
                sb.Append(p.ToString("F0")).Append(' ');
            }
            Debug.Log("[Emberhold] 40 spawns, max z = " + maxZ.ToString("F1") + " (mountain starts near z 22): " + sb);
        }

        [MenuItem("Emberhold/Test/Place trap + pen near HQ", priority = 149)]
        static void TrapPen() { PlaceAtView("pen"); PlaceAtView("trap"); }

        [MenuItem("Emberhold/Test/Cancel placing", priority = 143)]
        static void CancelGhost() { if (GM != null) Input.CancelPlacement(); }

        [MenuItem("Emberhold/Test/Open build menu", priority = 144)]
        static void OpenBuild()
        {
            var hud = Object.FindAnyObjectByType<HUD>();
            if (hud != null) typeof(HUD).GetMethod("TogglePanel", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance).Invoke(hud, new object[] { BuildingCategory.Economy });
        }

        [MenuItem("Emberhold/Test/Look at mountain", priority = 150)]
        static void LookMountain() { var rig = Object.FindAnyObjectByType<CameraRig>(); if (rig != null) { rig.focus = new Vector3(10f, 0, 20f); rig.distance = 60f; rig.Pan(Vector3.zero); } }

        [MenuItem("Emberhold/Test/Look back at city", priority = 151)]
        static void LookCity() { var rig = Object.FindAnyObjectByType<CameraRig>(); if (rig != null) { rig.focus = new Vector3(0f, 0, 9f); rig.distance = 78f; rig.Pan(Vector3.zero); } }

        [MenuItem("Emberhold/Test/Save screenshot", priority = 160)]
        static void Shot()
        {
            System.IO.Directory.CreateDirectory("Screens");
            var path = "Screens/shot_" + System.DateTime.Now.ToString("HHmmss") + ".png";
            ScreenCapture.CaptureScreenshot(path, 2);
            Debug.Log("[Emberhold] screenshot " + path);
        }
    }
}
