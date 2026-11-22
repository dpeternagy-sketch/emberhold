using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Creates the 3D look of a building. If a prefab exists at Resources/Models/{id}
    /// (e.g. from a free Quaternius or Kenney pack) it is used; otherwise a detailed
    /// procedural model is built: timber frames, roofs, windows, chimneys, fences, crops...
    /// Every model faces -z (toward the camera); its footprint is centred on the origin.
    /// </summary>
    public static class BuildingVisuals
    {
        // Palette
        static readonly Color Plaster = new Color(0.9f, 0.84f, 0.72f);
        static readonly Color Timber = new Color(0.29f, 0.19f, 0.12f);
        static readonly Color RoofRed = new Color(0.62f, 0.28f, 0.18f);
        static readonly Color RoofBrown = new Color(0.45f, 0.3f, 0.2f);
        static readonly Color Slate = new Color(0.3f, 0.34f, 0.42f);
        static readonly Color Thatch = new Color(0.74f, 0.62f, 0.34f);
        static readonly Color Stone = new Color(0.66f, 0.63f, 0.58f);
        static readonly Color StoneLight = new Color(0.74f, 0.71f, 0.66f);
        static readonly Color StoneDark = new Color(0.47f, 0.45f, 0.42f);
        static readonly Color Wood = new Color(0.5f, 0.35f, 0.22f);
        static readonly Color WoodLight = new Color(0.68f, 0.52f, 0.33f);
        static readonly Color Soil = new Color(0.38f, 0.27f, 0.17f);
        static readonly Color SoilDark = new Color(0.29f, 0.2f, 0.13f);
        static readonly Color Iron = new Color(0.22f, 0.22f, 0.24f);
        static readonly Color Glow = new Color(1f, 0.66f, 0.3f);
        static readonly Color Fire = new Color(1f, 0.55f, 0.15f);
        static readonly Color BannerRed = new Color(0.66f, 0.12f, 0.12f);

        public static GameObject Create(BuildingDef def, Transform parent)
        {
            var prefab = Resources.Load<GameObject>(def.PrefabPath);
            if (prefab != null)
            {
                var inst = Object.Instantiate(prefab, parent, false);
                inst.name = "Model";
                return inst;
            }

            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var t = root.transform;
            if (Future(def, t)) return root;
            switch (def.id)
            {
                case BuildingCatalog.Castle: Castle(t); break;
                case "house": House(t); break;
                case "farm": Farm(t); break;
                case "lumberjack": Lumberjack(t); break;
                case "quarry": Quarry(t); break;
                case "workshop": Workshop(t); break;
                case "wall": Wall(t); break;
                case "torch": Torch(t); break;
                case "watchtower": Watchtower(t); break;
                case "ballista": Ballista(t); break;
                case "cannon": CannonTower(t); break;
                case "carpenter": Carpenter(t); break;
                case "mine": Mine(t); break;
                case "smelter": Smelter(t); break;
                case "canal": Canal(t); break;
                case "well": Well(t); break;
                case "solar": SolarFallback(t); break;
                case "trap": Trap(t); break;
                case "pen": Pen(t); break;
                default: B(t, new Vector3(0, def.height * 0.5f, 0), new Vector3(def.width * 0.9f, def.height, def.depth * 0.9f), def.color); break;
            }
            // Workplaces and homes are drawn a bit smaller than their plot, leaving a yard around them.
            if (System.Array.IndexOf(Shrunk, def.id) >= 0)
                foreach (Transform c in t) { c.localPosition *= 0.86f; c.localScale *= 0.86f; }
            return root;
        }

        static readonly string[] Shrunk = { "house", "mine" };

        // ---------------- near-future set (KayKit City Builder + Space Base, CC0) ----------------

        static readonly Color Metal = new Color(0.8f, 0.82f, 0.86f);
        static readonly Color MetalDark = new Color(0.3f, 0.33f, 0.38f);
        static readonly Color Hazard = new Color(1f, 0.72f, 0.1f);
        static readonly Color Concrete = new Color(0.72f, 0.72f, 0.7f);
        static readonly Color Laser = new Color(0.3f, 0.95f, 1f);
        static readonly Color Rocket = new Color(0.9f, 0.22f, 0.16f);
        static readonly string[] HouseModels = { "building_A", "building_B", "building_E", "building_F", "building_C" };

        static float Top(Transform part, Transform space) { return ModelLibrary.LocalBounds(part, space).max.y; }

        static void Glowing(GameObject g, Color c, float intensity)
        {
            g.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(c, intensity);
        }

        /// <summary>
        /// Builds the near-future model for this building from the art packs. Returns false when the
        /// packs are missing, so the procedural model is used instead.
        /// </summary>
        static bool Future(BuildingDef def, Transform t)
        {
            if (!ModelLibrary.Has("Models/Base/basemodule_A")) return false;
            switch (def.id)
            {
                case BuildingCatalog.Castle:
                {
                    var pad = ModelLibrary.Spawn("Models/Base/landingpad_large", t, 3.9f, 3.9f, 0.5f);
                    float y = pad != null ? Top(pad, t) : 0f;
                    var core = ModelLibrary.Spawn("Models/Base/basemodule_E", t, 2.4f, 2.4f);
                    core.localPosition = new Vector3(0, y, 0.25f);
                    float top = Top(core, t);
                    var turbine = ModelLibrary.Spawn("Models/Base/windturbine_tall", t, 0.9f, 0.9f);
                    if (turbine != null) turbine.localPosition = new Vector3(1.45f, y, 1.45f);
                    var dish = ModelLibrary.Spawn("Models/Base/roofmodule_solarpanels", t, 1.4f, 1.4f);
                    if (dish != null) dish.localPosition = new Vector3(0, top, 0.25f);
                    // antenna with a red beacon, and the flag of the city
                    Cyl(t, new Vector3(-1.2f, y + 1.3f, 1.2f), 0.04f, 2.6f, MetalDark);
                    Glowing(Ball(t, new Vector3(-1.2f, y + 2.65f, 1.2f), Vector3.one * 0.14f, Rocket), Rocket, 3f);
                    B(t, new Vector3(-1.0f, y + 2.3f, 1.2f), new Vector3(0.4f, 0.26f, 0.02f), new Color(0.15f, 0.45f, 0.85f));
                    return true;
                }
                case "house":
                {
                    var m = HouseModels[Random.Range(0, HouseModels.Length)];
                    return ModelLibrary.Spawn("Models/City/" + m, t, 2f, 2f, 3.2f) != null;
                }
                case "well":
                {
                    var tower = ModelLibrary.Spawn("Models/City/watertower", t, 0.95f, 0.95f, 2.2f);
                    if (tower == null) return false;
                    var w = Cyl(t, new Vector3(0, 0.05f, 0), 0.32f, 0.06f, new Color(0.3f, 0.6f, 0.85f));
                    w.name = "Water";
                    return true;
                }
                case "solar":
                {
                    bool ok = false;
                    foreach (var x in new[] { -0.48f, 0.48f })
                    {
                        var p = ModelLibrary.Spawn("Models/Base/solarpanel", t, 0.9f, 1.8f);
                        if (p != null) { p.localPosition = new Vector3(x, 0, 0); ok = true; }
                    }
                    return ok;
                }
                case "mine":
                    return ModelLibrary.Spawn("Models/Base/drill_structure", t, 2f, 2f) != null;
                case "wall":
                    Barrier(t);
                    return true;
                case "gate":
                    Gate(t);
                    return true;
                case "watchtower":
                {
                    var b = ModelLibrary.Spawn("Models/Base/structure_tall", t, 1.6f, 1.6f, 2.4f);
                    float y = b != null ? Top(b, t) : 0f;
                    if (b == null) B(t, new Vector3(0, 1.2f, 0), new Vector3(1.4f, 2.4f, 1.4f), MetalDark);
                    GunHead(t, y == 0f ? 2.4f : y);
                    return true;
                }
                case "ballista":
                {
                    var b = ModelLibrary.Spawn("Models/Base/structure_low", t, 1.8f, 1.8f, 1.0f);
                    float y = b != null ? Top(b, t) : 0.8f;
                    if (b == null) B(t, new Vector3(0, 0.4f, 0), new Vector3(1.6f, 0.8f, 1.6f), MetalDark);
                    LaserHead(t, y);
                    return true;
                }
                case "cannon":
                {
                    var b = ModelLibrary.Spawn("Models/Base/landingpad_small", t, 1.9f, 1.9f, 0.6f);
                    float y = b != null ? Top(b, t) : 0.3f;
                    Cyl(t, new Vector3(0, y + 0.3f, 0), 0.55f, 0.6f, MetalDark);
                    MissileHead(t, y + 0.6f);
                    return true;
                }
            }
            return false;
        }

        static Transform TurretRoot(Transform t, float y)
        {
            var turret = new GameObject("Turret").transform;
            turret.SetParent(t, false);
            turret.localPosition = new Vector3(0, y, 0);
            return turret;
        }

        /// <summary>Twin-barrel gun. Barrels point along -Z (the turret code aims -Z at the target).</summary>
        static void GunHead(Transform t, float y)
        {
            Cyl(t, new Vector3(0, y + 0.06f, 0), 0.42f, 0.12f, Hazard);
            var h = TurretRoot(t, y + 0.12f);
            B(h, new Vector3(0, 0.22f, 0.05f), new Vector3(0.62f, 0.4f, 0.7f), Metal);
            B(h, new Vector3(0, 0.44f, 0.05f), new Vector3(0.5f, 0.06f, 0.6f), MetalDark);
            foreach (var x in new[] { -0.14f, 0.14f })
            {
                Cyl(h, new Vector3(x, 0.24f, -0.55f), 0.06f, 0.8f, MetalDark, 90, 0, 0);
                Cyl(h, new Vector3(x, 0.24f, -0.95f), 0.08f, 0.1f, MetalDark * 0.7f, 90, 0, 0);
            }
            B(h, new Vector3(0.32f, 0.24f, 0.1f), new Vector3(0.06f, 0.2f, 0.4f), Hazard);
            B(h, new Vector3(-0.32f, 0.24f, 0.1f), new Vector3(0.06f, 0.2f, 0.4f), Hazard);
        }

        /// <summary>Laser emitter with a glowing cyan lens.</summary>
        static void LaserHead(Transform t, float y)
        {
            Cyl(t, new Vector3(0, y + 0.1f, 0), 0.5f, 0.2f, MetalDark);
            var h = TurretRoot(t, y + 0.2f);
            Ball(h, new Vector3(0, 0.35f, 0.1f), new Vector3(0.7f, 0.55f, 0.8f), Metal);
            Cyl(h, new Vector3(0, 0.38f, -0.55f), 0.09f, 1.0f, MetalDark, 90, 0, 0);
            foreach (var z in new[] { -0.3f, -0.55f, -0.8f })
                Cyl(h, new Vector3(0, 0.38f, z), 0.14f, 0.05f, Metal, 90, 0, 0);
            Glowing(Ball(h, new Vector3(0, 0.38f, -1.08f), Vector3.one * 0.16f, Laser), Laser, 4f);
            Glowing(B(h, new Vector3(0, 0.5f, 0.2f), new Vector3(0.5f, 0.03f, 0.3f), Laser), Laser, 2f);
        }

        /// <summary>Box of six missiles with red tips.</summary>
        static void MissileHead(Transform t, float y)
        {
            var h = TurretRoot(t, y);
            B(h, new Vector3(0, 0.15f, 0), new Vector3(0.3f, 0.3f, 0.3f), MetalDark);
            var pod = new GameObject("Pod").transform;
            pod.SetParent(h, false);
            pod.localPosition = new Vector3(0, 0.55f, 0);
            pod.localRotation = Quaternion.Euler(-18, 0, 0);    // tilted up
            B(pod, Vector3.zero, new Vector3(0.95f, 0.62f, 0.9f), new Color(0.42f, 0.5f, 0.35f));
            B(pod, new Vector3(0, 0.33f, 0), new Vector3(0.95f, 0.05f, 0.9f), Hazard);
            for (int r = 0; r < 2; r++)
                for (int c = 0; c < 3; c++)
                {
                    var p = new Vector3(-0.3f + c * 0.3f, -0.14f + r * 0.28f, -0.46f);
                    Cyl(pod, p, 0.11f, 0.04f, MetalDark * 0.6f, 90, 0, 0);
                    Ball(pod, p + new Vector3(0, 0, -0.02f), Vector3.one * 0.15f, Rocket);
                }
        }

        /// <summary>Concrete road barrier with a yellow/black stripe.</summary>
        static readonly Color TechWhite = new Color(0.93f, 0.94f, 0.95f);   // palette #F4F6F8
        static readonly Color TeamBlue = new Color(0.18f, 0.49f, 0.96f);    // palette #2E7DF6

        /// <summary>Unbreakable tech wall: white panels with a team-blue stripe.</summary>
        static void Barrier(Transform t)
        {
            B(t, new Vector3(0, 0.1f, 0), new Vector3(1.02f, 0.2f, 0.78f), MetalDark);
            B(t, new Vector3(0, 0.62f, 0), new Vector3(1.0f, 0.9f, 0.52f), TechWhite);
            B(t, new Vector3(0, 1.13f, 0), new Vector3(1.0f, 0.14f, 0.4f), TechWhite * 0.9f);
            B(t, new Vector3(0, 0.78f, -0.265f), new Vector3(1.0f, 0.12f, 0.02f), TeamBlue);
            B(t, new Vector3(0, 0.78f, 0.265f), new Vector3(1.0f, 0.12f, 0.02f), TeamBlue);
        }

        /// <summary>The way in: two tall posts and a heavy blue door with a cyan light. Dinos must break it.</summary>
        static void Gate(Transform t)
        {
            B(t, new Vector3(0, 0.08f, 0), new Vector3(1.04f, 0.16f, 0.82f), MetalDark);
            foreach (var x in new[] { -0.42f, 0.42f })
            {
                B(t, new Vector3(x, 0.95f, 0), new Vector3(0.2f, 1.9f, 0.66f), TechWhite);
                B(t, new Vector3(x, 1.92f, 0), new Vector3(0.26f, 0.08f, 0.72f), TeamBlue);
            }
            B(t, new Vector3(0, 0.75f, 0), new Vector3(0.66f, 1.3f, 0.22f), TeamBlue * 0.85f);
            B(t, new Vector3(0, 0.75f, -0.115f), new Vector3(0.04f, 1.2f, 0.02f), MetalDark);
            B(t, new Vector3(0, 0.75f, 0.115f), new Vector3(0.04f, 1.2f, 0.02f), MetalDark);
            B(t, new Vector3(0, 1.62f, 0), new Vector3(0.66f, 0.12f, 0.5f), TechWhite * 0.9f);
            Glowing(B(t, new Vector3(0, 1.62f, -0.26f), new Vector3(0.5f, 0.05f, 0.02f), Laser), Laser, 3f);
            Glowing(B(t, new Vector3(0, 1.62f, 0.26f), new Vector3(0.5f, 0.05f, 0.02f), Laser), Laser, 3f);
        }

        /// <summary>Steel floor plate with open jaws and hazard stripes: dinosaurs that step on it are caught.</summary>
        static void Trap(Transform t)
        {
            Cyl(t, new Vector3(0, 0.03f, 0), 0.47f, 0.06f, MetalDark);
            Cyl(t, new Vector3(0, 0.065f, 0), 0.4f, 0.02f, Hazard);
            Cyl(t, new Vector3(0, 0.08f, 0), 0.3f, 0.02f, new Color(0.15f, 0.15f, 0.15f));
            foreach (var sgn in new[] { -1f, 1f })
                for (int i = 0; i < 5; i++)
                {
                    float x = -0.28f + i * 0.14f;
                    B(t, new Vector3(x, 0.14f, sgn * 0.3f), new Vector3(0.05f, 0.14f, 0.03f), Metal, sgn * 25f, 0, 0);
                }
            Glowing(Ball(t, new Vector3(0, 0.12f, 0), Vector3.one * 0.1f, Laser), Laser, 3f);
        }

        /// <summary>Open pen with steel posts and glowing laser fence bars, a shelter and a feeding trough.</summary>
        static void Pen(Transform t)
        {
            B(t, new Vector3(0, 0.025f, 0), new Vector3(2.9f, 0.05f, 2.9f), new Color(0.5f, 0.42f, 0.3f));   // packed dirt
            float h = 1.35f;
            for (int i = 0; i <= 4; i++)
            {
                float a = -1.4f + i * 0.7f;
                foreach (var p in new[] { new Vector3(a, 0, -1.4f), new Vector3(a, 0, 1.4f), new Vector3(-1.4f, 0, a), new Vector3(1.4f, 0, a) })
                {
                    B(t, p + new Vector3(0, h * 0.5f, 0), new Vector3(0.1f, h, 0.1f), MetalDark);
                    B(t, p + new Vector3(0, h + 0.03f, 0), new Vector3(0.14f, 0.06f, 0.14f), Hazard);
                }
            }
            foreach (var y in new[] { 0.45f, 0.95f })
            {
                Glowing(B(t, new Vector3(0, y, 1.4f), new Vector3(2.8f, 0.04f, 0.04f), Laser), Laser, 2.5f);
                Glowing(B(t, new Vector3(-1.4f, y, 0), new Vector3(0.04f, 0.04f, 2.8f), Laser), Laser, 2.5f);
                Glowing(B(t, new Vector3(1.4f, y, 0), new Vector3(0.04f, 0.04f, 2.8f), Laser), Laser, 2.5f);
                // front: a gate gap in the middle
                Glowing(B(t, new Vector3(-0.95f, y, -1.4f), new Vector3(0.9f, 0.04f, 0.04f), Laser), Laser, 2.5f);
                Glowing(B(t, new Vector3(0.95f, y, -1.4f), new Vector3(0.9f, 0.04f, 0.04f), Laser), Laser, 2.5f);
            }
            // shelter at the back
            B(t, new Vector3(0.75f, 0.45f, 0.95f), new Vector3(1.1f, 0.9f, 0.7f), Metal);
            B(t, new Vector3(0.75f, 0.95f, 0.95f), new Vector3(1.25f, 0.08f, 0.85f), new Color(0.3f, 0.55f, 0.35f));
            // trough with greens
            B(t, new Vector3(-0.8f, 0.15f, 0.9f), new Vector3(0.9f, 0.3f, 0.35f), MetalDark);
            B(t, new Vector3(-0.8f, 0.3f, 0.9f), new Vector3(0.8f, 0.06f, 0.25f), new Color(0.35f, 0.65f, 0.25f));
        }

        static void SolarFallback(Transform t)
        {
            foreach (var x in new[] { -0.48f, 0.48f })
            {
                B(t, new Vector3(x, 0.3f, 0), new Vector3(0.1f, 0.6f, 0.1f), MetalDark);
                B(t, new Vector3(x, 0.62f, 0), new Vector3(0.85f, 0.05f, 1.7f), new Color(0.15f, 0.25f, 0.5f), 25, 0, 0);
            }
        }

        // ---------------- helpers ----------------

        static GameObject B(Transform p, Vector3 pos, Vector3 size, Color c, float rx = 0, float ry = 0, float rz = 0)
        {
            var g = Util.Prim(PrimitiveType.Cube, p, pos, size, c);
            if (rx != 0 || ry != 0 || rz != 0) g.transform.localRotation = Quaternion.Euler(rx, ry, rz);
            return g;
        }

        static GameObject Cyl(Transform p, Vector3 pos, float radius, float height, Color c, float rx = 0, float ry = 0, float rz = 0)
        {
            var g = Util.Prim(PrimitiveType.Cylinder, p, pos, new Vector3(radius * 2f, height * 0.5f, radius * 2f), c);
            if (rx != 0 || ry != 0 || rz != 0) g.transform.localRotation = Quaternion.Euler(rx, ry, rz);
            return g;
        }

        static GameObject Ball(Transform p, Vector3 pos, Vector3 size, Color c)
        {
            return Util.Prim(PrimitiveType.Sphere, p, pos, size, c);
        }

        /// <summary>Gable roof with the ridge running along x. span = depth across the slopes.</summary>
        static GameObject Roof(Transform p, Vector3 pos, float length, float span, float height, Color c)
        {
            var g = MeshFactory.Create(MeshFactory.Gable(), p, pos, new Vector3(span, height, length), c);
            g.transform.localRotation = Quaternion.Euler(0, 90, 0);
            // ridge cap
            B(p, pos + new Vector3(0, height + 0.02f, 0), new Vector3(length + 0.02f, 0.06f, 0.08f), c * 0.75f);
            return g;
        }

        static GameObject Cone(Transform p, Vector3 pos, float radius, float height, Color c, int segments = 10)
        {
            return MeshFactory.Create(MeshFactory.Cone(segments), p, pos, new Vector3(radius * 2f, height, radius * 2f), c);
        }

        static void Window(Transform p, Vector3 pos, float w, float h, bool facingX = false)
        {
            var size = facingX ? new Vector3(0.04f, h, w) : new Vector3(w, h, 0.04f);
            var frame = facingX ? new Vector3(0.03f, h + 0.08f, w + 0.08f) : new Vector3(w + 0.08f, h + 0.08f, 0.03f);
            B(p, pos, frame, Timber);
            var glass = B(p, pos + (facingX ? new Vector3(0.012f * Mathf.Sign(pos.x), 0, 0) : new Vector3(0, 0, -0.012f)), size, Color.white);
            glass.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(0.35f, 0.25f, 0.12f), 1.4f);
            // cross bars
            B(p, pos + (facingX ? new Vector3(0.03f * Mathf.Sign(pos.x), 0, 0) : new Vector3(0, 0, -0.03f)), facingX ? new Vector3(0.01f, h, 0.025f) : new Vector3(0.025f, h, 0.01f), Timber);
            B(p, pos + (facingX ? new Vector3(0.03f * Mathf.Sign(pos.x), 0, 0) : new Vector3(0, 0, -0.03f)), facingX ? new Vector3(0.01f, 0.025f, w) : new Vector3(w, 0.025f, 0.01f), Timber);
        }

        static void Merlons(Transform p, Vector3 from, Vector3 to, float y, float size, Color c)
        {
            float len = Vector3.Distance(from, to);
            int n = Mathf.Max(2, Mathf.RoundToInt(len / (size * 2f)));
            for (int i = 0; i <= n; i++)
            {
                if (i % 2 == 1) continue;
                var pos = Vector3.Lerp(from, to, i / (float)n);
                B(p, new Vector3(pos.x, y, pos.z), new Vector3(size, size, size), c);
            }
        }

        static void Flame(Transform p, Vector3 pos, float size)
        {
            var outer = Ball(p, pos, new Vector3(size, size * 1.4f, size), Color.white);
            outer.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(Fire, 3f);
            var inner = Ball(p, pos + Vector3.down * size * 0.15f, new Vector3(size * 0.55f, size * 0.8f, size * 0.55f), Color.white);
            inner.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(new Color(1f, 0.9f, 0.5f), 4f);
        }

        static void Barrel(Transform p, Vector3 pos)
        {
            Cyl(p, pos + Vector3.up * 0.16f, 0.12f, 0.32f, Wood);
            Cyl(p, pos + Vector3.up * 0.08f, 0.125f, 0.03f, Iron);
            Cyl(p, pos + Vector3.up * 0.25f, 0.125f, 0.03f, Iron);
        }

        static void Crate(Transform p, Vector3 pos, float s)
        {
            B(p, pos + Vector3.up * s * 0.5f, Vector3.one * s, WoodLight);
            B(p, pos + new Vector3(0, s * 0.5f, -s * 0.51f), new Vector3(s * 0.15f, s, 0.01f), Wood, 0, 0, 45);
        }

        // ---------------- buildings ----------------

        static void House(Transform t)
        {
            B(t, new Vector3(0, 0.1f, 0), new Vector3(1.72f, 0.2f, 1.52f), StoneDark);
            B(t, new Vector3(0, 0.7f, 0), new Vector3(1.6f, 1.0f, 1.4f), Plaster);
            // timber frame
            foreach (var x in new[] { -0.8f, 0f, 0.8f })
                foreach (var z in new[] { -0.71f, 0.71f })
                    B(t, new Vector3(x, 0.7f, z), new Vector3(0.08f, 1.0f, 0.05f), Timber);
            foreach (var x in new[] { -0.81f, 0.81f })
                foreach (var z in new[] { -0.7f, 0f, 0.7f })
                    B(t, new Vector3(x, 0.7f, z), new Vector3(0.05f, 1.0f, 0.08f), Timber);
            foreach (var y in new[] { 0.22f, 0.72f, 1.18f })
            {
                B(t, new Vector3(0, y, -0.715f), new Vector3(1.64f, 0.07f, 0.04f), Timber);
                B(t, new Vector3(0, y, 0.715f), new Vector3(1.64f, 0.07f, 0.04f), Timber);
                B(t, new Vector3(-0.815f, y, 0), new Vector3(0.04f, 0.07f, 1.44f), Timber);
                B(t, new Vector3(0.815f, y, 0), new Vector3(0.04f, 0.07f, 1.44f), Timber);
            }
            B(t, new Vector3(-0.4f, 0.95f, -0.72f), new Vector3(0.05f, 0.62f, 0.03f), Timber, 0, 0, 50);
            B(t, new Vector3(0.4f, 0.95f, -0.72f), new Vector3(0.05f, 0.62f, 0.03f), Timber, 0, 0, -50);
            // roof with overhang + chimney
            Roof(t, new Vector3(0, 1.2f, 0), 1.95f, 1.75f, 0.85f, RoofRed);
            B(t, new Vector3(0.52f, 1.75f, 0.3f), new Vector3(0.24f, 0.75f, 0.24f), Stone);
            B(t, new Vector3(0.52f, 2.15f, 0.3f), new Vector3(0.3f, 0.07f, 0.3f), StoneDark);
            // door, windows, details
            B(t, new Vector3(0, 0.5f, -0.725f), new Vector3(0.42f, 0.66f, 0.03f), Timber);
            B(t, new Vector3(0, 0.48f, -0.74f), new Vector3(0.32f, 0.58f, 0.03f), Wood);
            Ball(t, new Vector3(0.1f, 0.48f, -0.76f), Vector3.one * 0.04f, Iron);
            B(t, new Vector3(0, 0.215f, -0.85f), new Vector3(0.5f, 0.05f, 0.22f), StoneLight); // step
            Window(t, new Vector3(-0.46f, 0.92f, -0.72f), 0.26f, 0.26f);
            Window(t, new Vector3(0.46f, 0.92f, -0.72f), 0.26f, 0.26f);
            Window(t, new Vector3(0.82f, 0.92f, 0f), 0.26f, 0.26f, true);
            Window(t, new Vector3(-0.82f, 0.92f, 0f), 0.26f, 0.26f, true);
            B(t, new Vector3(-0.46f, 0.76f, -0.78f), new Vector3(0.3f, 0.06f, 0.1f), new Color(0.3f, 0.5f, 0.2f)); // flower box
            Ball(t, new Vector3(-0.52f, 0.8f, -0.79f), Vector3.one * 0.07f, new Color(0.9f, 0.3f, 0.3f));
            Ball(t, new Vector3(-0.4f, 0.8f, -0.79f), Vector3.one * 0.07f, new Color(0.95f, 0.85f, 0.3f));
            Barrel(t, new Vector3(0.62f, 0, -0.9f));
        }

        static void Farm(Transform t)
        {
            B(t, new Vector3(0, 0.03f, 0), new Vector3(2.9f, 0.06f, 2.9f), Soil);
            var wheat = new Color(0.86f, 0.72f, 0.32f);
            var green = new Color(0.4f, 0.6f, 0.22f);
            for (int r = 0; r < 6; r++)
            {
                float z = -1.15f + r * 0.46f;
                B(t, new Vector3(0, 0.07f, z), new Vector3(2.6f, 0.06f, 0.2f), SoilDark);
                var crop = r < 3 ? wheat : green;
                for (int i = 0; i < 11; i++)
                {
                    float x = -1.2f + i * 0.24f + Random.Range(-0.04f, 0.04f);
                    float h = Random.Range(0.26f, 0.4f) * (r < 3 ? 1f : 0.7f);
                    B(t, new Vector3(x, 0.08f + h * 0.5f, z), new Vector3(0.08f, h, 0.08f), crop * Random.Range(0.9f, 1.1f), 0, Random.Range(0, 90f), Random.Range(-8f, 8f));
                    if (r < 3) B(t, new Vector3(x, 0.1f + h, z), new Vector3(0.06f, 0.12f, 0.06f), wheat * 1.08f);
                }
            }
            // fence with a gap at the front
            for (int i = 0; i <= 8; i++)
            {
                float a = -1.45f + i * (2.9f / 8f);
                B(t, new Vector3(a, 0.25f, 1.45f), new Vector3(0.07f, 0.5f, 0.07f), Wood);
                B(t, new Vector3(-1.45f, 0.25f, a), new Vector3(0.07f, 0.5f, 0.07f), Wood);
                B(t, new Vector3(1.45f, 0.25f, a), new Vector3(0.07f, 0.5f, 0.07f), Wood);
                if (Mathf.Abs(a) > 0.4f) B(t, new Vector3(a, 0.25f, -1.45f), new Vector3(0.07f, 0.5f, 0.07f), Wood);
            }
            foreach (var y in new[] { 0.2f, 0.4f })
            {
                B(t, new Vector3(0, y, 1.45f), new Vector3(2.9f, 0.05f, 0.04f), WoodLight);
                B(t, new Vector3(-1.45f, y, 0), new Vector3(0.04f, 0.05f, 2.9f), WoodLight);
                B(t, new Vector3(1.45f, y, 0), new Vector3(0.04f, 0.05f, 2.9f), WoodLight);
                B(t, new Vector3(-0.95f, y, -1.45f), new Vector3(1.0f, 0.05f, 0.04f), WoodLight);
                B(t, new Vector3(0.95f, y, -1.45f), new Vector3(1.0f, 0.05f, 0.04f), WoodLight);
            }
            // hay bales and a scarecrow
            Cyl(t, new Vector3(1.15f, 0.18f, -1.15f), 0.18f, 0.32f, Thatch, 0, 0, 90);
            Cyl(t, new Vector3(1.15f, 0.18f, -0.8f), 0.18f, 0.32f, Thatch * 0.95f, 0, 0, 90);
            B(t, new Vector3(0, 0.5f, 0.35f), new Vector3(0.05f, 1.0f, 0.05f), Wood);
            B(t, new Vector3(0, 0.75f, 0.35f), new Vector3(0.6f, 0.05f, 0.05f), Wood);
            B(t, new Vector3(0, 0.66f, 0.35f), new Vector3(0.26f, 0.32f, 0.12f), new Color(0.45f, 0.35f, 0.55f));
            Ball(t, new Vector3(0, 1.02f, 0.35f), Vector3.one * 0.18f, Thatch);
            Cone(t, new Vector3(0, 1.08f, 0.35f), 0.17f, 0.18f, RoofBrown, 8);
        }

        static void Lumberjack(Transform t)
        {
            B(t, new Vector3(-0.2f, 0.06f, 0.1f), new Vector3(1.25f, 0.12f, 1.25f), StoneDark);
            // log cabin walls
            for (int i = 0; i < 5; i++)
            {
                float y = 0.2f + i * 0.17f;
                Cyl(t, new Vector3(-0.2f, y, -0.5f), 0.09f, 1.3f, i % 2 == 0 ? Wood : Wood * 0.9f, 0, 0, 90);
                Cyl(t, new Vector3(-0.2f, y, 0.7f), 0.09f, 1.3f, Wood, 0, 0, 90);
                Cyl(t, new Vector3(-0.78f, y + 0.08f, 0.1f), 0.09f, 1.35f, Wood * 0.95f, 90, 0, 0);
                Cyl(t, new Vector3(0.38f, y + 0.08f, 0.1f), 0.09f, 1.35f, Wood * 0.95f, 90, 0, 0);
            }
            Roof(t, new Vector3(-0.2f, 1.05f, 0.1f), 1.55f, 1.6f, 0.65f, RoofBrown);
            B(t, new Vector3(-0.2f, 0.45f, -0.59f), new Vector3(0.34f, 0.55f, 0.03f), Timber);
            Window(t, new Vector3(-0.6f, 0.7f, -0.6f), 0.2f, 0.2f);
            // log pile
            for (int row = 0; row < 3; row++)
                for (int i = 0; i < 3 - row; i++)
                    Cyl(t, new Vector3(0.72f, 0.1f + row * 0.17f, -0.55f + i * 0.2f + row * 0.1f), 0.09f, 0.75f, i % 2 == 0 ? WoodLight : Wood, 0, 0, 90);
            for (int i = 0; i < 6; i++)
                Cyl(t, new Vector3(0.72f + Random.Range(-0.05f, 0.05f), 0.08f, -0.55f + i * 0.08f), 0.06f, 0.02f, new Color(0.85f, 0.7f, 0.48f), 0, 0, 90);
            // chopping stump + axe
            Cyl(t, new Vector3(0.6f, 0.15f, 0.5f), 0.18f, 0.3f, Wood);
            Cyl(t, new Vector3(0.6f, 0.305f, 0.5f), 0.17f, 0.01f, new Color(0.85f, 0.7f, 0.48f));
            B(t, new Vector3(0.6f, 0.5f, 0.5f), new Vector3(0.04f, 0.45f, 0.04f), WoodLight, 0, 0, 20);
            B(t, new Vector3(0.53f, 0.36f, 0.5f), new Vector3(0.14f, 0.1f, 0.03f), Iron, 0, 0, 20);
        }

        static void Quarry(Transform t)
        {
            B(t, new Vector3(0, 0.02f, 0), new Vector3(1.95f, 0.04f, 1.95f), new Color(0.5f, 0.48f, 0.45f));
            // terraced rock face at the back
            B(t, new Vector3(0, 0.35f, 0.65f), new Vector3(1.9f, 0.7f, 0.6f), StoneDark);
            B(t, new Vector3(0, 0.75f, 0.8f), new Vector3(1.9f, 0.4f, 0.35f), Stone);
            B(t, new Vector3(-0.4f, 0.2f, 0.25f), new Vector3(0.8f, 0.4f, 0.3f), Stone * 0.95f);
            // cut blocks
            for (int i = 0; i < 3; i++)
                for (int j = 0; j < 2 - (i / 2); j++)
                    B(t, new Vector3(-0.55f + i * 0.32f, 0.12f + j * 0.24f, -0.55f), new Vector3(0.28f, 0.22f, 0.28f), j == 0 ? StoneLight : Stone);
            for (int i = 0; i < 8; i++)
                Ball(t, new Vector3(Random.Range(-0.8f, 0.8f), 0.05f, Random.Range(-0.8f, 0.1f)), new Vector3(0.12f, 0.08f, 0.1f) * Random.Range(0.7f, 1.4f), StoneDark);
            // wooden crane
            B(t, new Vector3(0.65f, 0.7f, -0.2f), new Vector3(0.08f, 1.4f, 0.08f), Wood);
            B(t, new Vector3(0.35f, 1.38f, -0.2f), new Vector3(0.75f, 0.07f, 0.07f), Wood);
            B(t, new Vector3(0.65f, 0.45f, -0.2f), new Vector3(0.05f, 0.6f, 0.05f), Wood, 0, 0, 35);
            B(t, new Vector3(0.05f, 1.1f, -0.2f), new Vector3(0.015f, 0.55f, 0.015f), new Color(0.7f, 0.6f, 0.45f));
            B(t, new Vector3(0.05f, 0.78f, -0.2f), new Vector3(0.24f, 0.18f, 0.24f), StoneLight);
            // cart
            B(t, new Vector3(-0.6f, 0.25f, -0.1f), new Vector3(0.45f, 0.18f, 0.3f), Wood);
            Cyl(t, new Vector3(-0.6f, 0.14f, -0.27f), 0.12f, 0.03f, Timber, 90, 0, 0);
            Cyl(t, new Vector3(-0.6f, 0.14f, 0.07f), 0.12f, 0.03f, Timber, 90, 0, 0);
        }

        static void Workshop(Transform t)
        {
            B(t, new Vector3(0, 0.08f, 0), new Vector3(1.9f, 0.16f, 1.8f), StoneDark);
            B(t, new Vector3(0, 0.5f, 0.2f), new Vector3(1.7f, 0.7f, 1.3f), Stone);
            B(t, new Vector3(0, 1.1f, 0.2f), new Vector3(1.7f, 0.5f, 1.3f), Plaster);
            foreach (var x in new[] { -0.85f, -0.3f, 0.3f, 0.85f })
                B(t, new Vector3(x, 1.1f, -0.455f), new Vector3(0.07f, 0.5f, 0.04f), Timber);
            B(t, new Vector3(0, 0.87f, -0.46f), new Vector3(1.74f, 0.07f, 0.05f), Timber);
            Roof(t, new Vector3(0, 1.35f, 0.2f), 2.0f, 1.6f, 0.75f, Slate);
            // chimney with glow
            B(t, new Vector3(0.6f, 1.75f, 0.55f), new Vector3(0.32f, 1.1f, 0.32f), StoneDark);
            var glow = B(t, new Vector3(0.6f, 2.31f, 0.55f), new Vector3(0.22f, 0.02f, 0.22f), Color.white);
            glow.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(Fire, 2f);
            Window(t, new Vector3(-0.55f, 1.1f, -0.46f), 0.24f, 0.22f);
            Window(t, new Vector3(0.55f, 1.1f, -0.46f), 0.24f, 0.22f);
            // open forge under an awning
            B(t, new Vector3(0, 0.82f, -0.75f), new Vector3(1.6f, 0.05f, 0.65f), RoofBrown, -12, 0, 0);
            B(t, new Vector3(-0.75f, 0.42f, -1.0f), new Vector3(0.06f, 0.84f, 0.06f), Wood);
            B(t, new Vector3(0.75f, 0.42f, -1.0f), new Vector3(0.06f, 0.84f, 0.06f), Wood);
            B(t, new Vector3(-0.45f, 0.22f, -0.65f), new Vector3(0.5f, 0.3f, 0.4f), StoneDark);
            var coals = B(t, new Vector3(-0.45f, 0.38f, -0.65f), new Vector3(0.36f, 0.04f, 0.28f), Color.white);
            coals.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(Fire, 2.5f);
            B(t, new Vector3(0.1f, 0.12f, -0.75f), new Vector3(0.12f, 0.24f, 0.12f), Wood);
            B(t, new Vector3(0.1f, 0.27f, -0.75f), new Vector3(0.3f, 0.08f, 0.12f), Iron);
            // weapon rack
            B(t, new Vector3(0.55f, 0.35f, -0.65f), new Vector3(0.45f, 0.05f, 0.08f), Wood);
            for (int i = 0; i < 3; i++)
                B(t, new Vector3(0.4f + i * 0.15f, 0.35f, -0.68f), new Vector3(0.03f, 0.6f, 0.03f), new Color(0.78f, 0.8f, 0.84f));
        }

        static void Wall(Transform t)
        {
            B(t, new Vector3(0, 0.08f, 0), new Vector3(1.04f, 0.16f, 0.74f), StoneDark);
            B(t, new Vector3(0, 0.7f, 0), new Vector3(1.0f, 1.2f, 0.62f), Stone);
            // irregular stones on the face
            for (int row = 0; row < 4; row++)
                for (int i = 0; i < 3; i++)
                {
                    float x = -0.33f + i * 0.33f + (row % 2) * 0.16f;
                    if (x > 0.45f) continue;
                    var c = (row + i) % 3 == 0 ? StoneLight : (row + i) % 3 == 1 ? Stone * 0.93f : StoneDark * 1.15f;
                    B(t, new Vector3(x, 0.28f + row * 0.28f, -0.315f), new Vector3(0.3f, 0.24f, 0.02f), c);
                    B(t, new Vector3(x, 0.28f + row * 0.28f, 0.315f), new Vector3(0.3f, 0.24f, 0.02f), c);
                }
            B(t, new Vector3(0, 1.32f, 0), new Vector3(1.02f, 0.06f, 0.66f), StoneDark);
            B(t, new Vector3(-0.3f, 1.47f, -0.2f), new Vector3(0.3f, 0.26f, 0.2f), Stone);
            B(t, new Vector3(0.3f, 1.47f, -0.2f), new Vector3(0.3f, 0.26f, 0.2f), Stone);
            B(t, new Vector3(-0.3f, 1.47f, 0.2f), new Vector3(0.3f, 0.26f, 0.2f), Stone);
            B(t, new Vector3(0.3f, 1.47f, 0.2f), new Vector3(0.3f, 0.26f, 0.2f), Stone);
        }

        static void Torch(Transform t)
        {
            B(t, new Vector3(0, 0.06f, 0), new Vector3(0.36f, 0.12f, 0.36f), StoneDark);
            Cyl(t, new Vector3(0, 0.65f, 0), 0.05f, 1.1f, Wood);
            Cyl(t, new Vector3(0, 1.22f, 0), 0.11f, 0.12f, Iron);
            Cyl(t, new Vector3(0, 1.15f, 0), 0.07f, 0.05f, Iron);
            Flame(t, new Vector3(0, 1.38f, 0), 0.2f);
        }

        static void Watchtower(Transform t)
        {
            B(t, new Vector3(0, 0.06f, 0), new Vector3(1.8f, 0.12f, 1.8f), StoneDark);
            float top = 2.6f;
            foreach (var x in new[] { -0.65f, 0.65f })
                foreach (var z in new[] { -0.65f, 0.65f })
                    Cyl(t, new Vector3(x, top * 0.5f, z), 0.08f, top, Wood);
            // cross braces
            for (int k = 0; k < 2; k++)
            {
                float y = 0.7f + k * 1.0f;
                B(t, new Vector3(0, y, -0.66f), new Vector3(0.05f, 1.6f, 0.04f), Wood * 0.9f, 0, 0, 52);
                B(t, new Vector3(0, y, -0.66f), new Vector3(0.05f, 1.6f, 0.04f), Wood * 0.9f, 0, 0, -52);
                B(t, new Vector3(-0.66f, y, 0), new Vector3(0.04f, 1.6f, 0.05f), Wood * 0.9f, 52, 0, 0);
                B(t, new Vector3(0.66f, y, 0), new Vector3(0.04f, 1.6f, 0.05f), Wood * 0.9f, -52, 0, 0);
            }
            // platform with railing
            B(t, new Vector3(0, top, 0), new Vector3(1.7f, 0.1f, 1.7f), WoodLight);
            foreach (var s in new[] { -1f, 1f })
            {
                B(t, new Vector3(0, top + 0.35f, s * 0.82f), new Vector3(1.7f, 0.06f, 0.05f), Wood);
                B(t, new Vector3(s * 0.82f, top + 0.35f, 0), new Vector3(0.05f, 0.06f, 1.7f), Wood);
                for (int i = 0; i < 5; i++)
                {
                    float a = -0.8f + i * 0.4f;
                    B(t, new Vector3(a, top + 0.2f, s * 0.82f), new Vector3(0.04f, 0.32f, 0.04f), Wood);
                    B(t, new Vector3(s * 0.82f, top + 0.2f, a), new Vector3(0.04f, 0.32f, 0.04f), Wood);
                }
            }
            // roof on posts
            foreach (var x in new[] { -0.75f, 0.75f })
                foreach (var z in new[] { -0.75f, 0.75f })
                    B(t, new Vector3(x, top + 0.55f, z), new Vector3(0.06f, 1.1f, 0.06f), Wood);
            Cone(t, new Vector3(0, top + 1.1f, 0), 1.25f, 0.9f, RoofRed, 4);
            B(t, new Vector3(0, top + 2.15f, 0), new Vector3(0.03f, 0.4f, 0.03f), Wood);
            B(t, new Vector3(0.12f, top + 2.27f, 0), new Vector3(0.22f, 0.14f, 0.01f), BannerRed);
            // brazier
            Cyl(t, new Vector3(0, top + 0.15f, 0), 0.16f, 0.12f, Iron);
            Flame(t, new Vector3(0, top + 0.3f, 0), 0.2f);
            // ladder
            B(t, new Vector3(-0.18f, top * 0.5f, -0.92f), new Vector3(0.04f, top, 0.04f), WoodLight, -8, 0, 0);
            B(t, new Vector3(0.18f, top * 0.5f, -0.92f), new Vector3(0.04f, top, 0.04f), WoodLight, -8, 0, 0);
            for (int i = 0; i < 9; i++)
            {
                float y = 0.2f + i * 0.28f;
                B(t, new Vector3(0, y, -0.92f + (y - top * 0.5f) * 0.14f), new Vector3(0.36f, 0.03f, 0.03f), WoodLight);
            }
        }

        /// <summary>Wooden platform with a giant crossbow. The "Turret" child turns toward enemies.</summary>
        static void Ballista(Transform t)
        {
            B(t, new Vector3(0, 0.06f, 0), new Vector3(1.8f, 0.12f, 1.8f), StoneDark);
            B(t, new Vector3(0, 0.35f, 0), new Vector3(1.5f, 0.45f, 1.5f), Wood);
            foreach (var x in new[] { -0.7f, 0.7f })
                foreach (var z in new[] { -0.7f, 0.7f })
                    B(t, new Vector3(x, 0.35f, z), new Vector3(0.12f, 0.5f, 0.12f), Timber);
            B(t, new Vector3(0, 0.6f, 0), new Vector3(1.6f, 0.06f, 1.6f), WoodLight);
            Cyl(t, new Vector3(0, 0.72f, 0), 0.22f, 0.2f, Iron);
            var turret = new GameObject("Turret").transform;
            turret.SetParent(t, false);
            turret.localPosition = new Vector3(0, 0.85f, 0);
            B(turret, new Vector3(0, 0.12f, 0), new Vector3(0.3f, 0.25f, 0.4f), Wood);
            B(turret, new Vector3(0, 0.3f, -0.1f), new Vector3(0.14f, 0.1f, 1.4f), WoodLight, -6, 0, 0);       // stock
            foreach (var sgn in new[] { -1f, 1f })
                B(turret, new Vector3(sgn * 0.42f, 0.34f, -0.62f), new Vector3(0.8f, 0.08f, 0.08f), Timber, 0, sgn * 28f, 0); // bow arms
            B(turret, new Vector3(0, 0.34f, -0.45f), new Vector3(1.3f, 0.015f, 0.015f), new Color(0.85f, 0.8f, 0.7f)); // string
            B(turret, new Vector3(0, 0.38f, -0.5f), new Vector3(0.05f, 0.05f, 1.0f), Iron);                     // loaded bolt
            Cone(turret, new Vector3(0, 0.38f, -1.02f), 0.05f, 0.14f, Iron, 6).transform.localRotation = Quaternion.Euler(-90, 0, 0);
            Barrel(t, new Vector3(0.6f, 0.7f, 0.55f));
            for (int i = 0; i < 3; i++) B(t, new Vector3(-0.55f, 0.68f + i * 0.05f, 0.5f), new Vector3(0.05f, 0.05f, 0.7f), Iron, 0, 10 * i, 0);
        }

        /// <summary>Round stone tower with a black cannon on top. The "Turret" child turns toward enemies.</summary>
        static void CannonTower(Transform t)
        {
            Cyl(t, new Vector3(0, 0.08f, 0), 0.98f, 0.16f, StoneDark);
            Cyl(t, new Vector3(0, 0.75f, 0), 0.85f, 1.3f, Stone);
            for (int i = 0; i < 4; i++) Cyl(t, new Vector3(0, 0.25f + i * 0.33f, 0), 0.87f, 0.04f, StoneDark); // stone courses
            Cyl(t, new Vector3(0, 1.45f, 0), 0.95f, 0.12f, StoneLight);
            for (int i = 0; i < 8; i++)
            {
                float a = i * Mathf.PI / 4f;
                B(t, new Vector3(Mathf.Cos(a) * 0.85f, 1.65f, Mathf.Sin(a) * 0.85f), new Vector3(0.26f, 0.3f, 0.26f), StoneLight, 0, -a * Mathf.Rad2Deg, 0);
            }
            B(t, new Vector3(0, 0.45f, -0.84f), new Vector3(0.4f, 0.7f, 0.06f), Timber);   // door
            var turret = new GameObject("Turret").transform;
            turret.SetParent(t, false);
            turret.localPosition = new Vector3(0, 1.55f, 0);
            B(turret, new Vector3(0, 0.12f, 0.05f), new Vector3(0.55f, 0.2f, 0.7f), Wood);
            foreach (var sgn in new[] { -1f, 1f })
                Cyl(turret, new Vector3(sgn * 0.3f, 0.1f, 0.25f), 0.13f, 0.06f, Timber, 0, 0, 90); // wheels
            Cyl(turret, new Vector3(0, 0.34f, -0.2f), 0.14f, 0.95f, Iron, 80, 0, 0);       // barrel
            Cyl(turret, new Vector3(0, 0.42f, -0.65f), 0.17f, 0.12f, Iron * 0.8f, 80, 0, 0); // muzzle ring
            Ball(turret, new Vector3(0, 0.33f, 0.3f), Vector3.one * 0.2f, Iron);
            for (int i = 0; i < 3; i++) Ball(t, new Vector3(0.45f + i * 0.14f, 1.6f, 0.45f), Vector3.one * 0.13f, Iron * 0.7f);
            Flame(t, new Vector3(-0.5f, 1.65f, 0.45f), 0.12f);
        }

        /// <summary>Open saw shed: roof on posts, a log on the saw horse, stacks of fresh planks.</summary>
        static void Carpenter(Transform t)
        {
            B(t, new Vector3(0, 0.03f, 0), new Vector3(1.9f, 0.06f, 1.9f), new Color(0.62f, 0.5f, 0.34f)); // sawdust yard
            foreach (var x in new[] { -0.8f, 0.8f })
                foreach (var z in new[] { -0.35f, 0.75f })
                    B(t, new Vector3(x, 0.6f, z), new Vector3(0.1f, 1.2f, 0.1f), Timber);
            Roof(t, new Vector3(0, 1.2f, 0.2f), 1.95f, 1.5f, 0.45f, Thatch);
            // back wall of boards
            for (int i = 0; i < 7; i++) B(t, new Vector3(-0.72f + i * 0.24f, 0.55f, 0.8f), new Vector3(0.22f, 1.1f, 0.04f), i % 2 == 0 ? WoodLight : Wood);
            // saw horse with a log being cut
            B(t, new Vector3(-0.2f, 0.3f, 0.15f), new Vector3(0.06f, 0.6f, 0.5f), Wood, 18, 0, 0);
            B(t, new Vector3(0.3f, 0.3f, 0.15f), new Vector3(0.06f, 0.6f, 0.5f), Wood, 18, 0, 0);
            Cyl(t, new Vector3(0.05f, 0.62f, 0.15f), 0.14f, 1.3f, Wood, 0, 0, 90);
            Cyl(t, new Vector3(0.74f, 0.62f, 0.15f), 0.13f, 0.02f, new Color(0.88f, 0.74f, 0.5f), 0, 0, 90);
            B(t, new Vector3(0.05f, 0.85f, 0.15f), new Vector3(0.02f, 0.4f, 0.5f), Iron);          // big saw blade
            // plank stacks out front
            for (int k = 0; k < 2; k++)
                for (int i = 0; i < 5; i++)
                    B(t, new Vector3(-0.5f + k * 0.95f, 0.07f + i * 0.07f, -0.7f), new Vector3(0.75f, 0.06f, 0.28f), i % 2 == 0 ? new Color(0.86f, 0.7f, 0.46f) : new Color(0.8f, 0.63f, 0.4f));
            Cyl(t, new Vector3(-0.75f, 0.12f, 0.45f), 0.12f, 0.8f, Wood * 0.9f, 90, 0, 0);
        }

        /// <summary>Tunnel into a rock face with a timber frame, rails and an ore cart.</summary>
        static void Mine(Transform t)
        {
            var rock = new Color(0.45f, 0.42f, 0.4f);
            Ball(t, new Vector3(0, 0.35f, 0.45f), new Vector3(2.1f, 1.6f, 1.6f), rock);
            Ball(t, new Vector3(-0.6f, 0.55f, 0.6f), new Vector3(1.0f, 1.3f, 1.0f), rock * 0.9f);
            Ball(t, new Vector3(0.65f, 0.45f, 0.55f), new Vector3(1.0f, 1.1f, 1.0f), rock * 1.08f);
            B(t, new Vector3(0, 0.42f, -0.28f), new Vector3(0.7f, 0.8f, 0.2f), new Color(0.05f, 0.04f, 0.04f));   // dark tunnel
            B(t, new Vector3(-0.38f, 0.45f, -0.36f), new Vector3(0.12f, 0.9f, 0.12f), Timber);
            B(t, new Vector3(0.38f, 0.45f, -0.36f), new Vector3(0.12f, 0.9f, 0.12f), Timber);
            B(t, new Vector3(0, 0.92f, -0.36f), new Vector3(0.95f, 0.14f, 0.16f), Timber);
            // rails
            foreach (var x in new[] { -0.15f, 0.15f }) B(t, new Vector3(x, 0.03f, -0.75f), new Vector3(0.04f, 0.04f, 1.0f), Iron);
            for (int i = 0; i < 5; i++) B(t, new Vector3(0, 0.015f, -0.35f - i * 0.2f), new Vector3(0.5f, 0.03f, 0.07f), Wood);
            // cart full of rust-red ore
            B(t, new Vector3(0, 0.22f, -0.8f), new Vector3(0.42f, 0.22f, 0.5f), Wood);
            foreach (var x in new[] { -0.2f, 0.2f }) foreach (var z in new[] { -0.95f, -0.65f })
                Cyl(t, new Vector3(x, 0.09f, z), 0.07f, 0.04f, Iron, 0, 0, 90);
            var ore = new Color(0.55f, 0.28f, 0.16f);
            for (int i = 0; i < 6; i++) Ball(t, new Vector3(Random.Range(-0.14f, 0.14f), 0.36f, -0.8f + Random.Range(-0.15f, 0.15f)), Vector3.one * Random.Range(0.1f, 0.16f), ore);
            for (int i = 0; i < 5; i++) Ball(t, new Vector3(0.7f + Random.Range(-0.15f, 0.15f), 0.06f, -0.6f + Random.Range(-0.2f, 0.2f)), Vector3.one * Random.Range(0.12f, 0.2f), ore * 0.9f);
            // lantern
            B(t, new Vector3(0.38f, 0.75f, -0.45f), new Vector3(0.1f, 0.12f, 0.1f), Iron);
            var lamp = B(t, new Vector3(0.38f, 0.75f, -0.45f), new Vector3(0.07f, 0.08f, 0.07f), Color.white);
            lamp.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(Glow, 2f);
        }

        /// <summary>Round stone furnace with a tall chimney, glowing mouth and piles of ore and iron bars.</summary>
        static void Smelter(Transform t)
        {
            B(t, new Vector3(0, 0.05f, 0), new Vector3(1.9f, 0.1f, 1.9f), StoneDark);
            Cyl(t, new Vector3(-0.15f, 0.55f, 0.15f), 0.6f, 1.0f, new Color(0.55f, 0.38f, 0.3f));   // brick furnace
            Cone(t, new Vector3(-0.15f, 1.05f, 0.15f), 0.6f, 0.55f, new Color(0.5f, 0.34f, 0.27f), 12);
            B(t, new Vector3(-0.15f, 1.9f, 0.15f), new Vector3(0.3f, 1.4f, 0.3f), StoneDark);        // chimney
            Flame(t, new Vector3(-0.15f, 2.65f, 0.15f), 0.18f);
            var mouth = B(t, new Vector3(-0.15f, 0.35f, -0.44f), new Vector3(0.36f, 0.3f, 0.05f), Color.white);
            mouth.GetComponent<Renderer>().sharedMaterial = Materials.Emissive(Fire, 3f);
            // bellows
            B(t, new Vector3(0.55f, 0.3f, 0.2f), new Vector3(0.35f, 0.18f, 0.5f), new Color(0.35f, 0.22f, 0.14f), 0, 0, 12);
            // ore pile and iron bars
            var ore = new Color(0.55f, 0.28f, 0.16f);
            for (int i = 0; i < 7; i++) Ball(t, new Vector3(0.6f + Random.Range(-0.18f, 0.18f), 0.12f, -0.55f + Random.Range(-0.15f, 0.15f)), Vector3.one * Random.Range(0.12f, 0.2f), ore);
            for (int r = 0; r < 3; r++) for (int i = 0; i < 3 - r; i++)
                B(t, new Vector3(-0.75f + i * 0.17f + r * 0.08f, 0.15f + r * 0.08f, -0.6f), new Vector3(0.14f, 0.07f, 0.4f), new Color(0.62f, 0.64f, 0.68f));
        }

        static void Canal(Transform t)
        {
            // a dug channel: dark wet floor, water surface (on only when connected), banks added per side by Building.SetCanalBanks
            B(t, new Vector3(0, 0.005f, 0), new Vector3(1.0f, 0.01f, 1.0f), new Color(0.3f, 0.23f, 0.15f));
            B(t, new Vector3(0, 0.012f, 0), new Vector3(0.7f, 0.01f, 0.7f), new Color(0.2f, 0.15f, 0.1f));
            var water = B(t, new Vector3(0, 0.05f, 0), new Vector3(1.0f, 0.02f, 1.0f), Color.white);
            water.name = "Water";
            water.GetComponent<Renderer>().sharedMaterial = Materials.Water();
        }

        /// <summary>Raised earth-and-stone banks on the sides of a canal that do not continue into another canal.</summary>
        public static void CanalBanks(Transform t, bool north, bool south, bool east, bool west)
        {
            var old = t.Find("Banks");
            if (old != null) Object.DestroyImmediate(old.gameObject);
            var root = new GameObject("Banks").transform;
            root.SetParent(t, false);
            var earth = new Color(0.4f, 0.31f, 0.2f);
            System.Action<Vector3, bool> bank = (pos, alongX) =>
            {
                B(root, pos, alongX ? new Vector3(1.04f, 0.16f, 0.2f) : new Vector3(0.2f, 0.16f, 1.04f), earth);
                for (int i = 0; i < 3; i++)
                {
                    float o = -0.33f + i * 0.33f + Random.Range(-0.05f, 0.05f);
                    var p = pos + (alongX ? new Vector3(o, 0.09f, 0) : new Vector3(0, 0.09f, o));
                    Ball(root, p, new Vector3(0.16f, 0.09f, 0.14f), i % 2 == 0 ? StoneDark : Stone);
                }
            };
            if (!north) bank(new Vector3(0, 0.08f, 0.45f), true);
            if (!south) bank(new Vector3(0, 0.08f, -0.45f), true);
            if (!east) bank(new Vector3(0.45f, 0.08f, 0), false);
            if (!west) bank(new Vector3(-0.45f, 0.08f, 0), false);
        }

        static void Well(Transform t)
        {
            B(t, new Vector3(0, 0.03f, 0), new Vector3(0.95f, 0.06f, 0.95f), StoneDark);
            Cyl(t, new Vector3(0, 0.25f, 0), 0.36f, 0.42f, Stone);
            Cyl(t, new Vector3(0, 0.47f, 0), 0.38f, 0.05f, StoneLight);
            var w = Cyl(t, new Vector3(0, 0.4f, 0), 0.28f, 0.02f, Color.white);
            w.GetComponent<Renderer>().sharedMaterial = Materials.Water();
            B(t, new Vector3(-0.33f, 0.7f, 0), new Vector3(0.06f, 0.7f, 0.06f), Wood);
            B(t, new Vector3(0.33f, 0.7f, 0), new Vector3(0.06f, 0.7f, 0.06f), Wood);
            Cyl(t, new Vector3(0, 0.92f, 0), 0.05f, 0.72f, WoodLight, 0, 0, 90);
            B(t, new Vector3(0, 0.75f, 0), new Vector3(0.012f, 0.3f, 0.012f), new Color(0.7f, 0.6f, 0.45f));
            Cyl(t, new Vector3(0, 0.56f, 0), 0.07f, 0.1f, Wood);
            var r = MeshFactory.Create(MeshFactory.Gable(), t, new Vector3(0, 1.0f, 0), new Vector3(0.95f, 0.35f, 0.85f), RoofRed);
            r.transform.localRotation = Quaternion.Euler(0, 90, 0);
            Barrel(t, new Vector3(0.38f, 0, -0.38f));
        }

        static void Castle(Transform t)
        {
            // plinth and courtyard
            B(t, new Vector3(0, 0.08f, 0), new Vector3(3.95f, 0.16f, 3.95f), StoneDark);
            B(t, new Vector3(0, 0.17f, 0), new Vector3(3.3f, 0.02f, 3.3f), new Color(0.58f, 0.5f, 0.38f));
            float wallH = 1.5f, half = 1.62f, thick = 0.32f;
            // curtain walls (front split by the gate)
            B(t, new Vector3(0, wallH * 0.5f + 0.16f, half), new Vector3(3.2f, wallH, thick), Stone);
            B(t, new Vector3(-half, wallH * 0.5f + 0.16f, 0), new Vector3(thick, wallH, 3.2f), Stone);
            B(t, new Vector3(half, wallH * 0.5f + 0.16f, 0), new Vector3(thick, wallH, 3.2f), Stone);
            B(t, new Vector3(-1.05f, wallH * 0.5f + 0.16f, -half), new Vector3(1.1f, wallH, thick), Stone);
            B(t, new Vector3(1.05f, wallH * 0.5f + 0.16f, -half), new Vector3(1.1f, wallH, thick), Stone);
            float my = wallH + 0.16f + 0.1f;
            Merlons(t, new Vector3(-half, 0, half), new Vector3(half, 0, half), my, 0.2f, Stone);
            Merlons(t, new Vector3(-half, 0, -half), new Vector3(-half, 0, half), my, 0.2f, Stone);
            Merlons(t, new Vector3(half, 0, -half), new Vector3(half, 0, half), my, 0.2f, Stone);
            Merlons(t, new Vector3(-half, 0, -half), new Vector3(-0.5f, 0, -half), my, 0.2f, Stone);
            Merlons(t, new Vector3(0.5f, 0, -half), new Vector3(half, 0, -half), my, 0.2f, Stone);
            // stone courses on the front wall
            foreach (var x in new[] { -1.05f, 1.05f })
                for (int row = 0; row < 4; row++)
                    B(t, new Vector3(x, 0.4f + row * 0.32f, -half - 0.165f), new Vector3(1.08f, 0.03f, 0.01f), StoneDark);

            // corner towers with slate cone roofs and banners
            foreach (var x in new[] { -half, half })
                foreach (var z in new[] { -half, half })
                {
                    Cyl(t, new Vector3(x, 1.25f, z), 0.48f, 2.5f, StoneLight);
                    Cyl(t, new Vector3(x, 2.52f, z), 0.54f, 0.1f, StoneDark);
                    Cone(t, new Vector3(x, 2.57f, z), 0.58f, 1.2f, Slate, 12);
                    B(t, new Vector3(x, 1.6f, z - 0.47f), new Vector3(0.06f, 0.3f, 0.02f), Iron * 0.6f); // arrow slit
                    B(t, new Vector3(x, 0.9f, z - 0.47f), new Vector3(0.06f, 0.3f, 0.02f), Iron * 0.6f);
                    B(t, new Vector3(x, 3.95f, z), new Vector3(0.025f, 0.4f, 0.025f), Wood);
                    B(t, new Vector3(x + 0.13f, 4.05f, z), new Vector3(0.24f, 0.15f, 0.01f), BannerRed);
                }

            // gatehouse
            foreach (var x in new[] { -0.5f, 0.5f })
            {
                B(t, new Vector3(x, 1.05f, -half), new Vector3(0.42f, 2.1f, 0.5f), StoneLight);
                Merlons(t, new Vector3(x - 0.15f, 0, -half - 0.18f), new Vector3(x + 0.15f, 0, -half - 0.18f), 2.2f, 0.14f, StoneLight);
            }
            B(t, new Vector3(0, 1.75f, -half), new Vector3(0.6f, 0.7f, 0.5f), StoneLight);
            B(t, new Vector3(0, 0.62f, -half - 0.2f), new Vector3(0.56f, 0.95f, 0.1f), new Color(0.1f, 0.08f, 0.07f));
            Cyl(t, new Vector3(0, 1.1f, -half - 0.2f), 0.28f, 0.1f, new Color(0.1f, 0.08f, 0.07f), 90, 0, 0);
            for (int i = 0; i < 4; i++)
                B(t, new Vector3(-0.2f + i * 0.13f, 0.75f, -half - 0.26f), new Vector3(0.025f, 0.9f, 0.025f), Iron); // portcullis
            B(t, new Vector3(0, 0.85f, -half - 0.26f), new Vector3(0.55f, 0.025f, 0.025f), Iron);
            // hanging banners beside the gate
            B(t, new Vector3(-0.5f, 1.35f, -half - 0.27f), new Vector3(0.26f, 0.6f, 0.01f), BannerRed);
            B(t, new Vector3(0.5f, 1.35f, -half - 0.27f), new Vector3(0.26f, 0.6f, 0.01f), BannerRed);
            B(t, new Vector3(-0.5f, 1.4f, -half - 0.28f), new Vector3(0.1f, 0.1f, 0.01f), new Color(0.9f, 0.75f, 0.3f), 0, 0, 45);
            B(t, new Vector3(0.5f, 1.4f, -half - 0.28f), new Vector3(0.1f, 0.1f, 0.01f), new Color(0.9f, 0.75f, 0.3f), 0, 0, 45);

            // keep
            B(t, new Vector3(0, 1.6f, 0.45f), new Vector3(1.5f, 3.0f, 1.4f), Stone);
            B(t, new Vector3(0, 3.12f, 0.45f), new Vector3(1.6f, 0.08f, 1.5f), StoneDark);
            Merlons(t, new Vector3(-0.72f, 0, -0.27f), new Vector3(0.72f, 0, -0.27f), 3.27f, 0.18f, Stone);
            Merlons(t, new Vector3(-0.72f, 0, 1.17f), new Vector3(0.72f, 0, 1.17f), 3.27f, 0.18f, Stone);
            Cone(t, new Vector3(0, 3.16f, 0.45f), 0.62f, 1.3f, Slate, 4);
            foreach (var x in new[] { -0.4f, 0f, 0.4f })
            {
                Window(t, new Vector3(x, 2.5f, -0.26f), 0.16f, 0.3f);
                B(t, new Vector3(x, 1.7f, -0.26f), new Vector3(0.07f, 0.32f, 0.02f), Iron * 0.6f);
            }
            B(t, new Vector3(0, 4.45f, 0.45f), new Vector3(0.035f, 0.9f, 0.035f), Wood);
            B(t, new Vector3(0.24f, 4.7f, 0.45f), new Vector3(0.45f, 0.28f, 0.015f), BannerRed);
            B(t, new Vector3(0.24f, 4.7f, 0.44f), new Vector3(0.12f, 0.12f, 0.015f), new Color(0.9f, 0.75f, 0.3f), 0, 0, 45);
            // courtyard life
            Barrel(t, new Vector3(-1.1f, 0.17f, -0.9f));
            Barrel(t, new Vector3(-0.85f, 0.17f, -1.05f));
            Crate(t, new Vector3(1.0f, 0.17f, -0.95f), 0.25f);
            Crate(t, new Vector3(1.15f, 0.17f, -0.7f), 0.2f);
        }
    }
}
