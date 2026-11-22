using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>Marks a grid cell as part of the river (not buildable, not attackable).</summary>
    public sealed class RiverCell
    {
        public static readonly RiverCell Instance = new RiverCell();
    }

    /// <summary>
    /// A river that springs at the foot of the mountain and winds down to the sea.
    /// Canals dug next to it carry its water to farms and wells.
    /// </summary>
    public static class River
    {
        public const float HalfWidth = 1.1f;
        /// <summary>The river widens into the painted pool at its spring, so there is no grass between the two (QC runs 34-35).</summary>
        public static float HalfWidthAt(float z)
        {
            return HalfWidth + 2.6f * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((z - (StartZ - 7f)) / 5f));
        }
        public const float StartZ = GridMap.MountainZ + 15f;  // springs up in the hills
        public const float FallZ = GridMap.MountainZ + 5f;    // falls over a rock ledge here
        public const float EndZ = GridMap.SeaZ - 6f;

        public static float CenterX(float z)
        {
            return 13f + 3.2f * Mathf.Sin(z * 0.12f + 1.1f) + 1.2f * Mathf.Sin(z * 0.31f);
        }

        /// <summary>0..1, 1 in the middle of the river bed.</summary>
        public static float Mask(float x, float z)
        {
            if (z > StartZ + 1f || z < EndZ) return 0f;
            float d = Mathf.Abs(x - CenterX(z));
            float hw = HalfWidthAt(z);
            float m = 1f - Mathf.Clamp01((d - hw * 0.6f) / (HalfWidth * 1.0f));
            if (z > StartZ - 2f) m *= Mathf.Clamp01((StartZ + 1f - z) / 3f); // spring fades in
            return m;
        }

        public static bool IsRiver(Vector2Int c)
        {
            return Mask(c.x + 0.5f, c.y + 0.5f) > 0.55f && c.y >= GridMap.SeaZ;
        }

        public static void MarkCells(GridMap grid)
        {
            for (int z = GridMap.SeaZ; z <= (int)StartZ; z++)
                for (int x = 0; x < 30; x++)
                {
                    var c = new Vector2Int(x, z);
                    if (IsRiver(c)) grid.Occupy(new[] { c }, RiverCell.Instance);
                }
        }

        /// <summary>
        /// The river falls over the rock ledge: a sheet of white falling water, a foamy pool at its
        /// foot and drifting spray. Big rocks frame the fall, buried in the bank.
        /// </summary>
        static void BuildWaterfall(Transform parent)
        {
            // find the steepest stretch of the river
            float topZ = 0f, bottomZ = 0f, best = 0f;
            for (float z = StartZ - 1f; z > GridMap.MountainZ - 2f; z -= 0.25f)
            {
                float drop = WorldBuilder.HeightAt(CenterX(z), z) - WorldBuilder.HeightAt(CenterX(z - 1.5f), z - 1.5f);
                if (drop > best) { best = drop; topZ = z; bottomZ = z - 1.5f; }
            }
            if (best < 0.8f) return;
            // widen the window to include the whole drop
            while (WorldBuilder.HeightAt(CenterX(bottomZ), bottomZ) - WorldBuilder.HeightAt(CenterX(bottomZ - 0.25f), bottomZ - 0.25f) > 0.15f && bottomZ > GridMap.MountainZ - 4f) bottomZ -= 0.25f;
            while (WorldBuilder.HeightAt(CenterX(topZ + 0.25f), topZ + 0.25f) - WorldBuilder.HeightAt(CenterX(topZ), topZ) > 0.15f && topZ < StartZ - 1f) topZ += 0.25f;

            var verts = new List<Vector3>(); var uvs = new List<Vector2>(); var tris = new List<int>();
            int rows = 0;
            float w = HalfWidth * 1.15f;
            for (float z = topZ + 0.3f; z >= bottomZ - 0.2f; z -= 0.2f)
            {
                float cx = CenterX(z);
                float y = WorldBuilder.HeightAt(cx, z) + 0.32f;
                verts.Add(new Vector3(cx - w, y, z - 0.2f)); verts.Add(new Vector3(cx + w, y, z - 0.2f));
                float v = (topZ - z) * 0.6f;
                uvs.Add(new Vector2(0, v)); uvs.Add(new Vector2(1, v));
                if (rows > 0) { int i = verts.Count - 4; tris.Add(i); tris.Add(i + 1); tris.Add(i + 2); tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2); }
                rows++;
            }
            var mesh = new Mesh { name = "Waterfall" };
            mesh.SetVertices(verts); mesh.SetUVs(0, uvs); mesh.SetTriangles(tris, 0);
            var cols = new Color[verts.Count]; for (int i = 0; i < cols.Length; i++) cols[i] = Color.white; mesh.colors = cols;
            mesh.RecalculateNormals(); mesh.RecalculateBounds();
            var fall = new GameObject("Waterfall");
            fall.transform.SetParent(parent, false);
            fall.AddComponent<MeshFilter>().sharedMesh = mesh;
            var mat = DamageEffects.ParticleMaterial(false, StreakTexture());
            Materials.SetColor(mat, new Color(0.92f, 0.97f, 1f, 0.95f));
            fall.AddComponent<MeshRenderer>().sharedMaterial = mat;
            var flow = fall.AddComponent<WaterFlow>();
            flow.target = mat; flow.property = "_BaseMap"; flow.speed = new Vector2(0f, 1.1f); flow.shared = false;

            // foam pool and spray at the foot
            var foot = new Vector3(CenterX(bottomZ - 0.6f), 0f, bottomZ - 0.6f);
            foot.y = WorldBuilder.HeightAt(foot) + 0.28f;
            var pool = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            Object.Destroy(pool.GetComponent<Collider>());
            pool.name = "FoamPool";
            pool.transform.SetParent(parent, false);
            pool.transform.position = foot;
            pool.transform.localScale = new Vector3(w * 2.4f, 0.01f, 1.8f);
            var pm = DamageEffects.ParticleMaterial(false);
            Materials.SetColor(pm, new Color(1f, 1f, 1f, 0.6f));
            pool.GetComponent<Renderer>().sharedMaterial = pm;
            DamageEffects.Mist(parent, foot + Vector3.up * 0.2f);

            // rock outcrops on both sides of the fall, sunk into the slope
            var rockMesh = MeshFactory.Rock(5);
            for (int i = 0; i < 6; i++)
            {
                float z = Mathf.Lerp(bottomZ, topZ + 0.5f, i / 5f);
                float side = i % 2 == 0 ? -1f : 1f;
                float x = CenterX(z) + side * (w + Random.Range(0.5f, 1.0f));
                float s = Random.Range(0.9f, 1.6f);
                var r = new GameObject("FallRock");
                r.transform.SetParent(parent, false);
                r.AddComponent<MeshFilter>().sharedMesh = rockMesh;
                r.AddComponent<MeshRenderer>().sharedMaterial = Materials.Get(new Color(0.42f, 0.4f, 0.38f));
                r.transform.position = new Vector3(x, WorldBuilder.GroundUnder(x, z, s * 0.6f) - s * 0.2f, z);
                r.transform.rotation = Quaternion.Euler(Random.Range(-10f, 10f), Random.Range(0f, 360f), Random.Range(-10f, 10f));
                r.transform.localScale = new Vector3(s * 1.2f, s * 0.9f, s);
            }
        }

        static Texture2D streak;

        /// <summary>Vertical white streaks with soft gaps: reads as falling water when it scrolls.</summary>
        static Texture2D StreakTexture()
        {
            if (streak != null) return streak;
            const int n = 64;
            streak = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            var rnd = new System.Random(3);
            var colAlpha = new float[n];
            for (int x = 0; x < n; x++) colAlpha[x] = 0.45f + (float)rnd.NextDouble() * 0.5f;
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float edge = Mathf.Clamp01(Mathf.Min(x, n - 1 - x) / 6f);
                    float wave = 0.75f + 0.25f * Mathf.Sin((y / (float)n) * Mathf.PI * 2f * 3f + x * 0.7f);
                    float a = colAlpha[x] * wave * edge;
                    float b = 0.85f + 0.15f * wave;
                    px[y * n + x] = new Color(b, b, 1f, a);
                }
            streak.SetPixels(px);
            streak.Apply(true);
            return streak;
        }

        /// <summary>Water surface strip that follows the river bed.</summary>
        public static void BuildVisual(Transform parent)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float step = 0.25f;   // finer rows: a polygon kink showed in the water edge (QC run 30)
            int rows = 0;
            float prevY = 0f;
            // the water starts at the painted pool (the backdrop foot), never under the painting (QC run 23: grey smudge)
            for (float z = StartZ + 1f; z >= EndZ; z -= step)   // QC run 28: reach back under the painted pool so no bare bed shows
            {
                float cx = CenterX(z);
                float w = HalfWidthAt(z) * 1.05f;   // full width up to the pool: a narrow spring left bare sandy bed on both sides (QC run 28)
                // the water reaches its own smooth mesh edge instead of meeting the terrain's triangles (QC run 32: corners in the bank line)
                float edgeH = Mathf.Min(Mathf.Max(WorldBuilder.HeightAt(cx - w, z), WorldBuilder.HeightAt(cx + w, z)) + 0.03f, WorldBuilder.HeightAt(cx, z) + 0.6f);
                float y = Mathf.Max(Mathf.Max(WorldBuilder.HeightAt(cx, z) + 0.22f, WorldBuilder.WaterLevel + 0.02f), edgeH);
                verts.Add(new Vector3(cx - w, y, z));
                verts.Add(new Vector3(cx + w, y, z));
                bool fall = rows > 0 && prevY - y > 0.45f && z < StartZ - 6f;   // the waterfall sheet covers steep drops; never a hole between the painted pool and the river (QC run 34)
                prevY = y;
                if (rows > 0 && !fall)
                {
                    int i = verts.Count - 4;
                    tris.Add(i); tris.Add(i + 1); tris.Add(i + 2);
                    tris.Add(i + 1); tris.Add(i + 3); tris.Add(i + 2);
                }
                rows++;
            }
            var mesh = new Mesh { name = "River" };
            mesh.SetVertices(verts);
            mesh.SetTriangles(tris, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var go = new GameObject("River");
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var m = Materials.Water();
            go.AddComponent<MeshRenderer>().sharedMaterial = m;
            var flow = go.AddComponent<WaterFlow>();
            flow.target = m;

            BuildWaterfall(go.transform);
        }
    }
}
