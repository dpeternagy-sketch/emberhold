using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// A dinosaur drawn from Peter's painted portrait (Resources/Sprites/Portraits/{raptor,trike,trex,ptero}_{1..3}):
    /// one quad that faces the locked camera, standing on the dino's feet, with a soft shadow on the ground.
    /// The picture is mirrored to face the way it walks, and simple motion sells it: a step bob and sway
    /// while walking, a lunge on each bite, a white flash when hit, wing beats for the pterosaur, and a
    /// fall and fade when it dies.
    /// </summary>
    public class DinoSprite : MonoBehaviour
    {
        static Mesh quadMesh;
        static Shader shader;
        static Material shadowMat;

        Material mat;
        Transform pic, shadow;
        float h, aspect = 1f;
        bool lookRightInPicture, flyer;
        float turn = 1f, wantTurn = 1f;     // +1 = picture as painted, -1 = mirrored
        Vector3 lastPos;
        float phase, moving, stepFreq;
        float attackT = 9f, flash, fade, dieT = -1f;
        float seed;

        public float Height { get { return h * (flyer ? 0.55f : 0.75f); } }

        public static bool Available
        {
            get
            {
                if (shader == null) shader = Resources.Load<Shader>("Shaders/UnitSprite");
                return shader != null && shader.isSupported && Resources.Load<Texture2D>("Sprites/Portraits/raptor_1") != null;
            }
        }

        static string Base(EnemyKind k)
        {
            return k == EnemyKind.Boss ? "trex" : k == EnemyKind.Brute ? "trike" : k == EnemyKind.Flyer ? "ptero" : "raptor";
        }

        /// <summary>Picture height in world units (the painted creature fills most of its square).</summary>
        static float Size(EnemyKind k)
        {
            return k == EnemyKind.Boss ? 3.9f : k == EnemyKind.Brute ? 2.3f : k == EnemyKind.Flyer ? 2.1f : 1.9f;
        }

        /// <summary>Stronger nights bring the armoured looks (tier 2 from night 3, tier 3 from night 6).</summary>
        public static DinoSprite Create(Transform owner, EnemyKind kind, int strength, float scale = 1f)
        {
            if (!Available) return null;
            int tier = strength >= 6 ? 3 : strength >= 3 ? 2 : 1;
            var tex = Resources.Load<Texture2D>("Sprites/Portraits/" + Base(kind) + "_" + tier);
            if (tex == null) tex = Resources.Load<Texture2D>("Sprites/Portraits/" + Base(kind) + "_1");
            if (tex == null) return null;

            var d = owner.gameObject.AddComponent<DinoSprite>();
            d.h = Size(kind) * scale;
            d.aspect = tex.width / (float)tex.height;
            d.flyer = kind == EnemyKind.Flyer;
            d.lookRightInPicture = kind == EnemyKind.Flyer;      // the pterosaur is painted facing right, the others left
            d.stepFreq = kind == EnemyKind.Raider ? 11f : kind == EnemyKind.Boss ? 5f : 7f;
            d.seed = Random.Range(0f, 10f);

            if (quadMesh == null)
            {
                quadMesh = new Mesh { name = "DinoQuad" };
                quadMesh.vertices = new[] { new Vector3(-0.5f, 0, 0), new Vector3(0.5f, 0, 0), new Vector3(0.5f, 1, 0), new Vector3(-0.5f, 1, 0) };
                quadMesh.uv = new[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(1, 1), new Vector2(0, 1) };
                quadMesh.triangles = new[] { 0, 2, 1, 0, 3, 2 };
                quadMesh.RecalculateBounds();
                quadMesh.bounds = new Bounds(new Vector3(0, 0.5f, 0), new Vector3(2, 2, 2));
            }
            d.mat = new Material(shader);
            d.mat.SetTexture("_MainTex", tex);
            d.mat.SetFloat("_Fade", 0f);

            d.pic = new GameObject("DinoSprite").transform;
            d.pic.SetParent(owner, false);
            d.pic.gameObject.AddComponent<MeshFilter>().sharedMesh = quadMesh;
            var r = d.pic.gameObject.AddComponent<MeshRenderer>();
            r.sharedMaterial = d.mat;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;

            if (shadowMat == null)
            {
                shadowMat = DamageEffects.ParticleMaterial(false, DamageEffects.SoftDot);
                var dark = new Color(0f, 0.02f, 0.05f, 0.45f);
                if (shadowMat.HasProperty("_BaseColor")) shadowMat.SetColor("_BaseColor", dark);
                if (shadowMat.HasProperty("_Color")) shadowMat.SetColor("_Color", dark);
            }
            var sh = GameObject.CreatePrimitive(PrimitiveType.Quad);
            Object.Destroy(sh.GetComponent<Collider>());
            sh.name = "Shadow";
            d.shadow = sh.transform;
            d.shadow.SetParent(owner, false);
            var sr = sh.GetComponent<MeshRenderer>();
            sr.sharedMaterial = shadowMat;
            sr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            sr.receiveShadows = false;

            d.lastPos = owner.position;
            d.LateUpdate();
            return d;
        }

        /// <summary>0..1 while spawning in or fleeing away.</summary>
        public void SetFade(float f) { fade = f; }
        public void Flash() { flash = 1f; }
        public void Attack() { attackT = 0f; }
        public bool Dying { get { return dieT >= 0f; } }
        /// <summary>Falls over and fades; returns how long that takes.</summary>
        public float Die() { if (dieT < 0f) dieT = 0f; return 1.3f; }

        void LateUpdate()
        {
            if (pic == null) return;
            float dt = Mathf.Max(Time.deltaTime, 1e-4f);
            var facing = NatureSprites.Facing;
            var right = facing * Vector3.right;
            var p = transform.position;

            // which way is it walking on screen?
            var delta = p - lastPos; lastPos = p;
            delta.y = 0f;
            float speed = delta.magnitude / dt;
            moving = Mathf.MoveTowards(moving, speed > 0.15f && dieT < 0f ? 1f : 0f, dt * 5f);
            float sx = Vector3.Dot(delta, right);
            if (Mathf.Abs(sx) > 0.0005f)
            {
                bool goingRight = sx > 0f;
                wantTurn = goingRight == lookRightInPicture ? 1f : -1f;
            }
            turn = Mathf.MoveTowards(turn, wantTurn, dt * 9f);
            float side = Mathf.Sign(turn) * (lookRightInPicture ? 1f : -1f);    // +1 when the head points screen-right

            float sxScale = 1f, syScale = 1f, tilt = 0f, lift = 0f, lunge = 0f;
            float t = Time.time + seed;
            if (flyer)
            {
                float beat = Mathf.Sin(t * 9f);
                syScale = 1f + beat * 0.1f;
                sxScale = 1f - beat * 0.04f;
                lift = Mathf.Sin(t * 2.2f) * 0.12f;
                tilt = -side * (3f + moving * 4f);
            }
            else
            {
                phase += dt * stepFreq * moving;
                lift = Mathf.Abs(Mathf.Sin(phase)) * 0.045f * h * moving;
                tilt = Mathf.Sin(phase) * 2.5f * moving;
                syScale = 1f + Mathf.Sin(t * 2.4f) * 0.012f * (1f - moving);     // breathing when standing
            }
            if (attackT < 0.4f)
            {
                attackT += dt;
                float k = Mathf.Sin(Mathf.Clamp01(attackT / 0.4f) * Mathf.PI);
                lunge = k * 0.14f * h;
                tilt += -side * 9f * k;
                sxScale *= 1f + 0.06f * k;
                syScale *= 1f - 0.05f * k;
            }
            float shade = 1f, vis = fade;
            if (dieT >= 0f)
            {
                dieT += dt;
                float fall = Mathf.SmoothStep(0f, 1f, dieT / 0.45f);
                tilt = side * 80f * fall;                 // tips over backwards
                lift = flyer ? -Mathf.Min(dieT * 2.5f, 3f) : -0.15f * h * fall;
                shade = Mathf.Lerp(1f, 0.55f, fall);
                vis = Mathf.Min(vis, 1f - Mathf.Clamp01((dieT - 0.5f) / 0.8f));
            }
            flash = Mathf.MoveTowards(flash, 0f, dt * 6f);

            float w = h * aspect;
            pic.position = p + right * (side * lunge) + Vector3.up * lift;
            pic.rotation = facing * Quaternion.Euler(0f, 0f, tilt);
            pic.localScale = new Vector3(w * turn * sxScale, h * syScale, 1f);
            mat.SetFloat("_Fade", vis);
            mat.SetFloat("_Flash", flash);
            mat.SetFloat("_Shade", shade);

            // soft shadow on the ground under it, smaller and fainter when the flyer is high
            float gy = WorldBuilder.HeightAt(p);
            float air = Mathf.Max(0f, p.y - gy);
            float s = (flyer ? 1.2f : w * 0.55f) * Mathf.Lerp(1f, 0.6f, Mathf.Clamp01(air / 4f));
            shadow.position = new Vector3(p.x, gy + 0.04f, p.z);
            shadow.rotation = Quaternion.Euler(90f, CameraRig.Yaw, 0f);
            shadow.localScale = new Vector3(s, s * 0.55f, 1f) * Mathf.Clamp01(vis * 1.5f);
        }

        void OnDestroy()
        {
            if (mat != null) Destroy(mat);
        }
    }
}
