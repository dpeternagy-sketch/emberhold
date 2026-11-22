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
                default: B(t, new Vector3(0, def.height * 0.5f, 0), new Vector3(def.width * 0.9f, def.height, def.depth * 0.9f), def.color); break;
            }
            return root;
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
