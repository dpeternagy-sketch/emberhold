using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>A tree that can be chopped by a lumberjack and regrows over time.</summary>
    public class TreeNode : MonoBehaviour
    {
        public static readonly List<TreeNode> All = new List<TreeNode>();

        public const float RegrowSeconds = 90f;
        public Vector2Int cell;
        public bool Standing { get; private set; }

        Transform canopy;
        float regrowAt;

        static readonly Color Trunk = new Color(0.33f, 0.23f, 0.15f);
        static readonly Color[] PineGreens = { new Color(0.13f, 0.29f, 0.17f), new Color(0.16f, 0.33f, 0.19f), new Color(0.11f, 0.25f, 0.15f) };
        static readonly Color[] LeafGreens = { new Color(0.24f, 0.42f, 0.16f), new Color(0.3f, 0.48f, 0.18f), new Color(0.36f, 0.5f, 0.17f), new Color(0.45f, 0.5f, 0.16f) };

        /// <summary>style: 1 = pine, 0 = leafy, -1 = random.</summary>
        public static TreeNode Create(Transform parent, Vector2Int cell, float scale, int style)
        {
            var go = new GameObject("Tree");
            go.transform.SetParent(parent, false);
            var pos = new Vector3(cell.x + 0.5f + Random.Range(-0.25f, 0.25f), 0f, cell.y + 0.5f + Random.Range(-0.25f, 0.25f));
            pos.y = WorldBuilder.GroundUnder(pos.x, pos.z, 0.35f) - 0.08f; // planted in the ground, never hanging over a slope
            go.transform.position = pos;
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var t = go.AddComponent<TreeNode>();
            t.cell = cell;

            bool pine = style == 1 || (style == -1 && Random.value < 0.45f);
            var root = new GameObject("Shape").transform;
            root.SetParent(go.transform, false);
            root.localScale = Vector3.one * scale;
            t.canopy = new GameObject("Canopy").transform;
            t.canopy.SetParent(root, false);

            if (pine)
            {
                Util.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.45f, 0), new Vector3(0.16f, 0.45f, 0.16f), Trunk);
                var g = PineGreens[Random.Range(0, PineGreens.Length)];
                float hgt = Random.Range(2.4f, 3.2f);
                for (int i = 0; i < 4; i++)
                {
                    float f = i / 3f;
                    float w = Mathf.Lerp(1.55f, 0.55f, f);
                    MeshFactory.Create(MeshFactory.Cone(9), t.canopy, new Vector3(0, 0.55f + f * hgt * 0.62f, 0), new Vector3(w, hgt * 0.42f, w), g * Mathf.Lerp(0.9f, 1.12f, f));
                }
            }
            else
            {
                Util.Prim(PrimitiveType.Cylinder, root, new Vector3(0, 0.6f, 0), new Vector3(0.2f, 0.6f, 0.2f), Trunk);
                var b1 = Util.Prim(PrimitiveType.Cylinder, root, new Vector3(0.18f, 1.15f, 0), new Vector3(0.09f, 0.35f, 0.09f), Trunk);
                b1.transform.localRotation = Quaternion.Euler(0, 0, -35);
                var g = LeafGreens[Random.Range(0, LeafGreens.Length)];
                int blobs = Random.Range(4, 7);
                for (int i = 0; i < blobs; i++)
                {
                    var off = new Vector3(Random.Range(-0.55f, 0.55f), Random.Range(1.4f, 2.1f), Random.Range(-0.55f, 0.55f));
                    float s = Random.Range(0.8f, 1.25f);
                    Util.Prim(PrimitiveType.Sphere, t.canopy, off, new Vector3(s, s * 0.85f, s), g * Random.Range(0.88f, 1.12f));
                }
            }
            t.Standing = true;
            All.Add(t);
            return t;
        }

        void OnDestroy() { All.Remove(this); }

        public void Chop()
        {
            if (!Standing) return;
            Standing = false;
            canopy.gameObject.SetActive(false);
            transform.localScale = new Vector3(1f, 0.25f, 1f); // stump
            regrowAt = Time.time + RegrowSeconds;
        }

        void Update()
        {
            if (Standing)
            {
                if (transform.localScale.x < 0.999f)
                    transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.one, Time.deltaTime * 0.1f);
                return;
            }
            if (Time.time >= regrowAt)
            {
                Standing = true;
                canopy.gameObject.SetActive(true);
                transform.localScale = Vector3.one * 0.4f;
            }
        }

        public static TreeNode NearestStanding(Vector3 from, float radius)
        {
            TreeNode best = null;
            float bestD = radius * radius;
            foreach (var t in All)
            {
                if (!t.Standing) continue;
                float d = (t.transform.position - from).sqrMagnitude;
                if (d < bestD) { bestD = d; best = t; }
            }
            return best;
        }
    }
}
