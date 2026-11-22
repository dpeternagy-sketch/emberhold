using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>Creates and caches simple lit materials that work in URP (or Built-in as fallback).</summary>
    public static class Materials
    {
        static Shader lit;
        static readonly Dictionary<Color, Material> cache = new Dictionary<Color, Material>();

        static Shader Lit
        {
            get
            {
                if (lit == null)
                {
                    lit = Shader.Find("Universal Render Pipeline/Lit");
                    if (lit == null) lit = Shader.Find("Standard");
                }
                return lit;
            }
        }

        public static Material Get(Color color)
        {
            Material m;
            if (cache.TryGetValue(color, out m) && m != null) return m;
            m = new Material(Lit);
            m.enableInstancing = true;
            SetColor(m, color);
            if (m.HasProperty("_Smoothness")) m.SetFloat("_Smoothness", 0.15f);
            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", 0.15f);
            cache[color] = m;
            return m;
        }

        /// <summary>A unique (not shared) material, for things that get tinted at runtime.</summary>
        public static Material Unique(Color color)
        {
            var m = new Material(Get(color));
            return m;
        }

        public static void SetColor(Material m, Color c)
        {
            if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
            if (m.HasProperty("_Color")) m.SetColor("_Color", c);
        }

        static readonly Dictionary<string, Material> emissiveCache = new Dictionary<string, Material>();

        public static Material Emissive(Color color, float intensity)
        {
            string key = color.ToString() + intensity;
            Material cached;
            if (emissiveCache.TryGetValue(key, out cached) && cached != null) return cached;
            var m = Unique(color);
            emissiveCache[key] = m;
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            return m;
        }

        static Material water;
        static Texture2D waterNormal;

        /// <summary>Shared water: glossy, with a moving ripple normal map so it sparkles and flows.</summary>
        public static Material Water()
        {
            if (water != null) return water;
            water = new Material(Lit);
            SetColor(water, new Color(0.14f, 0.52f, 0.64f));   // closer to the turquoise of the painted waterfall pool it flows from (QC run 36)
            if (water.HasProperty("_Smoothness")) water.SetFloat("_Smoothness", 0.93f);
            if (water.HasProperty("_Metallic")) water.SetFloat("_Metallic", 0.1f);
            if (water.HasProperty("_BumpMap"))
            {
                water.SetTexture("_BumpMap", WaterNormal());
                water.SetFloat("_BumpScale", 0.6f);
                water.EnableKeyword("_NORMALMAP");
                water.SetTextureScale("_BumpMap", new Vector2(6f, 6f));
            }
            return water;
        }

        static Texture2D WaterNormal()
        {
            if (waterNormal != null) return waterNormal;
            const int n = 128;
            var h = new float[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * Mathf.PI * 2f, v = y / (float)n * Mathf.PI * 2f;
                    h[y * n + x] = Mathf.Sin(u * 3f + Mathf.Sin(v * 2f)) * 0.5f + Mathf.Sin(v * 5f + Mathf.Cos(u * 4f)) * 0.3f + Mathf.Sin((u + v) * 7f) * 0.2f;
                }
            waterNormal = new Texture2D(n, n, TextureFormat.RGBA32, true, true) { wrapMode = TextureWrapMode.Repeat };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = h[y * n + (x + 1) % n] - h[y * n + (x + n - 1) % n];
                    float dy = h[((y + 1) % n) * n + x] - h[((y + n - 1) % n) * n + x];
                    var nn = new Vector3(-dx, -dy, 1.2f).normalized;
                    px[y * n + x] = new Color(nn.x * 0.5f + 0.5f, nn.y * 0.5f + 0.5f, nn.z * 0.5f + 0.5f, 1f);
                }
            waterNormal.SetPixels(px);
            waterNormal.Apply(true);
            return waterNormal;
        }
    }

    /// <summary>Slides a material's textures so water looks like it is flowing.</summary>
    public class WaterFlow : MonoBehaviour
    {
        public Vector2 speed = new Vector2(0f, -0.08f);
        public string property = "_BumpMap";
        public Material target;
        Vector2 offset;
        static int lastFrame = -1;   // the shared water material is moved once per frame only
        public bool shared = true;

        void Update()
        {
            if (target == null) return;
            if (shared) { if (lastFrame == Time.frameCount) return; lastFrame = Time.frameCount; }
            offset += speed * Time.deltaTime;
            target.SetTextureOffset(property, offset);
        }
    }
}
