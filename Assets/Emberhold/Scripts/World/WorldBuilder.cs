using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Builds the natural world around the city: a sculpted terrain with a snowy mountain range
    /// behind the castle (north), a beach and sea (south), forested hills (east/west), a painted
    /// ground texture (grass tones, dirt, paths, sand, rock, snow) and thousands of small details
    /// (grass tufts, flowers, pebbles, bushes) batched into a few draw calls.
    /// </summary>
    public class WorldBuilder : MonoBehaviour
    {
        public Transform TreesRoot { get; private set; }
        Transform borderRoot;
        int borderRadius = -1;

        // Terrain extent (world units)
        const float MinX = -75f, MaxX = 75f, MinZ = -55f, MaxZ = 75f;
        public const float WaterLevel = -0.35f;
        static float seedX = 13.7f, seedZ = 71.3f;

        // ---------------- Height ----------------

        static float Noise(float x, float z, float f) { return Mathf.PerlinNoise(seedX + x * f, seedZ + z * f); }

        static float Smooth(float a, float b, float v) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v)); }

        /// <summary>Ground height at a world position. The city area is perfectly flat (y = 0).</summary>
        public static float HeightAt(float x, float z)
        {
            // Mountain range to the north
            float m = Smooth(GridMap.MountainZ - 1f, GridMap.MountainZ + 8f, z);
            float ridge = 0.55f * Noise(x, z, 0.035f) + 0.3f * Noise(x + 40f, z, 0.09f) + 0.15f * Noise(x, z + 40f, 0.22f);
            float peaks = Mathf.Pow(ridge, 1.6f) * 46f;
            float back = Mathf.Clamp01((z - GridMap.MountainZ - 4f) / 22f);
            float hm = m * (5f + peaks * (0.9f + 1.1f * back) + back * 10f);

            // Forested hills east and west
            float ax = Mathf.Abs(x);
            float f = Smooth(GridMap.ForestX - 1f, GridMap.ForestX + 18f, ax);
            float hf = f * (1.2f + 6f * Noise(x, z, 0.06f));

            // Beach slopes into the sea
            float s = Smooth(GridMap.SeaZ + 1.5f, GridMap.SeaZ - 7f, z);
            float h = Mathf.Max(hm, hf);
            h = Mathf.Lerp(h, -3.2f, s);

            // Tiny unevenness outside the city only
            float outside = Mathf.Max(m, f);
            h += outside * (Noise(x, z, 0.5f) - 0.5f) * 0.5f;
            return h;
        }

        public static float HeightAt(Vector3 p) { return HeightAt(p.x, p.z); }

        // ---------------- Build ----------------

        public void Build(GridMap grid, int seed)
        {
            Random.InitState(seed);
            seedX = 10f + (seed % 97) * 1.37f; seedZ = 50f + (seed % 53) * 2.11f;
            var root = transform;

            BuildTerrain(root);
            BuildSea(root);

            TreesRoot = new GameObject("Trees").transform;
            TreesRoot.SetParent(root, false);
            PlaceForest(grid);
            BuildDetails(root);

            borderRoot = new GameObject("BuildBorder").transform;
            borderRoot.SetParent(root, false);
        }

        float[] heights;
        int hnx, hnz;

        /// <summary>Fast bilinear height lookup from the terrain grid (used while painting).</summary>
        float SampleH(float x, float z)
        {
            float fx = Mathf.Clamp(x - MinX, 0, hnx - 1.001f), fz = Mathf.Clamp(z - MinZ, 0, hnz - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = heights[j * hnx + i], b = heights[j * hnx + i + 1], c = heights[(j + 1) * hnx + i], d = heights[(j + 1) * hnx + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        void BuildTerrain(Transform root)
        {
            const float step = 1f;
            int nx = Mathf.RoundToInt((MaxX - MinX) / step) + 1;
            int nz = Mathf.RoundToInt((MaxZ - MinZ) / step) + 1;
            hnx = nx; hnz = nz;
            heights = new float[nx * nz];
            var verts = new Vector3[nx * nz];
            var uvs = new Vector2[nx * nz];
            for (int j = 0; j < nz; j++)
                for (int i = 0; i < nx; i++)
                {
                    float x = MinX + i * step, z = MinZ + j * step;
                    float hv = HeightAt(x, z);
                    heights[j * nx + i] = hv;
                    verts[j * nx + i] = new Vector3(x, hv, z);
                    uvs[j * nx + i] = new Vector2((float)i / (nx - 1), (float)j / (nz - 1));
                }
            var tris = new int[(nx - 1) * (nz - 1) * 6];
            int k = 0;
            for (int j = 0; j < nz - 1; j++)
                for (int i = 0; i < nx - 1; i++)
                {
                    int a = j * nx + i, b = a + 1, c = a + nx, d = c + 1;
                    tris[k++] = a; tris[k++] = c; tris[k++] = b;
                    tris[k++] = b; tris[k++] = c; tris[k++] = d;
                }
            var mesh = new Mesh { name = "Terrain", indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
            mesh.vertices = verts; mesh.uv = uvs; mesh.triangles = tris;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();

            var go = new GameObject("Terrain");
            go.transform.SetParent(root, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var r = go.AddComponent<MeshRenderer>();
            var mat = Materials.Unique(Color.white);
            var tex = PaintGround();
            if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
            if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
            if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            r.sharedMaterial = mat;
        }

        /// <summary>Paints the ground colors into one texture: grass shades, dirt, paths, sand, rock and snow.</summary>
        Texture2D PaintGround()
        {
            const int ppu = 5; // pixels per world unit
            int w = Mathf.RoundToInt((MaxX - MinX) * ppu), h = Mathf.RoundToInt((MaxZ - MinZ) * ppu);
            var px = new Color32[w * h];

            var grassA = new Color(0.30f, 0.47f, 0.19f);
            var grassB = new Color(0.40f, 0.56f, 0.22f);
            var grassDry = new Color(0.55f, 0.56f, 0.28f);
            var forestFloor = new Color(0.22f, 0.33f, 0.15f);
            var dirt = new Color(0.47f, 0.37f, 0.24f);
            var path = new Color(0.58f, 0.48f, 0.33f);
            var sand = new Color(0.86f, 0.78f, 0.58f);
            var wetSand = new Color(0.62f, 0.55f, 0.42f);
            var rock = new Color(0.47f, 0.45f, 0.43f);
            var rockDark = new Color(0.33f, 0.32f, 0.31f);
            var snow = new Color(0.94f, 0.95f, 0.97f);

            var rng = new System.Random(7);
            for (int y = 0; y < h; y++)
            {
                float z = MinZ + (y + 0.5f) / ppu;
                for (int x = 0; x < w; x++)
                {
                    float wx = MinX + (x + 0.5f) / ppu;
                    float hh = SampleH(wx, z);
                    float slope = Mathf.Abs(SampleH(wx + 0.7f, z) - hh) + Mathf.Abs(SampleH(wx, z + 0.7f) - hh);

                    // grass with large and small variation
                    Color c = Color.Lerp(grassA, grassB, Noise(wx, z, 0.09f));
                    c = Color.Lerp(c, grassDry, Mathf.Clamp01((Noise(wx + 99, z, 0.035f) - 0.55f) * 2.2f));
                    // dirt patches
                    float dp = Noise(wx + 300, z, 0.14f);
                    if (dp > 0.68f) c = Color.Lerp(c, dirt, Mathf.Clamp01((dp - 0.68f) * 7f));
                    // forest floor at the sides
                    float forest = Smooth(GridMap.ForestX - 5f, GridMap.ForestX + 2f, Mathf.Abs(wx));
                    c = Color.Lerp(c, forestFloor, forest * 0.8f);
                    // worn paths: from the castle gate to the beach, and east-west
                    float p1 = Mathf.Abs(wx - (Noise(0, z, 0.08f) - 0.5f) * 3f);
                    float p2 = Mathf.Abs(z + 7f - (Noise(wx, 0, 0.07f) - 0.5f) * 3f);
                    float pathMask = 0f;
                    if (z < -2f && z > GridMap.SeaZ) pathMask = Mathf.Max(pathMask, 1f - Smooth(0.6f, 1.4f, p1));
                    if (Mathf.Abs(wx) < GridMap.ForestX) pathMask = Mathf.Max(pathMask, 1f - Smooth(0.5f, 1.2f, p2));
                    c = Color.Lerp(c, path, pathMask * 0.85f);
                    // beach
                    float beach = Smooth(GridMap.SeaZ + 3f, GridMap.SeaZ + 0.5f, z);
                    c = Color.Lerp(c, sand, beach);
                    if (hh < WaterLevel + 0.15f) c = Color.Lerp(sand, wetSand, Mathf.Clamp01((WaterLevel + 0.15f - hh) * 2f));
                    // mountain: rock on steep or high ground, snow on top
                    float rockMask = Mathf.Clamp01((slope - 0.55f) * 1.5f) + Mathf.Clamp01((hh - 7f) * 0.15f);
                    if (z > GridMap.MountainZ - 2f || Mathf.Abs(wx) > GridMap.ForestX)
                        c = Color.Lerp(c, Color.Lerp(rock, rockDark, Noise(wx, z, 0.3f)), Mathf.Clamp01(rockMask));
                    float snowLine = 17f + (Noise(wx, z, 0.12f) - 0.5f) * 6f;
                    if (hh > snowLine) c = Color.Lerp(c, snow, Mathf.Clamp01((hh - snowLine) * 0.6f) * (1f - Mathf.Clamp01(slope - 1.6f)));

                    // fine speckle so it never looks flat
                    float sp = 0.95f + (float)rng.NextDouble() * 0.1f;
                    c.r *= sp; c.g *= sp; c.b *= sp;
                    px[y * w + x] = c;
                }
            }
            var tex = new Texture2D(w, h, TextureFormat.RGBA32, true);
            tex.wrapMode = TextureWrapMode.Clamp;
            tex.filterMode = FilterMode.Trilinear;
            tex.anisoLevel = 4;
            tex.SetPixels32(px);
            tex.Apply(true, true);
            return tex;
        }

        void BuildSea(Transform root)
        {
            var sea = GameObject.CreatePrimitive(PrimitiveType.Plane);
            Destroy(sea.GetComponent<Collider>());
            sea.name = "Sea";
            sea.transform.SetParent(root, false);
            sea.transform.position = new Vector3(0f, WaterLevel, GridMap.SeaZ - 60f);
            sea.transform.localScale = new Vector3(40f, 1f, 12f);
            var m = Materials.Unique(new Color(0.11f, 0.33f, 0.42f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.92f);
            if (m.HasProperty("_Metallic")) m.SetFloat("_Metallic", 0.1f);
            sea.GetComponent<Renderer>().sharedMaterial = m;
            sea.AddComponent<SeaMotion>();
        }

        void PlaceForest(GridMap grid)
        {
            // Trees near the city are real (they can be chopped and regrow).
            // Far trees are decoration, merged into a few meshes so phones stay smooth.
            var far = new MeshFactory.Batcher();
            for (int x = -62; x < 62; x++)
            {
                for (int z = GridMap.SeaZ + 2; z < 62; z++)
                {
                    int ax = Mathf.Abs(x);
                    float hh = HeightAt(x + 0.5f, z + 0.5f);
                    bool mountain = z >= GridMap.MountainZ;
                    bool deepForest = ax >= GridMap.ForestX;
                    bool forestEdge = ax >= GridMap.ForestX - 4 && ax < GridMap.ForestX;
                    bool nearCity = ax < GridMap.ForestX + 7 && z < GridMap.MountainZ + 3;
                    float chance = 0f;
                    if (mountain) chance = hh < 6f ? 0.14f : hh < 10f ? 0.04f : 0f;   // pines up the lower slopes
                    else if (deepForest) chance = nearCity ? 0.4f : 0.3f;
                    else if (forestEdge) chance = 0.12f;
                    if (chance <= 0f || Random.value >= chance) continue;
                    var cell = new Vector2Int(x, z);
                    if (nearCity) PlaceTree(grid, cell, mountain);
                    else AddFarPine(far, new Vector3(x + Random.value, 0, z + Random.value), Random.Range(0.9f, 1.5f));
                }
            }
            far.Build(TreesRoot, "FarForest", true);

            // groves inside the valley (wood for the first lumberjacks)
            for (int g = 0; g < 8; g++)
            {
                var center = new Vector2(Random.Range(-19f, 19f), Random.Range(-15f, 17f));
                if (center.magnitude < 9f) continue;
                int n = Random.Range(4, 9);
                for (int i = 0; i < n; i++)
                {
                    var c = new Vector2Int(Mathf.RoundToInt(center.x + Random.Range(-2.5f, 2.5f)), Mathf.RoundToInt(center.y + Random.Range(-2.5f, 2.5f)));
                    if (grid.At(c) == null) PlaceTree(grid, c, false);
                }
            }
        }

        static readonly Color[] FarGreens = { new Color(0.12f, 0.27f, 0.16f), new Color(0.15f, 0.31f, 0.18f), new Color(0.1f, 0.23f, 0.14f) };

        static void AddFarPine(MeshFactory.Batcher b, Vector3 p, float scale)
        {
            p.y = HeightAt(p) - 0.1f;
            var g = FarGreens[Random.Range(0, FarGreens.Length)];
            float hgt = Random.Range(2.4f, 3.2f) * scale;
            var cone = MeshFactory.Cone(7);
            for (int i = 0; i < 3; i++)
            {
                float f = i / 2f;
                float w = Mathf.Lerp(1.6f, 0.65f, f) * scale;
                b.Add(cone, Matrix4x4.TRS(p + Vector3.up * (0.4f * scale + f * hgt * 0.55f), Quaternion.Euler(0, Random.Range(0, 360f), 0), new Vector3(w, hgt * 0.45f, w)), g);
            }
        }

        void PlaceTree(GridMap grid, Vector2Int cell, bool pine)
        {
            if (grid.At(cell) != null) return;
            var t = TreeNode.Create(TreesRoot, cell, Random.Range(0.8f, 1.35f), pine ? 1 : -1);
            grid.Occupy(new[] { cell }, t);
        }

        /// <summary>Grass tufts, flowers, pebbles, bushes and mountain boulders, batched for speed.</summary>
        void BuildDetails(Transform root)
        {
            var detail = new GameObject("Details").transform;
            detail.SetParent(root, false);
            var batch = new MeshFactory.Batcher();
            var cube = PrimitiveType.Cube;
            var sphere = PrimitiveType.Sphere;

            var grassCols = new[] { new Color(0.28f, 0.48f, 0.16f), new Color(0.36f, 0.55f, 0.2f), new Color(0.45f, 0.58f, 0.22f) };
            var flowerCols = new[] { new Color(0.95f, 0.85f, 0.25f), new Color(0.92f, 0.92f, 0.95f), new Color(0.75f, 0.35f, 0.75f), new Color(0.9f, 0.35f, 0.25f), new Color(0.45f, 0.55f, 0.95f) };
            var pebble = new Color(0.55f, 0.53f, 0.5f);

            for (int i = 0; i < 3000; i++)
            {
                float x = Random.Range(-GridMap.ForestX - 6f, GridMap.ForestX + 6f);
                float z = Random.Range(GridMap.SeaZ + 2.5f, GridMap.MountainZ + 3f);
                if (Mathf.Abs(x) < 3.5f && Mathf.Abs(z) < 3.5f) continue; // castle yard
                float y = HeightAt(x, z);
                // grass tuft: 3 thin blades
                var col = grassCols[Random.Range(0, grassCols.Length)];
                float hgt = Random.Range(0.18f, 0.38f);
                for (int b = 0; b < 3; b++)
                {
                    var rot = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 180f), Random.Range(-18f, 18f));
                    batch.Add(cube, new Vector3(x + Random.Range(-0.08f, 0.08f), y + hgt * 0.5f, z + Random.Range(-0.08f, 0.08f)), rot, new Vector3(0.03f, hgt, 0.09f), col);
                }
            }
            for (int i = 0; i < 900; i++)
            {
                // flowers in meadow clusters
                float cx = Random.Range(-GridMap.ForestX, GridMap.ForestX), cz = Random.Range(GridMap.SeaZ + 4f, GridMap.MountainZ);
                if (Noise(cx, cz, 0.12f) < 0.55f) continue;
                if (Mathf.Abs(cx) < 4f && Mathf.Abs(cz) < 4f) continue;
                var col = flowerCols[Random.Range(0, flowerCols.Length)];
                for (int f = 0; f < 4; f++)
                {
                    float x = cx + Random.Range(-0.6f, 0.6f), z = cz + Random.Range(-0.6f, 0.6f), y = HeightAt(x, z);
                    float hgt = Random.Range(0.15f, 0.3f);
                    batch.Add(cube, new Vector3(x, y + hgt * 0.5f, z), Quaternion.identity, new Vector3(0.02f, hgt, 0.02f), grassCols[0]);
                    batch.Add(sphere, new Vector3(x, y + hgt, z), Quaternion.identity, Vector3.one * Random.Range(0.07f, 0.11f), col);
                }
            }
            for (int i = 0; i < 700; i++)
            {
                float x = Random.Range(-GridMap.ForestX - 8f, GridMap.ForestX + 8f), z = Random.Range(GridMap.SeaZ + 1f, GridMap.MountainZ + 4f);
                float y = HeightAt(x, z);
                float s = Random.Range(0.06f, 0.22f);
                batch.Add(sphere, new Vector3(x, y + s * 0.2f, z), Quaternion.Euler(0, Random.Range(0, 360f), 0), new Vector3(s * 1.4f, s * 0.7f, s), pebble * Random.Range(0.85f, 1.15f));
            }
            // bushes along the forest edge
            var bushCols = new[] { new Color(0.2f, 0.38f, 0.14f), new Color(0.26f, 0.44f, 0.17f) };
            for (int i = 0; i < 260; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float x = side * Random.Range(GridMap.ForestX - 6f, GridMap.ForestX + 4f), z = Random.Range(GridMap.SeaZ + 3f, GridMap.MountainZ + 2f);
                float y = HeightAt(x, z);
                var col = bushCols[Random.Range(0, 2)];
                for (int b = 0; b < 3; b++)
                {
                    float s = Random.Range(0.45f, 0.8f);
                    batch.Add(sphere, new Vector3(x + Random.Range(-0.35f, 0.35f), y + s * 0.35f, z + Random.Range(-0.35f, 0.35f)), Quaternion.identity, new Vector3(s, s * 0.75f, s), col);
                }
            }
            // boulders on the mountain foot and in the hills
            var rockCols = new[] { new Color(0.5f, 0.48f, 0.46f), new Color(0.42f, 0.41f, 0.4f) };
            for (int i = 0; i < 160; i++)
            {
                float x = Random.Range(-60f, 60f), z = Random.Range(GridMap.MountainZ - 1f, GridMap.MountainZ + 30f);
                float y = HeightAt(x, z);
                float s = Random.Range(0.6f, 2.6f);
                batch.Add(sphere, new Vector3(x, y + s * 0.15f, z), Quaternion.Euler(Random.Range(-20, 20), Random.Range(0, 360), Random.Range(-20, 20)), new Vector3(s * 1.3f, s * 0.8f, s), rockCols[Random.Range(0, 2)]);
            }
            batch.Build(detail, "Detail", false);
        }

        /// <summary>Low stone markers around the area the castle level allows building in.</summary>
        public void ShowBuildBorder(int radius)
        {
            if (radius == borderRadius) return;
            borderRadius = radius;
            for (int i = borderRoot.childCount - 1; i >= 0; i--) Destroy(borderRoot.GetChild(i).gameObject);
            var c = new Color(0.72f, 0.68f, 0.6f);
            for (int i = -radius; i <= radius; i += 2)
            {
                Marker(new Vector3(i, 0f, -radius), c);
                Marker(new Vector3(i, 0f, radius), c);
                Marker(new Vector3(-radius, 0f, i), c);
                Marker(new Vector3(radius, 0f, i), c);
            }
        }

        void Marker(Vector3 p, Color c)
        {
            if (!GridMap.IsTerrainBuildable(GridMap.CellOf(p)) && !GridMap.IsTerrainBuildable(GridMap.CellOf(p) - Vector2Int.one)) return;
            Util.Prim(PrimitiveType.Cube, borderRoot, p + Vector3.up * 0.06f, new Vector3(0.22f, 0.12f, 0.22f), c);
        }
    }

    /// <summary>Gentle swell so the sea is not a flat sheet.</summary>
    public class SeaMotion : MonoBehaviour
    {
        Vector3 basePos;
        void Start() { basePos = transform.position; }
        void Update() { transform.position = basePos + Vector3.up * Mathf.Sin(Time.time * 0.6f) * 0.06f; }
    }
}
