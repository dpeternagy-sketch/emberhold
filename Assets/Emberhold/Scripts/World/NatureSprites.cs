using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Peter's painted vegetation (Resources/Sprites/Nature/atlas.png + atlas.txt: name u0 v0 u1 v1 aspect).
    /// Every item is a quad that faces the locked camera angle, stood on its foot point, and many are merged
    /// into one mesh so the whole forest is a handful of draw calls. Groups pick a random look by prefix
    /// ("tree_", "pine_", "bush_", "tuft_", "patch_", "rock_").
    /// </summary>
    public static class NatureSprites
    {
        public struct Item { public string name; public Rect uv; public float aspect; }

        static Dictionary<string, Item> items;
        static Material material;
        public static readonly Quaternion Facing = Quaternion.Euler(CameraRig.Pitch, CameraRig.Yaw, 0f);

        public static bool Available { get { Init(); return items != null && items.Count > 0; } }

        static void Init()
        {
            if (items != null) return;
            items = new Dictionary<string, Item>();
            var tex = Resources.Load<Texture2D>("Sprites/Nature/atlas");
            var meta = Resources.Load<TextAsset>("Sprites/Nature/atlas");
            var shader = Resources.Load<Shader>("Shaders/NatureSprite");
            if (tex == null || meta == null || shader == null || !shader.isSupported) return;
            var ci = System.Globalization.CultureInfo.InvariantCulture;
            foreach (var line in meta.text.Split('\n'))
            {
                var p = line.Trim().Split(' ');
                if (p.Length < 6) continue;
                float u0 = float.Parse(p[1], ci), v0 = float.Parse(p[2], ci), u1 = float.Parse(p[3], ci), v1 = float.Parse(p[4], ci);
                items[p[0]] = new Item { name = p[0], uv = Rect.MinMaxRect(u0, v0, u1, v1), aspect = float.Parse(p[5], ci) };
            }
            material = new Material(shader);
            material.SetTexture("_MainTex", tex);
        }

        /// <summary>A random item whose name starts with one of the given prefixes (weights = repeat a prefix).</summary>
        public static Item Pick(params string[] prefixes)
        {
            Init();
            string pre = prefixes[Random.Range(0, prefixes.Length)];
            var list = new List<Item>();
            foreach (var kv in items) if (kv.Key.StartsWith(pre)) list.Add(kv.Value);
            if (list.Count == 0) foreach (var kv in items) { list.Add(kv.Value); break; }
            return list[Random.Range(0, list.Count)];
        }

        public static bool Has(string name) { Init(); return items.ContainsKey(name); }
        public static Item Get(string name) { Init(); return items[name]; }

        /// <summary>
        /// Quad corners for an item standing on foot (world), height in world units.
        /// sink: how far (0..1 of the height) the picture's bottom goes below the foot (grass patches sit centred).
        /// </summary>
        static void Corners(Item it, Vector3 foot, float height, float sink, bool flip, Vector3[] c)
        {
            var right = Facing * Vector3.right * (height * it.aspect * 0.5f) * (flip ? -1f : 1f);
            var up = Facing * Vector3.up * height;
            var bottom = foot - up * sink;
            c[0] = bottom - right; c[1] = bottom + right; c[2] = bottom + up + right; c[3] = bottom + up - right;
        }

        public class Batch
        {
            readonly List<Vector3> v = new List<Vector3>();
            readonly List<Vector2> uv = new List<Vector2>();
            readonly List<Color> col = new List<Color>();
            readonly List<int> tri = new List<int>();
            readonly Vector3[] c = new Vector3[4];

            public int Count { get { return v.Count / 4; } }

            public void Add(Item it, Vector3 foot, float height, float sink = 0.06f, float shade = 1f)
            {
                Add(it, foot, height, sink, shade, Random.value < 0.5f);
            }

            public void Add(Item it, Vector3 foot, float height, float sink, float shade, bool flip)
            {
                Corners(it, foot, height, sink, flip, c);
                int b = v.Count;
                v.AddRange(c);
                var r = it.uv;
                uv.Add(new Vector2(r.xMin, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMin)); uv.Add(new Vector2(r.xMax, r.yMax)); uv.Add(new Vector2(r.xMin, r.yMax));
                var k = new Color(shade, shade, shade, 1f);
                col.Add(k); col.Add(k); col.Add(k); col.Add(k);
                tri.Add(b); tri.Add(b + 2); tri.Add(b + 1); tri.Add(b); tri.Add(b + 3); tri.Add(b + 2);
            }

            public GameObject Build(Transform parent, string name)
            {
                if (v.Count == 0 || material == null) return null;
                // draw back to front isn't needed (alpha-cut + depth), but sorting by depth keeps overdraw low
                var mesh = new Mesh { name = name, indexFormat = UnityEngine.Rendering.IndexFormat.UInt32 };
                mesh.SetVertices(v); mesh.SetUVs(0, uv); mesh.SetColors(col); mesh.SetTriangles(tri, 0);
                mesh.RecalculateBounds();
                var go = new GameObject(name);
                go.transform.SetParent(parent, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                var r = go.AddComponent<MeshRenderer>();
                r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                r.receiveShadows = false;
                return go;
            }
        }

        /// <summary>One item as its own object (trees near the city, which can be chopped and regrow).</summary>
        public static GameObject Single(Transform parent, Item it, float height)
        {
            Init();
            var b = new Batch();
            var saved = Random.state;
            b.Add(it, Vector3.zero, height);
            Random.state = saved;
            var holder = new GameObject("Sprite");
            holder.transform.SetParent(parent, false);
            b.Build(holder.transform, "NatureSprite");
            return holder;
        }
    }

    /// <summary>
    /// Grass tufts, flowers, rocks, logs and stumps on the valley floor, in 8x8-cell chunk meshes.
    /// Anything standing on a cell that a building, wall or gate now covers is left out, and the chunk
    /// is rebuilt when that changes, so nothing pokes up through a building's plate.
    /// </summary>
    public class DecorField : MonoBehaviour
    {
        struct It { public NatureSprites.Item item; public Vector3 foot; public float h, sink, shade; public bool flip; public Vector2Int cell; }
        const int C = 8;
        readonly Dictionary<Vector2Int, List<It>> chunks = new Dictionary<Vector2Int, List<It>>();
        readonly Dictionary<Vector2Int, GameObject> meshes = new Dictionary<Vector2Int, GameObject>();
        readonly Dictionary<Vector2Int, int> shownSig = new Dictionary<Vector2Int, int>();
        float nextCheck;
        public static DecorField Instance { get; private set; }

        void Awake() { Instance = this; }

        /// <summary>
        /// The rock, log or stump nearest to a ground point (Peter: tap one to clear it for resources).
        /// Grass and flowers are not obstacles and are never picked.
        /// </summary>
        public bool FindObstacle(Vector3 ground, float radius, out Vector3 foot, out string name)
        {
            foot = Vector3.zero; name = null;
            float best = radius * radius;
            var k0 = ChunkOf(GridMap.CellOf(ground));
            for (int dx = -1; dx <= 1; dx++)
                for (int dz = -1; dz <= 1; dz++)
                {
                    List<It> list;
                    if (!chunks.TryGetValue(k0 + new Vector2Int(dx, dz), out list)) continue;
                    foreach (var it in list)
                    {
                        var n = it.item.name;
                        if (!(n.StartsWith("rock_") || n == "log" || n == "stump") || Covered(it.cell)) continue;
                        var d = it.foot - ground; d.y = 0f;
                        if (d.sqrMagnitude < best) { best = d.sqrMagnitude; foot = it.foot; name = n; }
                    }
                }
            return name != null;
        }

        /// <summary>Takes the obstacle standing at this foot point off the map.</summary>
        public void Remove(Vector3 foot)
        {
            var k = ChunkOf(GridMap.CellOf(foot));
            List<It> list;
            if (!chunks.TryGetValue(k, out list)) return;
            int i = list.FindIndex(it => (it.foot - foot).sqrMagnitude < 0.0001f);
            if (i < 0) return;
            list.RemoveAt(i);
            Rebuild(k, Signature(list));
        }

        static Vector2Int ChunkOf(Vector2Int c) { return new Vector2Int(Mathf.FloorToInt(c.x / (float)C), Mathf.FloorToInt(c.y / (float)C)); }

        public void Add(NatureSprites.Item it, Vector3 foot, float height, float sink = 0.06f, float shade = 1f)
        {
            var cell = GridMap.CellOf(foot);
            var k = ChunkOf(cell);
            List<It> list;
            if (!chunks.TryGetValue(k, out list)) chunks[k] = list = new List<It>();
            list.Add(new It { item = it, foot = foot, h = height, sink = sink, shade = shade, flip = Random.value < 0.5f, cell = cell });
        }

        static bool Covered(Vector2Int c)
        {
            var gm = GameManager.Instance;
            if (gm == null || gm.Grid == null) return false;
            var b = gm.Grid.At(c) as Building;
            return b != null && !b.IsGhost;
        }

        int Signature(List<It> list)
        {
            int sig = 17;
            for (int i = 0; i < list.Count; i++) if (Covered(list[i].cell)) sig = sig * 31 + i + 1;
            return sig;
        }

        void Rebuild(Vector2Int k, int sig)
        {
            GameObject old;
            if (meshes.TryGetValue(k, out old) && old != null) Destroy(old);
            var b = new NatureSprites.Batch();
            foreach (var it in chunks[k]) if (!Covered(it.cell)) b.Add(it.item, it.foot, it.h, it.sink, it.shade, it.flip);
            meshes[k] = b.Build(transform, "Decor_" + k.x + "_" + k.y);
            shownSig[k] = sig;
        }

        void Update()
        {
            if (Time.time < nextCheck) return;
            nextCheck = Time.time + 0.4f;
            foreach (var kv in chunks)
            {
                int sig = Signature(kv.Value);
                int was;
                if (!shownSig.TryGetValue(kv.Key, out was) || was != sig) Rebuild(kv.Key, sig);
            }
        }
    }
}
