using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Renders a small picture of each building (the real 3D model) for the build menu cards.
    /// Done once, off-screen, then cached.
    /// </summary>
    public static class Thumbnails
    {
        static readonly Dictionary<string, Texture2D> cache = new Dictionary<string, Texture2D>();
        static readonly Vector3 StudioPos = new Vector3(0f, -400f, 0f);
        const int Size = 256;

        public static Texture2D Get(BuildingDef def)
        {
            Texture2D tex;
            if (cache.TryGetValue(def.id, out tex) && tex != null) return tex;
            tex = SpriteBuilding.Thumbnail(def.id) ?? Render(def);
            cache[def.id] = tex;
            return tex;
        }

        static Texture2D Render(BuildingDef def)
        {
            var holder = new GameObject("ThumbStudio");
            holder.transform.position = StudioPos;
            var model = BuildingVisuals.Create(def, holder.transform);
            model.transform.localRotation = Quaternion.Euler(0, 0, 0);

            // simple floor tile so it reads as a placed building
            var floor = Util.Prim(PrimitiveType.Cube, holder.transform, new Vector3(0, -0.03f, 0),
                new Vector3(def.width + 0.4f, 0.06f, def.depth + 0.4f), new Color(0.36f, 0.5f, 0.24f));

            var camGo = new GameObject("ThumbCam");
            var cam = camGo.AddComponent<Camera>();
            cam.enabled = false;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.17f, 0.13f, 0.1f, 1f);
            cam.fieldOfView = 30f;
            cam.nearClipPlane = 0.1f;
            cam.farClipPlane = 60f;
            float extent = Mathf.Max(def.width, def.depth, def.height * 1.1f);
            float dist = extent * 2.6f + 1.2f;
            var dir = Quaternion.Euler(30f, -35f, 0f);
            var target = StudioPos + Vector3.up * def.height * 0.4f;
            cam.transform.rotation = dir;
            cam.transform.position = target - dir * Vector3.forward * dist;

            var light = new GameObject("ThumbLight").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 0.6f;
            light.transform.rotation = Quaternion.Euler(35f, 140f, 0f);

            var rt = RenderTexture.GetTemporary(Size, Size, 24, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Default, 4);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog;
            RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;

            var prev = RenderTexture.active;
            RenderTexture.active = rt;
            var tex = new Texture2D(Size, Size, TextureFormat.RGBA32, false);
            tex.ReadPixels(new Rect(0, 0, Size, Size), 0, 0);
            tex.Apply();
            RenderTexture.active = prev;
            cam.targetTexture = null;
            RenderTexture.ReleaseTemporary(rt);

            // Destroy NOW (not at the end of the frame), otherwise the next picture would show this model too.
            Object.DestroyImmediate(light.gameObject);
            Object.DestroyImmediate(camGo);
            Object.DestroyImmediate(holder);
            return tex;
        }
    }
}
