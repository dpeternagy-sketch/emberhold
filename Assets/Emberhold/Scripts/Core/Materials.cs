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

        public static Material Emissive(Color color, float intensity)
        {
            var m = Unique(color);
            m.EnableKeyword("_EMISSION");
            if (m.HasProperty("_EmissionColor")) m.SetColor("_EmissionColor", color * intensity);
            return m;
        }
    }
}
