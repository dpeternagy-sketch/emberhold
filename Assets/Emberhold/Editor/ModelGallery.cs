using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;

namespace Emberhold.EditorTools
{
    /// <summary>
    /// QA tool: lays out every model of a Resources folder in a labelled grid, renders it to
    /// Screens/gallery_*.png and writes each model's size to Screens/models.txt.
    /// </summary>
    static class ModelGallery
    {
        [MenuItem("Emberhold/Test/Model gallery (all packs)", priority = 170)]
        static void All()
        {
            var sb = new StringBuilder();
            foreach (var folder in new[] { "Models/City", "Models/Base", "Models/Dinos", "KayKit/nature" }) Render(folder, sb);
            Directory.CreateDirectory("Screens");
            File.WriteAllText("Screens/models.txt", sb.ToString());
            Debug.Log("[Emberhold] gallery done");
        }

        static void Render(string folder, StringBuilder sb)
        {
            var prefabs = Resources.LoadAll<GameObject>(folder);
            if (prefabs.Length == 0) { sb.AppendLine(folder + ": nothing"); return; }
            var root = new GameObject("Gallery");
            root.transform.position = new Vector3(0, -1000, 0);
            var items = new List<GameObject>();
            float cell = 1f;
            foreach (var p in prefabs)
            {
                var go = (GameObject)Object.Instantiate(p, root.transform);
                go.name = p.name;
                var b = Bounds(go);
                sb.AppendLine(folder + "/" + p.name + "  size " + b.size.ToString("F2") + "  center " + (b.center - go.transform.position).ToString("F2"));
                cell = Mathf.Max(cell, b.size.x, b.size.z);
                items.Add(go);
            }
            cell *= 1.25f;
            int cols = Mathf.CeilToInt(Mathf.Sqrt(items.Count));
            for (int i = 0; i < items.Count; i++)
            {
                var go = items[i];
                var pos = root.transform.position + new Vector3((i % cols) * cell, 0, -(i / cols) * cell);
                go.transform.position = pos;
                var label = new GameObject("label").AddComponent<TextMesh>();
                label.transform.SetParent(root.transform, false);
                label.text = go.name;
                label.fontSize = 48; label.characterSize = cell * 0.012f;
                label.anchor = TextAnchor.UpperCenter; label.color = Color.black;
                label.transform.position = pos + new Vector3(0, 0.05f, -cell * 0.38f);
                label.transform.rotation = Quaternion.Euler(90, 0, 0);
            }
            int rows = Mathf.CeilToInt(items.Count / (float)cols);
            var camGo = new GameObject("GalleryCam");
            var cam = camGo.AddComponent<Camera>();
            cam.orthographic = true;
            cam.clearFlags = CameraClearFlags.SolidColor; cam.backgroundColor = new Color(0.85f, 0.88f, 0.9f);
            var center = root.transform.position + new Vector3((cols - 1) * cell * 0.5f, 0, -(rows - 1) * cell * 0.5f);
            cam.transform.rotation = Quaternion.Euler(55, 0, 0);
            cam.transform.position = center - cam.transform.forward * 200f;
            cam.orthographicSize = Mathf.Max(cols, rows) * cell * 0.55f;
            cam.nearClipPlane = 1; cam.farClipPlane = 500;
            var light = new GameObject("GalleryLight").AddComponent<Light>();
            light.type = LightType.Directional; light.intensity = 1.2f;
            light.transform.rotation = Quaternion.Euler(50, -30, 0);
            const int S = 2048;
            var rt = new RenderTexture(S, S, 24);
            cam.targetTexture = rt;
            bool fog = RenderSettings.fog; RenderSettings.fog = false;
            cam.Render();
            RenderSettings.fog = fog;
            RenderTexture.active = rt;
            var tex = new Texture2D(S, S, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, S, S), 0, 0); tex.Apply();
            RenderTexture.active = null;
            Directory.CreateDirectory("Screens");
            File.WriteAllBytes("Screens/gallery_" + folder.Replace('/', '_') + ".png", tex.EncodeToPNG());
            cam.targetTexture = null;
            Object.DestroyImmediate(rt); Object.DestroyImmediate(tex);
            Object.DestroyImmediate(camGo); Object.DestroyImmediate(light.gameObject); Object.DestroyImmediate(root);
        }

        static Bounds Bounds(GameObject go)
        {
            var rs = go.GetComponentsInChildren<Renderer>();
            if (rs.Length == 0) return new Bounds(go.transform.position, Vector3.one);
            var b = rs[0].bounds;
            foreach (var r in rs) b.Encapsulate(r.bounds);
            return b;
        }
    }
}
