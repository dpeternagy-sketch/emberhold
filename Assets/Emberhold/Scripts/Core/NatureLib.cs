using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Quaternius Stylized Nature (CC0) trees, bushes, rocks and grass. The models are stored as small
    /// binary meshes in Resources/Models/Nature (converted from glTF, Unity has no built-in glTF importer),
    /// with their textures in Resources/Models/Nature/Tex. Leaves use alpha-clipped, double-sided materials.
    /// </summary>
    public static class NatureLib
    {
        public class Model
        {
            public Mesh mesh;            // all parts as submeshes
            public Mesh[] parts;         // one mesh per submesh (for batching)
            public Material[] mats;
            public Bounds bounds;
        }

        static readonly Dictionary<string, Model> models = new Dictionary<string, Model>();
        static readonly Dictionary<string, Material> mats = new Dictionary<string, Material>();
        static int available = -1;

        public static bool Available
        {
            get
            {
                if (available < 0) available = Resources.Load<TextAsset>("Models/Nature/Pine_1") != null ? 1 : 0;
                return available == 1;
            }
        }

        public static Model Get(string name)
        {
            Model m;
            if (models.TryGetValue(name, out m)) return m;
            m = Load(name);
            models[name] = m;
            return m;
        }

        static string ReadStr(BinaryReader r) { int n = r.ReadInt32(); return System.Text.Encoding.UTF8.GetString(r.ReadBytes(n)); }

        static Model Load(string name)
        {
            var ta = Resources.Load<TextAsset>("Models/Nature/" + name);
            if (ta == null) return null;
            using (var r = new BinaryReader(new MemoryStream(ta.bytes)))
            {
                if (new string(r.ReadChars(4)) != "EHM1") return null;
                int subs = r.ReadInt32();
                var verts = new List<Vector3>(); var norms = new List<Vector3>(); var uvs = new List<Vector2>();
                var tris = new List<int[]>();
                var model = new Model { parts = new Mesh[subs], mats = new Material[subs] };
                for (int s = 0; s < subs; s++)
                {
                    string mat = ReadStr(r);
                    bool mask = r.ReadByte() == 1;
                    float cut = r.ReadSingle();
                    string tex = ReadStr(r);
                    var col = new Color(r.ReadSingle(), r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                    int vc = r.ReadInt32();
                    int baseV = verts.Count;
                    var pv = new Vector3[vc]; var pn = new Vector3[vc]; var pu = new Vector2[vc];
                    for (int i = 0; i < vc; i++)
                    {
                        pv[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        pn[i] = new Vector3(r.ReadSingle(), r.ReadSingle(), r.ReadSingle());
                        pu[i] = new Vector2(r.ReadSingle(), r.ReadSingle());
                    }
                    int ic = r.ReadInt32();
                    var pi = new int[ic];
                    for (int i = 0; i < ic; i++) pi[i] = r.ReadInt32();
                    var part = new Mesh { name = name + "_" + s };
                    part.SetVertices(pv); part.SetNormals(pn); part.SetUVs(0, pu); part.SetTriangles(pi, 0);
                    part.RecalculateBounds();
                    model.parts[s] = part;
                    verts.AddRange(pv); norms.AddRange(pn); uvs.AddRange(pu);
                    var shifted = new int[ic];
                    for (int i = 0; i < ic; i++) shifted[i] = pi[i] + baseV;
                    tris.Add(shifted);
                    model.mats[s] = Mat(mat, tex, mask, cut, col);
                }
                var mesh = new Mesh { name = name };
                if (verts.Count > 65000) mesh.indexFormat = UnityEngine.Rendering.IndexFormat.UInt32;
                mesh.SetVertices(verts); mesh.SetNormals(norms); mesh.SetUVs(0, uvs);
                mesh.subMeshCount = subs;
                for (int s = 0; s < subs; s++) mesh.SetTriangles(tris[s], s);
                mesh.RecalculateBounds();
                model.mesh = mesh;
                model.bounds = mesh.bounds;
                return model;
            }
        }

        // Tints that pull the Quaternius colours toward the game palette (bright, saturated cartoon greens).
        static Color Tint(string mat)
        {
            if (mat.StartsWith("Leaves_Pine")) return new Color(0.82f, 1.0f, 0.86f);
            if (mat.StartsWith("Leaves")) return new Color(1.0f, 1.08f, 0.92f);
            if (mat.StartsWith("Grass")) return new Color(1.0f, 1.05f, 0.9f);
            return Color.white;
        }

        static Material Mat(string mat, string tex, bool mask, float cut, Color col)
        {
            string key = mat + "|" + tex;
            Material m;
            if (mats.TryGetValue(key, out m) && m != null) return m;
            m = new Material(Materials.Get(Color.white));
            m.name = "Nature_" + mat;
            var t = string.IsNullOrEmpty(tex) ? null : Resources.Load<Texture2D>("Models/Nature/Tex/" + tex);
            if (t != null)
            {
                if (m.HasProperty("_BaseMap")) m.SetTexture("_BaseMap", t);
                if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", t);
            }
            Materials.SetColor(m, col * Tint(mat));
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.05f);
            if (mask || mat.StartsWith("Grass"))
            {
                if (m.HasProperty("_AlphaClip")) m.SetFloat("_AlphaClip", 1f);
                if (m.HasProperty("_Cutoff")) m.SetFloat("_Cutoff", Mathf.Clamp(cut, 0.3f, 0.6f));
                m.EnableKeyword("_ALPHATEST_ON");
                if (m.HasProperty("_Cull")) m.SetFloat("_Cull", 0f);   // leaf cards are seen from both sides
                m.doubleSidedGI = true;
                m.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            }
            m.enableInstancing = true;
            mats[key] = m;
            return m;
        }

        /// <summary>Spawns one model as a child object, base at the parent's origin, scaled to the given height.</summary>
        public static Transform Spawn(string name, Transform parent, float height, bool shadows = true)
        {
            var m = Get(name);
            if (m == null) return null;
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            float s = height / Mathf.Max(0.01f, m.bounds.size.y);
            go.transform.localScale = Vector3.one * s;
            go.transform.localPosition = new Vector3(0, -m.bounds.min.y * s, 0);
            go.AddComponent<MeshFilter>().sharedMesh = m.mesh;
            var r = go.AddComponent<MeshRenderer>();
            r.sharedMaterials = m.mats;
            r.shadowCastingMode = shadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
            return go.transform;
        }

        /// <summary>Merges many static nature models into a few meshes (one per material) so phones stay smooth.</summary>
        public class Batcher
        {
            readonly Dictionary<Material, List<CombineInstance>> parts = new Dictionary<Material, List<CombineInstance>>();

            /// <summary>Adds a model with its base at pos, scaled so it is `height` tall.</summary>
            public bool Add(string name, Vector3 pos, Quaternion rot, float height, float squash = 1f)
            {
                var m = Get(name);
                if (m == null) return false;
                float s = height / Mathf.Max(0.01f, m.bounds.size.y);
                var mat = Matrix4x4.TRS(pos - rot * new Vector3(0, m.bounds.min.y * s, 0), rot, new Vector3(s * squash, s, s * squash));
                for (int i = 0; i < m.parts.Length; i++)
                {
                    List<CombineInstance> list;
                    if (!parts.TryGetValue(m.mats[i], out list)) { list = new List<CombineInstance>(); parts[m.mats[i]] = list; }
                    list.Add(new CombineInstance { mesh = m.parts[i], transform = mat });
                }
                return true;
            }

            public void Build(Transform parent, string name, bool castShadows)
            {
                foreach (var kv in parts)
                {
                    var list = kv.Value;
                    const int chunk = 300;
                    for (int start = 0; start < list.Count; start += chunk)
                    {
                        int count = Mathf.Min(chunk, list.Count - start);
                        var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                        mesh.CombineMeshes(list.GetRange(start, count).ToArray(), true, true);
                        var go = new GameObject(name);
                        go.transform.SetParent(parent, false);
                        go.AddComponent<MeshFilter>().sharedMesh = mesh;
                        var r = go.AddComponent<MeshRenderer>();
                        r.sharedMaterial = kv.Key;
                        r.shadowCastingMode = castShadows ? UnityEngine.Rendering.ShadowCastingMode.On : UnityEngine.Rendering.ShadowCastingMode.Off;
                    }
                }
                parts.Clear();
            }
        }
    }
}
