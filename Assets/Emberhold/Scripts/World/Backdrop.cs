using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Peter's painted mountain-and-waterfall image (Resources/Sprites/Backdrops/{name}), standing up on the
    /// mountain behind the HQ like a giant card that always faces the camera. The foot of the waterfall is
    /// anchored where the river springs, so the falls feed the river. Uses the IsoSprite shader, so it
    /// shares the day/night tint and sorts with the 3D terrain in front of it.
    /// </summary>
    public class Backdrop : MonoBehaviour
    {
        const float Width = 64f;                                   // world units across the screen
        static readonly Vector2 Foot = new Vector2(0.52f, 0.96f);  // waterfall foot in the image (0..1 from the top-left)

        Material mat;
        Vector3 foot;
        float w, h;

        public static Backdrop Build(Transform parent, string name)
        {
            var tex = Resources.Load<Texture2D>("Sprites/Backdrops/" + name);
            var shader = Resources.Load<Shader>("Shaders/IsoSprite");
            if (tex == null || shader == null) return null;
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = "Backdrop";
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            var b = q.AddComponent<Backdrop>();
            b.mat = new Material(shader);
            b.mat.SetTexture("_MainTex", tex);
            b.mat.SetTexture("_EmisTex", Texture2D.blackTexture);
            b.mat.SetFloat("_Cut", 1f);
            b.mat.SetFloat("_Shade", 1f);
            b.mat.SetColor("_Overlay", new Color(1, 1, 1, 0));
            r.sharedMaterial = b.mat;
            float z = River.StartZ - 1f;
            float x = River.CenterX(z);
            b.foot = new Vector3(x, WorldBuilder.HeightAt(x, z) - 0.4f, z);
            b.w = Width;
            b.h = Width * tex.height / tex.width;
            return b;
        }

        void LateUpdate()
        {
            var cam = Camera.main;
            if (cam == null) return;
            var rot = cam.transform.rotation;
            transform.rotation = rot;
            transform.localScale = new Vector3(w, h, 1f);
            // move the quad centre so the image's foot point lands on the river spring
            float ox = (Foot.x - 0.5f) * w, oy = (0.5f - Foot.y) * h;
            transform.position = foot - (rot * Vector3.right) * ox - (rot * Vector3.up) * oy;
            var fwd = rot * Vector3.forward;
            mat.SetVector("_Center", foot);
            mat.SetVector("_Normal", -new Vector3(fwd.x, 0f, fwd.z).normalized);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
