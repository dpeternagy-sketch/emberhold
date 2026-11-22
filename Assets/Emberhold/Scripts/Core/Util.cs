using UnityEngine;

namespace Emberhold
{
    public static class Util
    {
        public static GameObject Prim(PrimitiveType type, Transform parent, Vector3 localPos, Vector3 scale, Color color, bool keepCollider = false)
        {
            var go = GameObject.CreatePrimitive(type);
            if (!keepCollider)
            {
                var col = go.GetComponent<Collider>();
                if (col != null) Object.Destroy(col);
            }
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPos;
            go.transform.localScale = scale;
            go.GetComponent<Renderer>().sharedMaterial = Materials.Get(color);
            return go;
        }

        public static string FormatTime(float seconds)
        {
            if (seconds < 0) seconds = 0;
            int s = Mathf.CeilToInt(seconds);
            if (s >= 3600) return (s / 3600) + "h " + ((s % 3600) / 60) + "m";
            if (s >= 60) return (s / 60) + "m " + (s % 60) + "s";
            return s + "s";
        }
    }
}
