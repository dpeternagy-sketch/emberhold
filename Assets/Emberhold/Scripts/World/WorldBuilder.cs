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

        /// <summary>The painted north-south path's centre x at a given z, and the east-west path's centre z at a given x
        /// (the wall's gates sit where these paths meet the wall).</summary>
        public static float PathX(float z) { return (Noise(0, z, 0.08f) - 0.5f) * 3f; }
        public static float PathZ(float x) { return 4f + (Noise(x, 0, 0.07f) - 0.5f) * 3f; }
        static float Smooth(float a, float b, float v) { return Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(a, b, v)); }

        /// <summary>Ground height at a world position. The city area is perfectly flat (y = 0).</summary>
        public static float HeightAt(float x, float z)
        {
            float mz = GridMap.MountainZ;
            // 1) Rolling green foothills right behind the city
            float m = Smooth(mz - 1f, mz + 7f, z);
            float hFoot = m * (1.6f + 2.4f * Noise(x, z, 0.08f));
            // 2) The main range. Its front edge wanders in and out, so it never reads as one straight wall,
            //    and it rises over ~30 units: forest, then rock, then snow.
            float front = mz + 7f + 8f * Noise(x, 3f, 0.05f);
            float rise = Smooth(front, front + 30f, z);
            float ridge = 0.55f * Noise(x, z, 0.035f) + 0.3f * Noise(x + 40f, z, 0.09f) + 0.15f * Noise(x, z + 40f, 0.22f);
            float peaks = Mathf.Pow(ridge, 1.6f) * 34f;
            float back = Mathf.Clamp01((z - mz - 30f) / 20f);
            float hMain = rise * (6f + peaks + back * 12f);
            float rn = 1f - Mathf.Abs(2f * Noise(x + 200f, z, 0.11f) - 1f);
            hMain += rise * rise * rn * rn * 3f;                // rocky spurs up high
            float band = 3.2f;                                   // light terraces: ledges between rock faces
            float tq = hMain / band, fl = Mathf.Floor(tq), fr = tq - fl;
            float terr = (fl + Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((fr - 0.55f) / 0.45f))) * band;
            hMain = Mathf.Lerp(hMain, terr, 0.35f * rise);
            // Peter's painted mountain (the backdrop) is the only big mountain: the 3D range stays low green hills,
            // so no grey 3D rock pokes out around or through the painting (QC run 22: "two backdrops")
            hMain *= 0.18f;
            float hm = hFoot + hMain;
            // 3) A rock ledge the river falls over (the waterfall), blending into the hills on both sides
            float nearRiver = 1f - Smooth(2.5f, 8f, Mathf.Abs(x - River.CenterX(z)));
            float step = nearRiver * Smooth(River.FallZ - 0.3f, River.FallZ + 0.3f, z);
            // (no 3D rock ledge any more: the waterfall is the one in Peter's painting, QC run 22)
            hm = Mathf.Max(hm, step * 0f);

            // Forested hills east and west
            float ax = Mathf.Abs(x);
            float f = Smooth(GridMap.ForestX - 1f, GridMap.ForestX + 18f, ax);
            float hf = f * (1.2f + 6f * Noise(x, z, 0.06f));

            // Beach slopes into the sea
            float s = Smooth(GridMap.SeaZ + 1.5f, GridMap.SeaZ - 7f, z);
            float h = Mathf.Max(hm, hf);
            h = Mathf.Lerp(h, -3.2f, s);

            // River bed
            float rv = River.Mask(x, z);
            if (rv > 0f) h -= rv * (0.7f + 0.2f * m);   // only a little deeper in the hills: steep cut banks shaded into a dark band (QC run 25)

            // Tiny unevenness outside the city only
            float outside = Mathf.Max(m, f);
            h += outside * (Noise(x, z, 0.5f) - 0.5f) * 0.5f;
            // behind the painted mountain the land falls away out of sight, so above the painted peaks
            // there is only sky (the camera clears to the fog's sky blue)
            h = Mathf.Lerp(h, -40f, Smooth(River.StartZ + 9f, River.StartZ + 18f, z));   // QC run 30: falling away sooner left a sky-blue strip under the painting's edge
            return h;
        }

        public static float HeightAt(Vector3 p) { return HeightAt(p.x, p.z); }

        public static Vector3 NormalAt(float x, float z)
        {
            const float e = 0.4f;
            float dx = HeightAt(x + e, z) - HeightAt(x - e, z);
            float dz = HeightAt(x, z + e) - HeightAt(x, z - e);
            return new Vector3(-dx, 2f * e, -dz).normalized;
        }

        /// <summary>0 = flat, ~1 = 45 degrees.</summary>
        public static float SlopeAt(float x, float z) { var n = NormalAt(x, z); return Mathf.Sqrt(1f - n.y * n.y) / Mathf.Max(0.05f, n.y); }

        /// <summary>Lowest ground under a round base, so things never hang over a slope.</summary>
        public static float GroundUnder(float x, float z, float radius)
        {
            float h = HeightAt(x, z);
            for (int k = 0; k < 4; k++)
            {
                float a = k * Mathf.PI * 0.5f;
                h = Mathf.Min(h, HeightAt(x + Mathf.Cos(a) * radius, z + Mathf.Sin(a) * radius));
            }
            return h;
        }

        // ---------------- Build ----------------

        public void Build(GridMap grid, int seed)
        {
            Random.InitState(seed);
            seedX = 10f + (seed % 97) * 1.37f; seedZ = 50f + (seed % 53) * 2.11f;
            var root = transform;

            BuildTerrain(root);
            BuildSea(root);
            River.MarkCells(grid);
            River.BuildVisual(root);
            // Peter's cliffs, falls and pool behind the HQ (drawn at our camera angle); the pool sits on the river spring.
            // mountain_1 / mountain_2 are spares.
            Backdrop.Build(root, "mountain_iso", 44f, new Vector2(0.524f, 0.80f));

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
        const float TerrainStep = 0.5f;

        float SampleH(float x, float z)
        {
            float fx = Mathf.Clamp((x - MinX) / TerrainStep, 0, hnx - 1.001f), fz = Mathf.Clamp((z - MinZ) / TerrainStep, 0, hnz - 1.001f);
            int i = (int)fx, j = (int)fz;
            float tx = fx - i, tz = fz - j;
            float a = heights[j * hnx + i], b = heights[j * hnx + i + 1], c = heights[(j + 1) * hnx + i], d = heights[(j + 1) * hnx + i + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, tx), Mathf.Lerp(c, d, tx), tz);
        }

        void BuildTerrain(Transform root)
        {
            const float step = TerrainStep;
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
            Texture2D control;
            var tex = PaintGround(out control);
            var splat = Resources.Load<Shader>("Shaders/GroundSplat");
            var grass = Resources.Load<Texture2D>("Textures/Ground/grass");
            Material mat;
            if (splat != null && splat.isSupported && grass != null)
            {
                // Peter's tiling ground textures, blended by the control map
                mat = new Material(splat);
                mat.SetTexture("_Painted", tex);
                mat.SetTexture("_Control", control);
                mat.SetTexture("_Grass", grass);
                mat.SetTexture("_Dirt", Resources.Load<Texture2D>("Textures/Ground/dirt") ?? grass);
                mat.SetTexture("_Stone", Resources.Load<Texture2D>("Textures/Ground/stone") ?? grass);
                mat.SetFloat("_Tile", 6f);
            }
            else
            {
                mat = Materials.Unique(Color.white);
                if (mat.HasProperty("_BaseMap")) mat.SetTexture("_BaseMap", tex);
                if (mat.HasProperty("_MainTex")) mat.SetTexture("_MainTex", tex);
                if (mat.HasProperty("_Smoothness")) mat.SetFloat("_Smoothness", 0.05f);
            }
            r.sharedMaterial = mat;
        }

        /// <summary>Paints the ground colors into one texture: grass shades, dirt, paths, sand, rock and snow.</summary>
        /// Also fills a control map for the GroundSplat shader: R = dirt, G = stone path, B = keep this painted colour, A = brightness.
        Texture2D PaintGround(out Texture2D control)
        {
            const int ppu = 6; // pixels per world unit
            int w = Mathf.RoundToInt((MaxX - MinX) * ppu), h = Mathf.RoundToInt((MaxZ - MinZ) * ppu);
            var px = new Color32[w * h];
            var ctl = new Color32[w * h];

            var grassA = new Color(0.33f, 0.58f, 0.25f);   // palette grass shade #5A9E45
            var grassB = new Color(0.45f, 0.71f, 0.32f);   // palette grass #7BC35A
            var grassDry = new Color(0.6f, 0.68f, 0.34f);
            var forestFloor = new Color(0.25f, 0.45f, 0.2f);
            var dirt = new Color(0.66f, 0.52f, 0.34f);
            var path = new Color(0.76f, 0.62f, 0.41f);       // palette dirt #C9A06A
            var sand = new Color(0.86f, 0.78f, 0.58f);
            var wetSand = new Color(0.62f, 0.55f, 0.42f);
            var rock = new Color(0.55f, 0.56f, 0.6f);        // palette rock #8C8F99
            var rockDark = new Color(0.42f, 0.43f, 0.47f);
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
                    // no dirt patch round the river spring under the painted pool (QC runs 27-29: a rust-brown triangle there)
                    float spring = Smooth(River.StartZ - 9f, River.StartZ - 6f, z) * (1f - Smooth(River.HalfWidth + 6f, River.HalfWidth + 9f, Mathf.Abs(wx - River.CenterX(z))));
                    float cDirt = (dp > 0.6f ? Mathf.Clamp01((dp - 0.6f) * 6f) : 0f) * (1f - spring);
                    if (dp > 0.68f) c = Color.Lerp(c, dirt, Mathf.Clamp01((dp - 0.68f) * 7f) * (1f - spring));
                    // forest floor at the sides
                    float forest = Smooth(GridMap.ForestX - 5f, GridMap.ForestX + 2f, Mathf.Abs(wx));
                    c = Color.Lerp(c, forestFloor, forest * 0.8f);
                    float cBright = Noise(wx, z, 0.09f) * 0.6f + (1f - Noise(wx + 99, z, 0.035f)) * 0.4f - forest * 0.45f;
                    // worn paths: from the castle gate to the beach, and east-west
                    float p1 = Mathf.Abs(wx - PathX(z));
                    float p2 = Mathf.Abs(z - PathZ(wx));
                    float pathMask = 0f;
                    if (z < GameManager.CastleCenter.z - 2f && z > GridMap.SeaZ) pathMask = Mathf.Max(pathMask, 1f - Smooth(0.6f, 1.4f, p1));
                    if (Mathf.Abs(wx) < GridMap.ForestX) pathMask = Mathf.Max(pathMask, 1f - Smooth(0.5f, 1.2f, p2));
                    c = Color.Lerp(c, path, pathMask * 0.85f);
                    float cStone = pathMask;
                    float cKeep = 0f;
                    // river bed and muddy banks
                    float rvm = River.Mask(wx, z);
                    // a soft, light muddy edge that fades into the grass (QC run 26: a dark brown smear on the bank)
                    // no mud above the spot where the water starts (QC run 27: a brown triangle between the painted pool and the river)
                    float mudK = Mathf.Clamp01((River.StartZ - 0.5f - z) / 1.5f);
                    // (no mud tint: the painted bank colour read as a blurry olive halo, QC runs 30-32)
                    cKeep = Mathf.Max(cKeep, Mathf.Clamp01((rvm - 0.55f) * 4f));   // painted colour only right at the water, so the bank edge is a crisp thin line
                    // the grass under the mountain painting's lower edge matches its darker painted grass (QC runs 30-31)
                    // ground behind the painting's card (seen only in the gaps under its lower edge) takes the
                    // painting's own darker grass tone, so its edge does not sit on much lighter grass (QC runs 30-33).
                    // The card faces the camera (yaw 45), so "behind" is measured across x + z, never z alone.
                    float bz = River.StartZ - 1f, bx = River.CenterX(bz);
                    float back = ((wx - bx) + (z - bz)) * 0.70710678f + 2f;   // distance behind the card (Backdrop: foot - 2 x forward)
                    float under = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((back + 0.5f) / 2f));
                    c = Color.Lerp(c, new Color(0.3f, 0.46f, 0.08f), under);
                    cKeep = Mathf.Max(cKeep, under);
                    // beach
                    float beach = Smooth(GridMap.SeaZ + 3f, GridMap.SeaZ + 0.5f, z);
                    c = Color.Lerp(c, sand, beach);
                    cKeep = Mathf.Max(cKeep, beach);
                    // sand where the ground dips to the water, blended over a band instead of a hard height cut
                    // (QC run 26: a stair-step sand edge along the river)
                    float low = Smooth(WaterLevel + 0.15f, WaterLevel + 0.05f, hh) * mudK;   // no sand patch at the spring (QC run 28)
                    cKeep = Mathf.Max(cKeep, low);
                    if (low > 0f) c = Color.Lerp(c, Color.Lerp(sand, wetSand, Mathf.Clamp01((WaterLevel + 0.15f - hh) * 2f)), low);
                    // mountain: rock on steep or high ground, snow on top
                    float rockMask = Mathf.Clamp01((slope - 0.45f) * 2.2f) + Mathf.Clamp01((hh - 13f) * 0.12f);
                    // the river's own cut banks stay green and muddy, never a blurred grey rock band (QC runs 22-24)
                    rockMask *= Smooth(River.HalfWidth + 1f, River.HalfWidth + 4f, Mathf.Abs(wx - River.CenterX(z)));
                    if (z > GridMap.MountainZ - 2f || Mathf.Abs(wx) > GridMap.ForestX)
                    {
                        // ledges between the cliffs keep tough alpine grass and scree
                        if (z > GridMap.MountainZ - 1f) c = Color.Lerp(c, Color.Lerp(new Color(0.33f, 0.4f, 0.2f), new Color(0.5f, 0.47f, 0.4f), Noise(wx, z, 0.4f)), Mathf.Clamp01((hh - 7f) * 0.2f));
                        // rock faces: warm and cool stone, horizontal strata, dark cracks
                        var rc = Color.Lerp(Color.Lerp(rock, new Color(0.7f, 0.72f, 0.76f), Noise(wx + 7f, z, 0.08f)), rockDark, Noise(wx, z, 0.3f) * 0.8f);
                        float strata = 0.82f + 0.18f * Mathf.Sin(hh * 5.5f + Noise(wx, z, 0.2f) * 4f);
                        float crack = Noise(wx * 3f, z * 0.6f, 0.9f) > 0.72f ? 0.6f : 1f;
                        rc *= strata * crack; rc.a = 1f;
                        c = Color.Lerp(c, rc, Mathf.Clamp01(rockMask));
                        cKeep = Mathf.Max(cKeep, Mathf.Clamp01(rockMask) * 1.2f, z > GridMap.MountainZ - 1f ? Mathf.Clamp01((hh - 7f) * 0.2f) : 0f);
                    }
                    float snowLine = 17f + (Noise(wx, z, 0.12f) - 0.5f) * 6f;
                    if (hh > snowLine) c = Color.Lerp(c, snow, Mathf.Clamp01((hh - snowLine) * 0.6f) * (1f - Mathf.Clamp01(slope - 1.6f)));
                    if (hh > snowLine) cKeep = 1f;
                    ctl[y * w + x] = new Color(cDirt, cStone, Mathf.Clamp01(cKeep), Mathf.Clamp01(cBright));

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
            control = new Texture2D(w, h, TextureFormat.RGBA32, true, true);
            control.wrapMode = TextureWrapMode.Clamp;
            control.filterMode = FilterMode.Bilinear;
            control.SetPixels32(ctl);
            control.Apply(true, true);
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
            var natureFar = new NatureLib.Batcher();
            bool sprites = NatureSprites.Available;            // Peter's painted trees replace the 3D ones
            var spriteFar = new NatureSprites.Batch();
            for (int x = -62; x < 62; x++)
            {
                for (int z = GridMap.SeaZ + 2; z < (int)River.StartZ + 3; z++)   // nothing on the land that falls away behind the mountain
                {
                    int ax = Mathf.Abs(x);
                    float hh = HeightAt(x + 0.5f, z + 0.5f);
                    bool mountain = z >= GridMap.MountainZ;
                    bool deepForest = ax >= GridMap.ForestX;
                    bool forestEdge = ax >= GridMap.ForestX - 4 && ax < GridMap.ForestX;
                    bool nearCity = ax < GridMap.ForestX + 7 && z < GridMap.MountainZ + 3;
                    float chance = 0f;
                    float slope = mountain ? SlopeAt(x + 0.5f, z + 0.5f) : 0f;
                    if (mountain) chance = slope > 0.9f ? 0f : hh < 6f ? 0.16f : hh < 11f ? 0.06f : hh < 14f ? 0.015f : 0f;   // forested foothills, thinning pines higher up, none on cliffs
                    else if (deepForest) chance = nearCity ? 0.4f : 0.3f;
                    else if (forestEdge) chance = 0.12f;
                    if (chance <= 0f || Random.value >= chance) continue;
                    var cell = new Vector2Int(x, z);
                    if (River.Mask(x + 0.5f, z + 0.5f) > 0.1f) continue;
                    if (nearCity) PlaceTree(grid, cell, mountain);
                    else if (sprites)
                    {
                        if (ax >= 56 || z >= GridMap.MountainZ + 26) continue;   // out of view
                        var fp = new Vector3(x + Random.value, 0, z + Random.value);
                        fp.y = HeightAt(fp.x, fp.z);
                        bool snowy = mountain && hh > 13f;
                        var it = snowy ? NatureSprites.Pick("pine_snow") : mountain || Random.value < 0.45f ? NatureSprites.Pick("pine_a", "pine_b") : NatureSprites.Pick("tree_green", "tree_green", "tree_green", "tree_orange", "tree_pink");
                        float sc = Random.Range(0.85f, 1.3f) * (mountain ? Mathf.Lerp(1f, 0.65f, hh / 16f) : 1f);
                        spriteFar.Add(it, fp, 3.4f * sc, 0.06f, Random.Range(0.88f, 1f));
                    }
                    else if (NatureLib.Available && ax < 48 && z < GridMap.MountainZ + 16)
                    {
                        // visible far forest: real (lighter) Quaternius trees, a bit sparser but bigger
                        if (Random.value < 0.55f)
                        {
                            float sc = Random.Range(0.95f, 1.35f) * (mountain ? Mathf.Lerp(1f, 0.6f, hh / 14f) : 1f);
                            var fp = new Vector3(x + Random.value, 0, z + Random.value);
                            fp.y = GroundUnder(fp.x, fp.z, 0.4f * sc) - 0.1f;
                            bool fpine = mountain || Random.value < 0.6f;
                            natureFar.Add((fpine ? "Pine_" : "CommonTree_") + Random.Range(1, 6) + "_LOD", fp, Quaternion.Euler(0, Random.Range(0, 360f), 0), (fpine ? 3.6f : 3.0f) * sc);
                        }
                    }
                    else AddFarPine(far, new Vector3(x + Random.value, 0, z + Random.value), Random.Range(0.9f, 1.5f) * (mountain ? Mathf.Lerp(1f, 0.55f, hh / 14f) : 1f));
                }
            }
            far.Build(TreesRoot, "FarForest", true);
            spriteFar.Build(TreesRoot, "FarForestPainted");
            natureFar.Build(TreesRoot, "FarForestQ", true);

            // groves inside the valley (wood for the first lumberjacks)
            for (int g = 0; g < 8; g++)
            {
                var center = new Vector2(Random.Range(-19f, 19f), Random.Range(-15f, 17f));
                if (Vector2.Distance(center, new Vector2(GameManager.CastleCenter.x, GameManager.CastleCenter.z)) < 7f) continue;
                if (Mathf.Abs(center.x) < 9f && center.y > 2f && center.y < 14f) continue; // starting farmland stays open
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
            p.y = GroundUnder(p.x, p.z, 0.4f * scale) - 0.1f;
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

            if (NatureSprites.Available) { PaintedDetails(detail); return; }
            var nb = new NatureLib.Batcher();
            bool q = NatureLib.Available;
            for (int i = 0; i < 600; i++)
            {
                float x = Random.Range(-GridMap.ForestX - 6f, GridMap.ForestX + 6f);
                float z = Random.Range(GridMap.SeaZ + 2.5f, GridMap.MountainZ + 3f);
                if (Mathf.Abs(x - GameManager.CastleCenter.x) < 3.5f && Mathf.Abs(z - GameManager.CastleCenter.z) < 3.5f) continue; // castle yard
                if (River.Mask(x, z) > 0.2f) continue;
                if (Noise(x + 50f, z, 0.08f) < 0.5f) continue;   // only in natural clumps, never an even carpet
                float y = HeightAt(x, z);
                if (q) { nb.Add(Random.value < 0.6f ? "Grass_Common_Short" : "Grass_Wispy_Short", new Vector3(x, y - 0.02f, z), Quaternion.Euler(0, Random.Range(0, 360f), 0), Random.Range(0.22f, 0.4f)); continue; }
                // grass tuft: 3 thin blades
                var col = grassCols[Random.Range(0, grassCols.Length)];
                float hgt = Random.Range(0.18f, 0.38f);
                for (int b = 0; b < 3; b++)
                {
                    var rot = Quaternion.Euler(Random.Range(-18f, 18f), Random.Range(0f, 180f), Random.Range(-18f, 18f));
                    batch.Add(cube, new Vector3(x + Random.Range(-0.08f, 0.08f), y + hgt * 0.5f, z + Random.Range(-0.08f, 0.08f)), rot, new Vector3(0.03f, hgt, 0.09f), col);
                }
            }
            for (int i = 0; i < 45; i++)
            {
                // flowers in meadow clusters
                float cx = Random.Range(-GridMap.ForestX, GridMap.ForestX), cz = Random.Range(GridMap.SeaZ + 4f, GridMap.MountainZ);
                if (Noise(cx, cz, 0.12f) < 0.6f) continue;
                if (Mathf.Abs(cx - GameManager.CastleCenter.x) < 4f && Mathf.Abs(cz - GameManager.CastleCenter.z) < 4f) continue;
                if (q)
                {
                    for (int f = 0; f < 3; f++)
                    {
                        float fx = cx + Random.Range(-0.8f, 0.8f), fz = cz + Random.Range(-0.8f, 0.8f);
                        nb.Add(f == 0 ? "Bush_Common_Flowers" : "Clover_" + Random.Range(1, 3), new Vector3(fx, HeightAt(fx, fz) - 0.03f, fz), Quaternion.Euler(0, Random.Range(0, 360f), 0), f == 0 ? Random.Range(0.45f, 0.65f) : Random.Range(0.12f, 0.2f));
                    }
                    continue;
                }
                var col = flowerCols[Random.Range(0, flowerCols.Length)];
                for (int f = 0; f < 3; f++)
                {
                    float x = cx + Random.Range(-0.6f, 0.6f), z = cz + Random.Range(-0.6f, 0.6f), y = HeightAt(x, z);
                    float hgt = Random.Range(0.15f, 0.3f);
                    batch.Add(cube, new Vector3(x, y + hgt * 0.5f, z), Quaternion.identity, new Vector3(0.02f, hgt, 0.02f), grassCols[0]);
                    batch.Add(sphere, new Vector3(x, y + hgt, z), Quaternion.identity, Vector3.one * Random.Range(0.07f, 0.11f), col);
                }
            }
            for (int i = 0; i < 70; i++)
            {
                float x = Random.Range(-GridMap.ForestX - 8f, GridMap.ForestX + 8f), z = Random.Range(GridMap.SeaZ + 1f, GridMap.MountainZ + 4f);
                float y = HeightAt(x, z);
                float s = Random.Range(0.06f, 0.22f);
                if (q) { nb.Add("Pebble_Round_" + Random.Range(1, 6), new Vector3(x, y - 0.02f, z), Quaternion.Euler(0, Random.Range(0, 360f), 0), s * 1.2f); continue; }
                batch.Add(sphere, new Vector3(x, y + s * 0.2f, z), Quaternion.Euler(0, Random.Range(0, 360f), 0), new Vector3(s * 1.4f, s * 0.7f, s), pebble * Random.Range(0.85f, 1.15f));
            }
            // bushes along the forest edge
            var bushCols = new[] { new Color(0.2f, 0.38f, 0.14f), new Color(0.26f, 0.44f, 0.17f) };
            for (int i = 0; i < 260; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float x = side * Random.Range(GridMap.ForestX - 6f, GridMap.ForestX + 4f), z = Random.Range(GridMap.SeaZ + 3f, GridMap.MountainZ + 2f);
                float y = HeightAt(x, z);
                if (q)
                {
                    if (i % 2 == 0) nb.Add(Random.value < 0.8f ? "Bush_Common" : "Bush_Common_Flowers", new Vector3(x, y - 0.05f, z), Quaternion.Euler(0, Random.Range(0, 360f), 0), Random.Range(0.6f, 1.0f));
                    continue;
                }
                var col = bushCols[Random.Range(0, 2)];
                for (int b = 0; b < 3; b++)
                {
                    float s = Random.Range(0.45f, 0.8f);
                    batch.Add(sphere, new Vector3(x + Random.Range(-0.35f, 0.35f), y + s * 0.35f, z + Random.Range(-0.35f, 0.35f)), Quaternion.identity, new Vector3(s, s * 0.75f, s), col);
                }
            }
            // Boulders: always sitting IN the ground (lowest point under them, partly buried, tilted with the slope).
            var rockCols = new[] { new Color(0.5f, 0.48f, 0.46f), new Color(0.42f, 0.41f, 0.4f), new Color(0.46f, 0.43f, 0.39f) };
            var rockMeshes = new[] { MeshFactory.Rock(1), MeshFactory.Rock(2), MeshFactory.Rock(3) };
            for (int i = 0; i < 220; i++)
            {
                float x = Random.Range(-60f, 60f), z = Random.Range(GridMap.MountainZ - 1f, GridMap.MountainZ + 30f);
                if (River.Mask(x, z) > 0.2f) continue;
                float s = Random.Range(0.5f, 2.2f);
                float lowest = float.MaxValue;
                for (int k = 0; k < 6; k++)
                {
                    float a = k * Mathf.PI / 3f;
                    lowest = Mathf.Min(lowest, HeightAt(x + Mathf.Cos(a) * s * 0.6f, z + Mathf.Sin(a) * s * 0.6f));
                }
                var n = NormalAt(x, z);
                var rot = Quaternion.FromToRotation(Vector3.up, Vector3.Lerp(Vector3.up, n, 0.7f)) * Quaternion.Euler(0, Random.Range(0, 360), 0);
                var pos = new Vector3(x, lowest - s * 0.12f, z);
                if (q) nb.Add("Rock_Medium_" + Random.Range(1, 4), pos, rot, s * 0.8f, 1.3f);
                else batch.Add(rockMeshes[Random.Range(0, rockMeshes.Length)], Matrix4x4.TRS(pos, rot, new Vector3(s * 1.25f, s * 0.75f, s)), rockCols[Random.Range(0, rockCols.Length)]);
            }
            batch.Build(detail, "Detail", false);
            nb.Build(detail, "DetailQ", false);
        }

        /// <summary>Peter's painted grass tufts, flower patches, bushes, rocks, logs and stumps (one atlas, a few meshes).</summary>
        void PaintedDetails(Transform detail)
        {
            var b = new NatureSprites.Batch();
            var fieldGo = new GameObject("DecorField");
            fieldGo.transform.SetParent(detail, false);
            var f = fieldGo.AddComponent<DecorField>();
            bool Yard(float x, float z) { return Mathf.Abs(x - GameManager.CastleCenter.x) < 4f && Mathf.Abs(z - GameManager.CastleCenter.z) < 4f; }
            // grass tufts in natural clumps across the valley
            for (int i = 0; i < 700; i++)
            {
                float x = Random.Range(-GridMap.ForestX - 6f, GridMap.ForestX + 6f), z = Random.Range(GridMap.SeaZ + 2.5f, GridMap.MountainZ + 3f);
                if (Yard(x, z) || River.Mask(x, z) > 0.2f || Noise(x + 50f, z, 0.08f) < 0.5f) continue;
                f.Add(NatureSprites.Pick("tuft_"), new Vector3(x, HeightAt(x, z), z), Random.Range(0.3f, 0.55f));
            }
            // flower and clover patches lying on the meadow
            for (int i = 0; i < 70; i++)
            {
                float x = Random.Range(-GridMap.ForestX, GridMap.ForestX), z = Random.Range(GridMap.SeaZ + 4f, GridMap.MountainZ);
                if (Yard(x, z) || River.Mask(x, z) > 0.2f || Noise(x, z, 0.12f) < 0.55f) continue;
                f.Add(NatureSprites.Pick("patch_"), new Vector3(x, HeightAt(x, z), z), Random.Range(0.45f, 0.7f), 0.35f);
            }
            // bushes along the forest edges
            for (int i = 0; i < 160; i++)
            {
                float side = Random.value < 0.5f ? -1f : 1f;
                float x = side * Random.Range(GridMap.ForestX - 6f, GridMap.ForestX + 4f), z = Random.Range(GridMap.SeaZ + 3f, GridMap.MountainZ + 2f);
                if (River.Mask(x, z) > 0.2f) continue;
                f.Add(NatureSprites.Pick("bush_a", "bush_a", "bush_lime", "bush_white", "bush_pink", "bush_blue", "bush_red", "fern"), new Vector3(x, HeightAt(x, z), z), Random.Range(0.8f, 1.2f));
            }
            // rocks, logs and stumps here and there; boulders on the mountain
            for (int i = 0; i < 60; i++)
            {
                float x = Random.Range(-GridMap.ForestX - 8f, GridMap.ForestX + 8f), z = Random.Range(GridMap.SeaZ + 1f, GridMap.MountainZ + 4f);
                if (Yard(x, z) || River.Mask(x, z) > 0.2f) continue;
                // smaller than before: they are props, not obstacles
                f.Add(NatureSprites.Pick("rock_", "rock_", "log", "stump"), new Vector3(x, HeightAt(x, z), z), Random.Range(0.4f, 0.65f));
            }
            for (int i = 0; i < 160; i++)
            {
                float x = Random.Range(-56f, 56f), z = Random.Range(GridMap.MountainZ - 1f, GridMap.MountainZ + 24f);
                if (River.Mask(x, z) > 0.2f || SlopeAt(x, z) > 1.1f) continue;
                b.Add(NatureSprites.Pick("rock_moss", "rock_big", "rock_mid"), new Vector3(x, HeightAt(x, z), z), Random.Range(0.9f, 2.0f), 0.12f, Random.Range(0.85f, 1f));
            }
            b.Build(detail, "DetailPainted");
        }

        /// <summary>Low stone markers around the area the castle level allows building in.</summary>
        public void ShowBuildBorder(int radius)
        {
            if (radius == borderRadius) return;
            borderRadius = radius;
            for (int i = borderRoot.childCount - 1; i >= 0; i--) Destroy(borderRoot.GetChild(i).gameObject);
            var c = new Color(0.72f, 0.68f, 0.6f);
            int back = GridMap.BuildBackZ, front = GridMap.BuildBackZ - 2 * radius;
            for (int i = -radius; i <= radius; i += 2)
            {
                Marker(new Vector3(i, 0f, front), c);
                Marker(new Vector3(i, 0f, back), c);
            }
            for (int z = front; z <= back; z += 2)
            {
                Marker(new Vector3(-radius, 0f, z), c);
                Marker(new Vector3(radius, 0f, z), c);
            }
        }

        void Marker(Vector3 p, Color c)
        {
            // the wall loop now marks the edge of the land: no little beige border cubes any more (QC run 26: "stray cubes")
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
