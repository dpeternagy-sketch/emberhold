using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Peter's painted mountain-and-waterfall image (drawn at the game's own camera angle) (Resources/Sprites/Backdrops/{name}), standing up on the
    /// mountain behind the HQ like a giant card that always faces the camera. The foot of the waterfall is
    /// anchored where the river springs, so the falls feed the river. Uses the IsoSprite shader, so it
    /// shares the day/night tint and sorts with the 3D terrain in front of it.
    /// </summary>
    public class Backdrop : MonoBehaviour
    {
        float width;
        Vector2 footUv;     // waterfall foot in the image (0..1 from the top-left)

        Material mat;
        Vector3 foot;
        float w, h;

        public static Backdrop Build(Transform parent, string name, float width, Vector2 footUv)
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
            b.width = width; b.footUv = footUv;
            b.w = width;
            b.h = width * tex.height / tex.width;
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
            float ox = (footUv.x - 0.5f) * w, oy = (0.5f - footUv.y) * h;
            transform.position = foot - (rot * Vector3.right) * ox - (rot * Vector3.up) * oy;
            var fwd = rot * Vector3.forward;
            // depth plane a little in front of the foot, so the hills just in front don't cut the painting's
            // bottom (treetops) off with a straight edge (QC run 26)
            mat.SetVector("_Center", foot - new Vector3(fwd.x, 0f, fwd.z).normalized * 2f);
            mat.SetVector("_Normal", -new Vector3(fwd.x, 0f, fwd.z).normalized);
            Shader.SetGlobalVector("_EH_BackPlane", foot - new Vector3(fwd.x, 0f, fwd.z).normalized * 2f);
            Shader.SetGlobalVector("_EH_BackNormal", -new Vector3(fwd.x, 0f, fwd.z).normalized);
        }

        void OnDestroy() { if (mat != null) Destroy(mat); }
    }
}
