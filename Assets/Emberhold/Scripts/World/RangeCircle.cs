using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// A tinted disc on the ground that shows how far a weapon reaches.
    /// Shown while placing a weapon and when a weapon is selected.
    /// </summary>
    public class RangeCircle : MonoBehaviour
    {
        static RangeCircle instance;
        Material mat;
        float shownRadius;

        static Texture2D ringTexture;

        public static void Show(Vector3 center, float radius, bool valid)
        {
            if (instance == null) instance = Create();
            var t = instance.transform;
            t.gameObject.SetActive(true);
            t.position = new Vector3(center.x, WorldBuilder.HeightAt(center) + 0.07f, center.z);
            // ease the size so it grows smoothly after an upgrade
            instance.shownRadius = instance.shownRadius <= 0f ? radius : Mathf.Lerp(instance.shownRadius, radius, 0.25f);
            t.localScale = new Vector3(instance.shownRadius * 2f, 1f, instance.shownRadius * 2f);
            var c = valid ? new Color(1f, 0.82f, 0.3f, 1f) : new Color(1f, 0.3f, 0.25f, 1f);
            Materials.SetColor(instance.mat, c);
        }

        public static void Hide()
        {
            if (instance != null && instance.gameObject.activeSelf)
            {
                instance.gameObject.SetActive(false);
                instance.shownRadius = 0f;
            }
        }

        static RangeCircle Create()
        {
            var go = new GameObject("RangeCircle");
            var mf = go.AddComponent<MeshFilter>();
            var mr = go.AddComponent<MeshRenderer>();
            mr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            mr.receiveShadows = false;
            mf.sharedMesh = QuadMesh();
            var rc = go.AddComponent<RangeCircle>();
            rc.mat = new Material(DamageEffects.ParticleMaterial(false, RingTexture()));
            // QC: drawn before the building sprites and the water, so plates and the river sit on top of the ring
            // (it used to tint plates and wash the river grey inside the circle)
            if (rc.mat.HasProperty("_ZWrite")) rc.mat.SetFloat("_ZWrite", 0f);
            rc.mat.renderQueue = 2010;
            mr.sharedMaterial = rc.mat;
            return rc;
        }

        static Mesh QuadMesh()
        {
            var m = new Mesh { name = "RangeQuad" };
            m.vertices = new[] { new Vector3(-0.5f, 0, -0.5f), new Vector3(-0.5f, 0, 0.5f), new Vector3(0.5f, 0, 0.5f), new Vector3(0.5f, 0, -0.5f) };
            m.uv = new[] { new Vector2(0, 0), new Vector2(0, 1), new Vector2(1, 1), new Vector2(1, 0) };
            m.colors = new[] { Color.white, Color.white, Color.white, Color.white };
            m.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            m.RecalculateNormals();
            m.RecalculateBounds();
            return m;
        }

        /// <summary>Soft filled disc with a bright rim and a few dashes, so it reads clearly on grass.</summary>
        static Texture2D RingTexture()
        {
            if (ringTexture != null) return ringTexture;
            const int n = 256;
            ringTexture = new Texture2D(n, n, TextureFormat.RGBA32, true) { wrapMode = TextureWrapMode.Clamp };
            var px = new Color[n * n];
            for (int y = 0; y < n; y++)
                for (int x = 0; x < n; x++)
                {
                    float dx = (x + 0.5f) / n * 2f - 1f, dy = (y + 0.5f) / n * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = 0f;
                    if (r < 1f)
                    {
                        a = 0.16f + 0.1f * Mathf.SmoothStep(0.5f, 0.95f, r);           // tinted fill, a bit stronger outward
                        float rim = 1f - Mathf.Clamp01(Mathf.Abs(r - 0.965f) / 0.03f);   // bright rim
                        float ang = Mathf.Atan2(dy, dx);
                        float dash = Mathf.Sin(ang * 36f) > -0.2f ? 1f : 0.45f;
                        a = Mathf.Max(a, rim * 0.95f * dash);
                    }
                    px[y * n + x] = new Color(1f, 1f, 1f, a);
                }
            ringTexture.SetPixels(px);
            ringTexture.Apply();
            return ringTexture;
        }
    }
}
