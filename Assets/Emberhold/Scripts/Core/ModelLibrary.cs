using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Loads art models from Resources and fits them to a footprint, so every model sits flat on
    /// the ground, centred on its cell, at a size that matches the game grid whatever its source scale.
    /// </summary>
    public static class ModelLibrary
    {
        static readonly Dictionary<string, GameObject> cache = new Dictionary<string, GameObject>();

        public static bool Has(string path) { return Load(path) != null; }

        static GameObject Load(string path)
        {
            GameObject p;
            if (cache.TryGetValue(path, out p) && p != null) return p;
            p = Resources.Load<GameObject>(path);
            cache[path] = p;
            return p;
        }

        /// <summary>
        /// Instantiates the model under a holder. It is scaled uniformly so its footprint fits
        /// width x depth (and its height fits maxHeight if given), then centred with its base at y = 0.
        /// Returns the holder (null if the model is missing).
        /// </summary>
        public static Transform Spawn(string path, Transform parent, float width, float depth, float maxHeight = 0f, string name = null)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var holder = new GameObject(name ?? prefab.name).transform;
            holder.SetParent(parent, false);
            var m = Object.Instantiate(prefab, holder, false);
            m.name = "Model";
            foreach (var c in m.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            var b = LocalBounds(m.transform, holder);
            float s = Mathf.Min(width / Mathf.Max(0.01f, b.size.x), depth / Mathf.Max(0.01f, b.size.z));
            if (maxHeight > 0f) s = Mathf.Min(s, maxHeight / Mathf.Max(0.01f, b.size.y));
            m.transform.localScale *= s;
            m.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z) * s;
            return holder;
        }

        /// <summary>Scales so the model's longest side on the ground equals length (for creatures).</summary>
        public static Transform SpawnByLength(string path, Transform parent, float length)
        {
            var prefab = Load(path);
            if (prefab == null) return null;
            var holder = new GameObject(prefab.name).transform;
            holder.SetParent(parent, false);
            var m = Object.Instantiate(prefab, holder, false);
            m.name = "Model";
            foreach (var c in m.GetComponentsInChildren<Collider>()) Object.Destroy(c);
            var b = LocalBounds(m.transform, holder);
            float s = length / Mathf.Max(0.01f, Mathf.Max(b.size.x, b.size.z));
            m.transform.localScale *= s;
            m.transform.localPosition -= new Vector3(b.center.x, b.min.y, b.center.z) * s;
            return holder;
        }

        public static Bounds LocalBounds(Transform t, Transform space)
        {
            var rs = t.GetComponentsInChildren<Renderer>();
            bool any = false;
            var b = new Bounds();
            foreach (var r in rs)
            {
                Bounds rb;
                var smr = r as SkinnedMeshRenderer;
                if (smr != null && smr.sharedMesh != null) rb = Transform(smr.sharedMesh.bounds, smr.transform, space);
                else
                {
                    var mf = r.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null) continue;
                    rb = Transform(mf.sharedMesh.bounds, r.transform, space);
                }
                if (!any) { b = rb; any = true; } else b.Encapsulate(rb);
            }
            return any ? b : new Bounds(Vector3.zero, Vector3.one);
        }

        static Bounds Transform(Bounds local, Transform from, Transform to)
        {
            var c = local.center; var e = local.extents;
            var result = new Bounds(to.InverseTransformPoint(from.TransformPoint(c)), Vector3.zero);
            for (int i = 0; i < 8; i++)
            {
                var corner = c + new Vector3((i & 1) == 0 ? -e.x : e.x, (i & 2) == 0 ? -e.y : e.y, (i & 4) == 0 ? -e.z : e.z);
                result.Encapsulate(to.InverseTransformPoint(from.TransformPoint(corner)));
            }
            return result;
        }

        /// <summary>Plays the first legacy clip whose name ends with the given action (e.g. "_Run").</summary>
        public static bool Play(Animation anim, string action, float fade = 0.2f)
        {
            if (anim == null) return false;
            foreach (AnimationState st in anim)
                if (st.name.EndsWith("_" + action))
                {
                    if (!anim.IsPlaying(st.name)) anim.CrossFade(st.name, fade);
                    return true;
                }
            return false;
        }
    }
}
