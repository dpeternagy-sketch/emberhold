using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Something that reveals the dark at night (castle, torch, watchtower).
    /// Enemies are only visible inside a light's radius.
    /// </summary>
    public class LightSource : MonoBehaviour
    {
        public static readonly List<LightSource> All = new List<LightSource>();

        public float radius = 5f;
        public Light pointLight;
        float flickerSeed;

        public static LightSource Attach(GameObject go, float radius, float height)
        {
            var ls = go.AddComponent<LightSource>();
            ls.radius = radius;
            var lgo = new GameObject("Light");
            lgo.transform.SetParent(go.transform, false);
            lgo.transform.localPosition = new Vector3(0, height, 0);
            var l = lgo.AddComponent<Light>();
            l.type = LightType.Point;
            l.color = new Color(1f, 0.72f, 0.4f);
            l.range = radius * 1.6f;
            l.intensity = 0f;
            l.shadows = LightShadows.None;
            ls.pointLight = l;
            ls.flickerSeed = Random.value * 10f;
            return ls;
        }

        void OnEnable() { All.Add(this); }
        void OnDisable() { All.Remove(this); }

        void Update()
        {
            if (pointLight == null) return;
            float night = DayNightCycle.Instance != null ? DayNightCycle.Instance.Darkness : 0f;
            float flicker = 1f + 0.08f * Mathf.Sin(Time.time * 9f + flickerSeed) + 0.05f * Mathf.Sin(Time.time * 23f + flickerSeed);
            pointLight.intensity = night * 2.2f * flicker;
            pointLight.enabled = night > 0.02f;
        }

        public static bool IsLit(Vector3 p, float margin = 0f)
        {
            foreach (var l in All)
            {
                float r = l.radius + margin;
                var d = l.transform.position - p;
                d.y = 0;
                if (d.sqrMagnitude <= r * r) return true;
            }
            return false;
        }
    }
}
