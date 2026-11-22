using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>Procedural meshes that Unity has no primitive for (cones, gable roofs), plus mesh combining.</summary>
    public static class MeshFactory
    {
        static readonly Dictionary<string, Mesh> cache = new Dictionary<string, Mesh>();

        /// <summary>Cone of radius 0.5 and height 1, base at y=0. segments=4 gives a pyramid roof.</summary>
        public static Mesh Cone(int segments)
        {
            string key = "cone" + segments;
            Mesh m;
            if (cache.TryGetValue(key, out m)) return m;
            var v = new List<Vector3>(); var t = new List<int>();
            float off = segments == 4 ? Mathf.PI / 4f : 0f;
            for (int i = 0; i < segments; i++)
            {
                float a0 = off + i * Mathf.PI * 2f / segments, a1 = off + (i + 1) * Mathf.PI * 2f / segments;
                var p0 = new Vector3(Mathf.Cos(a0) * 0.5f, 0, Mathf.Sin(a0) * 0.5f);
                var p1 = new Vector3(Mathf.Cos(a1) * 0.5f, 0, Mathf.Sin(a1) * 0.5f);
                int b = v.Count;
                v.Add(p0); v.Add(Vector3.up); v.Add(p1);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
                b = v.Count; // bottom
                v.Add(p0); v.Add(p1); v.Add(Vector3.zero);
                t.Add(b); t.Add(b + 1); t.Add(b + 2);
            }
            m = Build(key, v, t);
            return m;
        }

        /// <summary>Gable roof (triangular prism): 1 wide (x), 1 deep (z), 1 high, base at y=0, ridge along z.</summary>
        public static Mesh Gable()
        {
            Mesh m;
            if (cache.TryGetValue("gable", out m)) return m;
            var v = new List<Vector3>(); var t = new List<int>();
            Vector3 a = new Vector3(-0.5f, 0, -0.5f), b = new Vector3(0.5f, 0, -0.5f), c = new Vector3(0, 1, -0.5f);
            Vector3 a2 = new Vector3(-0.5f, 0, 0.5f), b2 = new Vector3(0.5f, 0, 0.5f), c2 = new Vector3(0, 1, 0.5f);
            Quad(v, t, a, c, c2, a2);   // left slope
            Quad(v, t, c, b, b2, c2);   // right slope
            Tri(v, t, a, b, c);         // front gable (faces -z)
            Tri(v, t, b2, a2, c2);      // back gable
            Quad(v, t, a2, b2, b, a);   // bottom
            m = Build("gable", v, t);
            return m;
        }

        static void Tri(List<Vector3> v, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2)
        {
            int i = v.Count; v.Add(p0); v.Add(p1); v.Add(p2);
            t.Add(i); t.Add(i + 2); t.Add(i + 1);
        }

        static void Quad(List<Vector3> v, List<int> t, Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3)
        {
            Tri(v, t, p0, p1, p2); Tri(v, t, p0, p2, p3);
        }

        static Mesh Build(string key, List<Vector3> v, List<int> t)
        {
            var m = new Mesh { name = key };
            m.SetVertices(v);
            m.SetTriangles(t, 0);
            m.RecalculateNormals();
            m.RecalculateBounds();
            // make winding consistent: if normals point inward, flip
            cache[key] = m;
            return m;
        }

        public static GameObject Create(Mesh mesh, Transform parent, Vector3 pos, Vector3 scale, Color color)
        {
            var go = new GameObject(mesh.name);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = pos;
            go.transform.localScale = scale;
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            go.AddComponent<MeshRenderer>().sharedMaterial = Materials.Get(color);
            return go;
        }

        // ---------- Batching for decoration (thousands of small pieces -> a few draw calls) ----------

        static readonly Dictionary<PrimitiveType, Mesh> primitiveMeshes = new Dictionary<PrimitiveType, Mesh>();

        public static Mesh PrimitiveMesh(PrimitiveType type)
        {
            Mesh m;
            if (primitiveMeshes.TryGetValue(type, out m)) return m;
            var go = GameObject.CreatePrimitive(type);
            m = go.GetComponent<MeshFilter>().sharedMesh;
            Object.Destroy(go);
            primitiveMeshes[type] = m;
            return m;
        }

        public class Batcher
        {
            readonly Dictionary<Color, List<CombineInstance>> parts = new Dictionary<Color, List<CombineInstance>>();

            public void Add(Mesh mesh, Matrix4x4 matrix, Color color)
            {
                List<CombineInstance> list;
                if (!parts.TryGetValue(color, out list)) { list = new List<CombineInstance>(); parts[color] = list; }
                list.Add(new CombineInstance { mesh = mesh, transform = matrix });
            }

            public void Add(PrimitiveType type, Vector3 pos, Quaternion rot, Vector3 scale, Color color)
            {
                Add(PrimitiveMesh(type), Matrix4x4.TRS(pos, rot, scale), color);
            }

            public void Build(Transform parent, string name, bool castShadows)
            {
                foreach (var kv in parts)
                {
                    var list = kv.Value;
                    for (int start = 0; start < list.Count; start += 1500)
                    {
                        int count = Mathf.Min(1500, list.Count - start);
                        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        mesh.CombineMeshes(list.GetRange(start, count).ToArray(), true, true);
                        var go = new GameObject(name);
                        go.transform.SetParent(parent, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = go.AddComponent<MeshRenderer>();
                        r.sharedMaterial = Materials.Get(kv.Key);
                        r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                parts.Clear();
            }
        }
    }
}
