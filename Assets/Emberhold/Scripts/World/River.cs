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
        public const float StartZ = GridMap.MountainZ + 7f;
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
            float m = 1f - Mathf.Clamp01((d - HalfWidth * 0.6f) / (HalfWidth * 1.0f));
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

        /// <summary>Water surface strip that follows the river bed.</summary>
        public static void BuildVisual(Transform parent)
        {
            var verts = new List<Vector3>();
            var tris = new List<int>();
            float step = 0.5f;
            int rows = 0;
            for (float z = StartZ; z >= EndZ; z -= step)
            {
                float cx = CenterX(z);
                float y = Mathf.Max(WorldBuilder.HeightAt(cx, z) + 0.22f, WorldBuilder.WaterLevel + 0.02f);
                float w = HalfWidth * 1.05f * (z > StartZ - 2f ? Mathf.Clamp01((StartZ - z) / 2f + 0.2f) : 1f);
                verts.Add(new Vector3(cx - w, y, z));
                verts.Add(new Vector3(cx + w, y, z));
                if (rows > 0)
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
            var m = Materials.Unique(new Color(0.16f, 0.42f, 0.52f));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.9f);
            go.AddComponent<MeshRenderer>().sharedMaterial = m;

            // spring: a few rocks where the river leaves the mountain
            var rock = new Color(0.5f, 0.48f, 0.46f);
            for (int i = 0; i < 6; i++)
            {
                float z = StartZ + Random.Range(-0.5f, 1.2f);
                float x = CenterX(z) + Random.Range(-1.8f, 1.8f);
                float s = Random.Range(0.5f, 1.1f);
                Util.Prim(PrimitiveType.Sphere, go.transform, new Vector3(x, WorldBuilder.HeightAt(x, z), z), new Vector3(s * 1.3f, s * 0.8f, s), rock);
            }
        }
    }
}
