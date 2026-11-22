using System.Collections.Generic;
using UnityEngine;

namespace Emberhold
{
    /// <summary>
    /// Shows a building as one of Peter's pre-rendered isometric images instead of a 3D model.
    /// Images live in Resources/Sprites/Buildings/{id}_t{1|3|5}.png with a glow mask ({..}_emis.png)
    /// and a sidecar ({..}.txt: anchor x, anchor y, plate width, in pixels from the top-left), all made
    /// by Tools/sprite_cutout.py. The level picks the highest tier image at or below it.
    ///
    /// Sits on the building's "Model" object so Building's existing animations still drive it:
    /// model scale Y = construction reveal, model position = hit shake, odd rotation = mirror flip.
    /// The quad itself hangs off the building root, so those transforms never distort the picture.
    /// </summary>
    public class SpriteBuilding : MonoBehaviour
    {
        public const string QuadName = "IsoSprite";
        static readonly int[] Tiers = { 1, 3, 5 };

        class Art { public Texture2D tex, emis; public float cx, cy, plate; }
        static readonly Dictionary<string, Art> cache = new Dictionary<string, Art>();
        static Shader shader;

        BuildingDef def;
        Building building;
        Transform quad;
        Material mat;
        Art art;
        int shownTier = -1;
        Color overlay = new Color(1, 1, 1, 0);

        static Art Load(string key)
        {
            Art a;
            if (cache.TryGetValue(key, out a)) return a;
            var tex = Resources.Load<Texture2D>("Sprites/Buildings/" + key);
            var meta = Resources.Load<TextAsset>("Sprites/Buildings/" + key);
            a = null;
            if (tex != null && meta != null)
            {
                var p = meta.text.Trim().Split(' ');
                a = new Art
                {
                    tex = tex,
                    emis = Resources.Load<Texture2D>("Sprites/Buildings/" + key + "_emis"),
                    cx = float.Parse(p[0], System.Globalization.CultureInfo.InvariantCulture),
                    cy = float.Parse(p[1], System.Globalization.CultureInfo.InvariantCulture),
                    plate = float.Parse(p[2], System.Globalization.CultureInfo.InvariantCulture),
                };
            }
            cache[key] = a;
            return a;
        }

        static int LowestTier(string id)
        {
            foreach (var t in Tiers) if (Load(id + "_t" + t) != null) return t;
            return 0;
        }

        public static bool Has(string id) { return LowestTier(id) > 0; }

        /// <summary>Build-menu picture: the lowest-tier image itself.</summary>
        public static Texture2D Thumbnail(string id) { int t = LowestTier(id); return t > 0 ? Load(id + "_t" + t).tex : null; }

        /// <summary>Highest tier at or below the level; until the L1 art exists, the lowest tier we have.</summary>
        static int TierFor(string id, int level)
        {
            int best = LowestTier(id);
            foreach (var t in Tiers) if (t <= level && Load(id + "_t" + t) != null) best = t;
            return best;
        }

        public static GameObject Create(BuildingDef def, Transform parent)
        {
            var root = new GameObject("Model");
            root.transform.SetParent(parent, false);
            var sb = root.AddComponent<SpriteBuilding>();
            sb.def = def;
            if (shader == null) shader = Resources.Load<Shader>("Shaders/IsoSprite");
            var q = GameObject.CreatePrimitive(PrimitiveType.Quad);
            q.name = QuadName;
            Object.Destroy(q.GetComponent<Collider>());
            q.transform.SetParent(parent, false);
            var r = q.GetComponent<MeshRenderer>();
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            r.receiveShadows = false;
            sb.mat = new Material(shader);
            r.sharedMaterial = sb.mat;
            sb.quad = q.transform;
            return root;
        }

        /// <summary>Ghost colouring (placement) and charred ruins: Building.SetTint routes here.</summary>
        public void SetTint(Color? tint)
        {
            overlay = tint.HasValue ? new Color(tint.Value.r, tint.Value.g, tint.Value.b, 0.4f) : new Color(1, 1, 1, 0);
        }

        /// <summary>Day/night look for every building image. dark: 0 = noon, 1 = midnight.</summary>
        public static void SetLighting(float dark)
        {
            Shader.SetGlobalColor("_EH_SpriteTint", Color.Lerp(Color.white, new Color(0.46f, 0.52f, 0.78f, 1f), dark));
            Shader.SetGlobalFloat("_EH_SpriteGlow", Mathf.Lerp(0.05f, 0.75f, dark));
        }

        void OnDestroy()
        {
            if (quad != null) Destroy(quad.gameObject);
            if (mat != null) Destroy(mat);
        }

        void LateUpdate()
        {
            if (quad == null) return;
            if (building == null) building = GetComponentInParent<Building>();
            int level = building != null && building.Data != null ? building.Data.level : 1;
            int tier = TierFor(def.id, level);
            if (tier != shownTier)
            {
                shownTier = tier;
                art = Load(def.id + "_t" + tier);
                if (art == null) { quad.gameObject.SetActive(false); return; }
                mat.SetTexture("_MainTex", art.tex);
                mat.SetTexture("_EmisTex", art.emis != null ? art.emis : Texture2D.blackTexture);
            }
            if (art == null) return;

            var cam = Camera.main;
            if (cam == null) return;
            var rot = cam.transform.rotation;
            int rotation = building != null && building.Data != null ? building.Data.rotation : 0;
            bool flip = (rotation & 1) == 1;
            var size = GridMap.Size(def, rotation);
            float diamond = Mathf.Max(size.x, size.y) * 1.41421356f;     // footprint diagonal as seen from 45 degrees
            float w = diamond * art.tex.width / art.plate;
            float ox = (art.cx / art.tex.width - 0.5f) * w * (flip ? -1f : 1f);
            float oy = (0.5f - art.cy / art.tex.height) * w;

            var ground = transform.parent.position;
            quad.rotation = rot;
            quad.localScale = new Vector3(flip ? -w : w, w, 1f);
            quad.position = ground - (rot * Vector3.right) * ox - (rot * Vector3.up) * oy + transform.localPosition;

            bool ruined = building != null && building.Ruined;
            bool building_ = building != null && building.UnderConstruction && !building.IsGhost;
            float levelScale = 1f + 0.06f * (level - 1);
            float reveal = building_ ? Mathf.Clamp01(transform.localScale.y / levelScale) : 1f;
            var fwd = rot * Vector3.forward;
            var n = -new Vector3(fwd.x, 0f, fwd.z).normalized;
            mat.SetVector("_Center", ground);
            mat.SetVector("_Normal", n);
            mat.SetFloat("_Cut", ruined ? 1f : Mathf.Lerp(0.18f, 1f, reveal));
            mat.SetFloat("_Shade", ruined ? 0.42f : 1f);
            mat.SetColor("_Overlay", ruined ? new Color(0.2f, 0.17f, 0.15f, 0.35f) : overlay);
        }
    }
}
