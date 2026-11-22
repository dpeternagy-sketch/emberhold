using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Dev only: stages a showcase city and records portrait screenshots plus video frames
    /// (Screens/preview/). Used to show the look before a build ships. Never runs in a player build
    /// unless started by hand from the Editor menu.
    /// </summary>
    public class PreviewTour : MonoBehaviour
    {
        public static bool Running { get; private set; }
        public static bool Finished { get; private set; }
        const string Dir = "Screens/preview";
        GameManager gm; CameraRig rig; InteractionController input; HUD hud;
        int frame;
        bool recording;
        readonly List<Transform> troops = new List<Transform>();

        public static void Run()
        {
            if (!Application.isPlaying || Running) return;
            if (!MemorySaveBackend.PreviewMode) { Debug.LogWarning("[Preview] refused: preview mode is off (it would change the real save)"); return; }
            new GameObject("PreviewTour").AddComponent<PreviewTour>();
        }

        IEnumerator Start()
        {
            Running = true;
            gm = GameManager.Instance;
            rig = FindAnyObjectByType<CameraRig>();
            input = FindAnyObjectByType<InteractionController>();
            hud = FindAnyObjectByType<HUD>();
            System.IO.Directory.CreateDirectory(Dir + "/frames");
            foreach (var f in System.IO.Directory.GetFiles(Dir + "/frames")) System.IO.File.Delete(f);
            yield return null;
            Stage();
            yield return new WaitForSeconds(2.5f);   // let models, workers and badges settle

            // ---- stills, day ----
            hud.PreviewCloseAll();
            View(new Vector3(0f, 0f, 6f), 78f); yield return Shot("01_day_city");
            View(new Vector3(0f, 0f, 9f), 52f); yield return Shot("02_day_close");
            View(gm.Castle.Center + new Vector3(0, 0, -6f), 34f); yield return Shot("03_hq_troops");
            var barracks = First("barracks");
            if (barracks != null) { input.Select(barracks); hud.PreviewShowTrain(barracks); yield return new WaitForSeconds(0.5f); yield return Shot("03b_train_popup"); hud.PreviewCloseAll(); input.Deselect(); }
            input.Select(gm.Castle); yield return new WaitForSeconds(0.6f); yield return Shot("04_tap_hq");
            hud.ShowInfo(gm.Castle); yield return new WaitForSeconds(0.4f); yield return Shot("05_info_panel");
            hud.PreviewCloseAll();
            var house = First("house");
            if (house != null) { input.Select(house); hud.ShowUpgrade(house); yield return new WaitForSeconds(0.4f); yield return Shot("06_upgrade_panel"); }
            hud.PreviewCloseAll(); input.Deselect();
            View(new Vector3(0f, 0f, 6f), 62f);
            hud.PreviewOpenBuild(BuildingCategory.Economy); yield return new WaitForSeconds(0.4f); yield return Shot("07_build_menu");
            hud.PreviewOpenBuild(BuildingCategory.Defense); yield return new WaitForSeconds(0.4f); yield return Shot("08_defense_menu");
            hud.PreviewOpenBuild(BuildingCategory.Army); yield return new WaitForSeconds(0.4f); yield return Shot("08b_army_menu");
            hud.PreviewCloseAll();
            var gun = First("watchtower");
            if (gun != null) { input.Select(gun); yield return new WaitForSeconds(0.4f); yield return Shot("09_turret_range"); input.Deselect(); }

            // ---- dino attack by day (stills) ----
            SpawnDinos(new Vector3(-20f, 0, 2f), 6, true);
            View(new Vector3(-9f, 0, 4f), 40f);
            yield return new WaitForSeconds(5f); yield return Shot("10_dino_attack");
            yield return new WaitForSeconds(3f); yield return Shot("11_turrets_fire");
            foreach (var e in Enemy.All.ToArray()) if (e != null) Destroy(e.gameObject);
            yield return new WaitForSeconds(1f);

            // ---- video: day fly-over, tap, menu, attack, dusk to night ----
            Time.captureFramerate = 30; recording = true; frame = 0;
            yield return Glide(new Vector3(0, 0, -8f), 90f, new Vector3(0, 0, 8f), 60f, 4f);
            yield return Glide(new Vector3(0, 0, 8f), 60f, gm.Castle.Center + new Vector3(0, 0, -4f), 36f, 2.5f);
            input.Select(gm.Castle); yield return Hold(1.5f);
            hud.ShowInfo(gm.Castle); yield return Hold(1.5f);
            hud.PreviewCloseAll(); input.Deselect();
            hud.PreviewOpenBuild(BuildingCategory.Defense); yield return Hold(1.5f);
            hud.PreviewCloseAll();
            SpawnDinos(new Vector3(-21f, 0, 3f), 6, true);
            yield return Glide(gm.Castle.Center + new Vector3(0, 0, -4f), 36f, new Vector3(-9f, 0, 4f), 44f, 2f);
            yield return Hold(5f);
            gm.Cycle.StartNightNow();
            yield return Glide(new Vector3(-9f, 0, 4f), 44f, new Vector3(0, 0, 4f), 70f, 6f);
            yield return Hold(3f);
            recording = false; Time.captureFramerate = 0;

            // ---- night stills ----
            yield return WaitDark();
            View(new Vector3(0f, 0f, 6f), 78f); yield return Shot("12_night_city");
            View(new Vector3(0f, 0f, 9f), 48f); yield return Shot("13_night_close");
            Debug.Log("[Preview] done: " + frame + " video frames in " + Dir + "/frames");
            Running = false; Finished = true;
        }

        // ------------------------------------------------------------------ staging

        void Stage()
        {
            var s = gm.State;
            s.steel += 20000; s.energy += 20000; s.coins += 20000; s.food += 5000; s.water += 5000;
            while (gm.CastleLevel < 3) { gm.Castle.Data.level++; gm.OnBuildingUpgraded(gm.Castle); }
            var c = GameManager.CastleCenter;
            Place("house", c + new Vector3(-9, 0, -2)); Place("house", c + new Vector3(-9, 0, -6)); Place("house", c + new Vector3(-6, 0, -9));
            Place("house", c + new Vector3(7, 0, -2)); Place("house", c + new Vector3(7, 0, -6));
            Place("farm", c + new Vector3(-4, 0, -14)); Place("farm", c + new Vector3(3, 0, -14));
            Place("solar", c + new Vector3(11, 0, -10)); Place("solar", c + new Vector3(11, 0, -13));
            Place("mine", c + new Vector3(-12, 0, -11));
            Place("well", c + new Vector3(4, 0, -9));
            Place("watchtower", c + new Vector3(-6, 0, -18)); Place("watchtower", c + new Vector3(8, 0, -18));
            Place("ballista", c + new Vector3(-13, 0, -5)); Place("cannon", c + new Vector3(1, 0, -19));
            Place("trap", c + new Vector3(-16, 0, -12));
            // a wall arc with a gate across the west approach
            var wall = BuildingCatalog.Get("wall"); var gate = BuildingCatalog.Get("gate");
            int wx = Mathf.FloorToInt(c.x) - 17;
            for (int z = Mathf.FloorToInt(c.z) - 16; z <= Mathf.FloorToInt(c.z) - 2; z++)
            {
                Clear(wx, z, 1, 1);
                bool isGate = z == Mathf.FloorToInt(c.z) - 9;
                gm.PlaceNew(isGate ? gate : wall, wx, z, 0);
            }
            gm.RefreshWallJoins();
            // army district in front of the HQ: camp, barracks, command center, robot factory
            Place("camp", c + new Vector3(-2, 0, -8));
            Place("barracks", c + new Vector3(-6, 0, -13));
            Place("command", c + new Vector3(3, 0, -6));
            Place("robotfactory", c + new Vector3(8, 0, -14));
            s.riflemen = 8; s.heavies = 2; s.drones = 2;
            gm.RecalculateCity();
        }

        Building First(string id)
        {
            foreach (var b in Building.All) if (b.Def.id == id) return b;
            return null;
        }

        void Place(string id, Vector3 at)
        {
            var def = BuildingCatalog.Get(id);
            int x0 = Mathf.FloorToInt(at.x), z0 = Mathf.FloorToInt(at.z);
            for (int r = 0; r < 6; r++)
                for (int dx = -r; dx <= r; dx++)
                    for (int dz = -r; dz <= r; dz++)
                    {
                        if (Mathf.Max(Mathf.Abs(dx), Mathf.Abs(dz)) != r) continue;
                        int x = x0 + dx, z = z0 + dz;
                        Clear(x, z, def.width, def.depth);
                        if (gm.CanPlace(def, x, z, 0) && gm.PlaceNew(def, x, z, 0) != null) return;
                    }
            Debug.LogWarning("[Preview] could not place " + id);
        }

        void Clear(int x, int z, int w, int d)
        {
            for (int i = 0; i < w; i++)
                for (int j = 0; j < d; j++)
                {
                    var cell = new Vector2Int(x + i, z + j);
                    var t = gm.Grid.At(cell) as TreeNode;
                    if (t != null) { gm.Grid.Free(new[] { cell }, t); Destroy(t.gameObject); }
                }
        }

        /// <summary>Stand-ins for the army (milestone 2): soldiers, a robot and the commander near the HQ.</summary>
        void Troops(Vector3 c)
        {
            var root = new GameObject("PreviewTroops").transform;
            for (int i = 0; i < 4; i++) Trooper(root, "Models/Space/astronautA", c + new Vector3(-3f + i * 1.3f, 0, -5.5f), 1.25f, Color.clear, i);
            Trooper(root, "Models/Space/astronautB", c + new Vector3(3.6f, 0, -5.2f), 1.5f, Color.clear, 5);
            Trooper(root, "Models/Space/astronautB", c + new Vector3(0.3f, 0, -3.6f), 1.75f, new Color(1f, 0.78f, 0.2f), 6);
            Trooper(root, "Models/Units/Robot", c + new Vector3(-5f, 0, -4.2f), 1.5f, Color.clear, 7);
            var drone = ModelLibrary.Spawn("Models/Space/craft_speederA", root, 1.6f, 1.6f);
            if (drone != null) { drone.position = c + new Vector3(5.5f, 2.2f, -7f); troops.Add(drone); drone.gameObject.AddComponent<PreviewHover>(); }
        }

        void Trooper(Transform root, string path, Vector3 pos, float height, Color trim, int seed)
        {
            var t = ModelLibrary.Spawn(path, root, height, height, height);
            if (t == null) { Debug.LogWarning("[Preview] missing " + path); return; }
            t.position = pos;
            t.rotation = Quaternion.Euler(0, 180f, 0);
            if (trim.a > 0f)
            {
                foreach (var r in t.GetComponentsInChildren<Renderer>())
                    foreach (var m in r.materials) { if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", Color.Lerp(m.GetColor("_BaseColor"), trim, 0.45f)); }
                var ring = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
                Destroy(ring.GetComponent<Collider>());
                ring.transform.SetParent(t, false); ring.transform.localScale = new Vector3(1.4f, 0.02f, 1.4f); ring.transform.localPosition = new Vector3(0, 0.02f, 0);
                var rm = ring.GetComponent<Renderer>().material; rm.color = trim;
                if (rm.HasProperty("_EmissionColor")) { rm.EnableKeyword("_EMISSION"); rm.SetColor("_EmissionColor", trim * 1.5f); }
            }
            var anim = t.GetComponentInChildren<Animation>();
            if (anim != null) ModelLibrary.Play(anim, "Idle", 0f);
            troops.Add(t);
        }

        void SpawnDinos(Vector3 at, int raptors, bool withTrike)
        {
            var parent = gm.transform;
            for (int i = 0; i < raptors; i++) Enemy.Spawn(EnemyKind.Raider, at + new Vector3(Random.Range(-2f, 2f), 0, Random.Range(-3f, 3f)), 2, parent);
            if (withTrike) Enemy.Spawn(EnemyKind.Brute, at + new Vector3(-2f, 0, 0), 2, parent);
            Enemy.Spawn(EnemyKind.Flyer, at + new Vector3(0, 0, 4f), 3, parent);
        }

        // ------------------------------------------------------------------ camera + capture

        void View(Vector3 focus, float distance) { rig.focus = focus; rig.distance = distance; rig.Pan(Vector3.zero); }

        IEnumerator Glide(Vector3 f0, float d0, Vector3 f1, float d1, float seconds)
        {
            for (float t = 0; t < seconds; t += Time.deltaTime)
            {
                float k = Mathf.SmoothStep(0, 1, t / seconds);
                View(Vector3.Lerp(f0, f1, k), Mathf.Lerp(d0, d1, k));
                yield return null;
            }
            View(f1, d1);
        }

        IEnumerator Hold(float seconds) { for (float t = 0; t < seconds; t += Time.deltaTime) yield return null; }

        IEnumerator WaitDark()
        {
            float until = Time.time + 25f;
            while (gm.Cycle.Darkness < 0.95f && Time.time < until) yield return null;
            yield return new WaitForSeconds(1f);
        }

        IEnumerator Shot(string name)
        {
            yield return new WaitForEndOfFrame();
            ScreenCapture.CaptureScreenshot(Dir + "/" + name + ".png", 1);
            yield return null; yield return null;
            Debug.Log("[Preview] shot " + name);
        }

        void LateUpdate()
        {
            if (recording) StartCoroutine(Frame());
        }

        IEnumerator Frame()
        {
            yield return new WaitForEndOfFrame();
            var tex = ScreenCapture.CaptureScreenshotAsTexture();
            System.IO.File.WriteAllBytes(Dir + "/frames/f_" + (frame++).ToString("0000") + ".jpg", tex.EncodeToJPG(88));
            Destroy(tex);
        }
    }

    public class PreviewHover : MonoBehaviour
    {
        Vector3 basePos; float phase;
        void Start() { basePos = transform.position; phase = Random.value * 6f; }
        void Update()
        {
            phase += Time.deltaTime;
            transform.position = basePos + new Vector3(Mathf.Sin(phase * 0.6f) * 2.5f, Mathf.Sin(phase * 2f) * 0.15f, Mathf.Cos(phase * 0.6f) * 1.2f);
            transform.rotation = Quaternion.Euler(0, Mathf.Rad2Deg * phase * 0.6f + 90f, Mathf.Sin(phase * 0.6f) * 10f);
        }
    }
}
