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
        float fullScale;

        public static TreeNode Create(Transform parent, Vector2Int cell, float scale)
        {
            var go = new GameObject("Tree");
            go.transform.SetParent(parent, false);
            go.transform.position = new Vector3(cell.x + 0.5f + Random.Range(-0.2f, 0.2f), 0f, cell.y + 0.5f + Random.Range(-0.2f, 0.2f));
            go.transform.rotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
            var t = go.AddComponent<TreeNode>();
            t.cell = cell;
            t.fullScale = scale;

            var trunkColor = new Color(0.36f, 0.25f, 0.16f);
            Util.Prim(PrimitiveType.Cylinder, go.transform, new Vector3(0, 0.5f * scale, 0), new Vector3(0.18f, 0.5f, 0.18f) * scale, trunkColor);

            t.canopy = new GameObject("Canopy").transform;
            t.canopy.SetParent(go.transform, false);
            var g = Random.Range(0.32f, 0.45f);
            var leaf = new Color(0.16f, g, 0.14f);
            bool pine = Random.value < 0.5f;
            if (pine)
            {
                for (int i = 0; i < 3; i++)
                {
                    float s = (1.3f - i * 0.35f) * scale;
                    Util.Prim(PrimitiveType.Sphere, t.canopy, new Vector3(0, (1.0f + i * 0.55f) * scale, 0), new Vector3(s, s * 0.8f, s), leaf);
                }
            }
            else
            {
                Util.Prim(PrimitiveType.Sphere, t.canopy, new Vector3(0, 1.4f * scale, 0), new Vector3(1.4f, 1.2f, 1.4f) * scale, leaf);
                Util.Prim(PrimitiveType.Sphere, t.canopy, new Vector3(0.35f, 1.7f * scale, 0.2f), new Vector3(0.9f, 0.8f, 0.9f) * scale, leaf * 1.1f);
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
            if (Standing) return;
            if (Time.time >= regrowAt)
            {
                Standing = true;
                canopy.gameObject.SetActive(true);
                transform.localScale = Vector3.one * 0.4f;
            }
        }

        void LateUpdate()
        {
            // regrow animation: grow back to full size
            if (Standing && transform.localScale.x < 0.999f)
                transform.localScale = Vector3.MoveTowards(transform.localScale, Vector3.one, Time.deltaTime * 0.1f);
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
